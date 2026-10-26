using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Recruitment.Application.Abstractions;

namespace Recruitment.Infrastructure.Calendar;

/// <summary>
/// Meşgul saatleri Microsoft Graph <c>getSchedule</c> ile okur (uygulama izni, istemci sırrı ile).
/// Yalnızca zaman aralığı alınır; toplantı konusu ve katılımcılar okunmaz. Hata durumunda istisna
/// fırlatmaz, <see cref="OutlookStatus.Unavailable"/> döner ve nedeni loglanır.
/// </summary>
internal sealed class GraphCalendarService(
    IHttpClientFactory httpClientFactory,
    IOptions<GraphOptions> options,
    IMemoryCache cache,
    TimeProvider clock,
    ILogger<GraphCalendarService> logger) : ICalendarService
{
    public const string ApiClientName = "graph";
    public const string TokenClientName = "graph-token";

    // getSchedule tek istekte en fazla 20 kişi alır.
    private const int BatchSize = 20;

    // Bu durumlar kişiyi meşgul sayar; free ve workingElsewhere sayılmaz.
    private static readonly HashSet<string> BusyStatuses = new(StringComparer.OrdinalIgnoreCase) { "busy", "tentative", "oof" };

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _token;
    private DateTimeOffset _tokenExpiresAt;

    private GraphOptions Options => options.Value;

    public bool IsEnabled => Options.IsConfigured;

    public async Task<IReadOnlyDictionary<string, MailboxBusy>> GetBusy(
        IReadOnlyCollection<string> mailboxes, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var result = new Dictionary<string, MailboxBusy>(StringComparer.OrdinalIgnoreCase);
        if (!IsEnabled)
        {
            foreach (var mailbox in mailboxes)
                result[mailbox] = MailboxBusy.Of(OutlookStatus.NotConfigured);
            return result;
        }

        var missing = new List<string>();
        foreach (var mailbox in mailboxes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (cache.TryGetValue(CacheKey(mailbox, from, to), out MailboxBusy? cached) && cached is not null)
                result[mailbox] = cached;
            else
                missing.Add(mailbox);
        }

        foreach (var batch in missing.Chunk(BatchSize))
        {
            var fetched = await Fetch(batch, from, to, ct);
            foreach (var mailbox in batch)
            {
                var busy = fetched.GetValueOrDefault(mailbox) ?? MailboxBusy.Of(OutlookStatus.Unavailable);
                result[mailbox] = busy;
                if (busy.Status is OutlookStatus.Ok or OutlookStatus.NoMailbox)
                    cache.Set(CacheKey(mailbox, from, to), busy, TimeSpan.FromSeconds(Math.Max(Options.CacheSeconds, 0)));
            }
        }

        return result;
    }

    private static string CacheKey(string mailbox, DateTimeOffset from, DateTimeOffset to) =>
        $"graph-busy|{mailbox.ToLowerInvariant()}|{from.UtcTicks}|{to.UtcTicks}";

    private async Task<Dictionary<string, MailboxBusy>> Fetch(
        string[] mailboxes, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var results = new Dictionary<string, MailboxBusy>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Options.TimeoutSeconds));

            var token = await GetToken(timeout.Token);
            if (token is null)
                return results;

            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"users/{Uri.EscapeDataString(Options.OrganizerMailbox)}/calendar/getSchedule")
            {
                Content = JsonContent.Create(new
                {
                    schedules = mailboxes,
                    startTime = new { dateTime = Format(from), timeZone = "UTC" },
                    endTime = new { dateTime = Format(to), timeZone = "UTC" },
                    availabilityViewInterval = 30,
                }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("Prefer", "outlook.timezone=\"UTC\"");

            using var response = await httpClientFactory.CreateClient(ApiClientName).SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    InvalidateToken();
                logger.LogWarning(
                    "Graph getSchedule başarısız: {Status} {Body}",
                    (int)response.StatusCode, await Truncated(response, timeout.Token));
                return results;
            }

            var body = await response.Content.ReadFromJsonAsync<ScheduleResponse>(timeout.Token);
            foreach (var schedule in body?.Value ?? [])
            {
                if (schedule.ScheduleId is null)
                    continue;
                results[schedule.ScheduleId] = Map(schedule);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
                throw;
            logger.LogWarning(ex, "Graph takvimine ulaşılamadı.");
        }

        return results;
    }

    private MailboxBusy Map(ScheduleInfo schedule)
    {
        if (schedule.Error is { } error)
        {
            if (string.Equals(error.ResponseCode, "ErrorMailRecipientNotFound", StringComparison.OrdinalIgnoreCase))
                return MailboxBusy.Of(OutlookStatus.NoMailbox);

            logger.LogWarning("Graph {Mailbox} için hata döndü: {Code}", schedule.ScheduleId, error.ResponseCode);
            return MailboxBusy.Of(OutlookStatus.Unavailable);
        }

        var slots = new List<BusySlot>();
        foreach (var item in schedule.ScheduleItems ?? [])
        {
            if (item.Status is null || !BusyStatuses.Contains(item.Status) || item.Start is null || item.End is null)
                continue;
            if (TryParseUtc(item.Start.DateTime, out var start) && TryParseUtc(item.End.DateTime, out var end) && end > start)
                slots.Add(new BusySlot(start, end));
        }

        return new MailboxBusy(OutlookStatus.Ok, [.. slots.OrderBy(s => s.StartsAt)]);
    }

    private static bool TryParseUtc(string? value, out DateTimeOffset result)
    {
        result = default;
        if (!DateTime.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return false;

        result = new DateTimeOffset(parsed, TimeSpan.Zero);
        return true;
    }

    private static string Format(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    private static async Task<string> Truncated(HttpResponseMessage response, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        return text.Length <= 300 ? text : text[..300];
    }

    private void InvalidateToken() => _token = null;

    private async Task<string?> GetToken(CancellationToken ct)
    {
        if (_token is not null && clock.GetUtcNow() < _tokenExpiresAt)
            return _token;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_token is not null && clock.GetUtcNow() < _tokenExpiresAt)
                return _token;

            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"{Uri.EscapeDataString(Options.TenantId)}/oauth2/v2.0/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = Options.ClientId,
                    ["client_secret"] = Options.ClientSecret,
                    ["scope"] = "https://graph.microsoft.com/.default",
                    ["grant_type"] = "client_credentials",
                }),
            };

            using var response = await httpClientFactory.CreateClient(TokenClientName).SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                // Gövde loglanmaz: hata ayrıntısı yeterli, sır sızmasın.
                logger.LogError("Graph erişim anahtarı alınamadı: {Status}. Graph ayarlarını kontrol edin.", (int)response.StatusCode);
                return null;
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
            if (string.IsNullOrEmpty(token?.AccessToken))
                return null;

            _token = token.AccessToken;
            _tokenExpiresAt = clock.GetUtcNow().AddSeconds(Math.Max(token.ExpiresIn - 60, 30));
            return _token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record ScheduleResponse(
        [property: JsonPropertyName("value")] List<ScheduleInfo>? Value);

    private sealed record ScheduleInfo(
        [property: JsonPropertyName("scheduleId")] string? ScheduleId,
        [property: JsonPropertyName("scheduleItems")] List<ScheduleItem>? ScheduleItems,
        [property: JsonPropertyName("error")] ScheduleError? Error);

    private sealed record ScheduleItem(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("start")] GraphDateTime? Start,
        [property: JsonPropertyName("end")] GraphDateTime? End);

    private sealed record GraphDateTime([property: JsonPropertyName("dateTime")] string? DateTime);

    private sealed record ScheduleError([property: JsonPropertyName("responseCode")] string? ResponseCode);
}
