using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Recruitment.Application.Abstractions;
using Recruitment.Infrastructure.Calendar;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Interviews;

/// <summary>Graph yanıtlarının okunması; gerçek Microsoft'a gitmeden sahte HTTP işleyicisiyle.</summary>
public sealed class GraphCalendarServiceTests
{
    private static readonly DateTimeOffset From = new(2030, 3, 4, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = From.AddDays(1);

    private static GraphOptions Configured(int cacheSeconds = 120) => new()
    {
        TenantId = "tenant",
        ClientId = "client",
        ClientSecret = "gizli",
        OrganizerMailbox = "ik@sirket.com",
        CacheSeconds = cacheSeconds,
    };

    private static (GraphCalendarService Service, StubGraph Graph) Create(GraphOptions options, StubGraph? graph = null)
    {
        graph ??= new StubGraph();
        var service = new GraphCalendarService(
            new StubFactory(graph), Options.Create(options), new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System, NullLogger<GraphCalendarService>.Instance);
        return (service, graph);
    }

    private static string Item(string status, string start, string end) =>
        $$$"""{"status":"{{{status}}}","start":{"dateTime":"{{{start}}}","timeZone":"UTC"},"end":{"dateTime":"{{{end}}}","timeZone":"UTC"}}""";

    private static string Schedule(string id, params string[] items) =>
        $$$"""{"scheduleId":"{{{id}}}","scheduleItems":[{{{string.Join(',', items)}}}]}""";

    [Fact]
    public async Task NotConfigured_ReturnsStatusWithoutCallingGraph()
    {
        var (service, graph) = Create(new GraphOptions());

        service.IsEnabled.ShouldBeFalse();
        var result = await service.GetBusy(["a@sirket.com"], From, To, default);

        result["a@sirket.com"].Status.ShouldBe(OutlookStatus.NotConfigured);
        graph.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadsBusyTentativeAndOof_ButIgnoresFreeAndWorkingElsewhere()
    {
        var graph = new StubGraph
        {
            Schedules = $$"""{"value":[{{Schedule(
                "a@sirket.com",
                Item("busy", "2030-03-04T07:00:00.0000000", "2030-03-04T08:00:00.0000000"),
                Item("free", "2030-03-04T09:00:00.0000000", "2030-03-04T10:00:00.0000000"),
                Item("workingElsewhere", "2030-03-04T10:00:00.0000000", "2030-03-04T11:00:00.0000000"),
                Item("tentative", "2030-03-04T11:00:00.0000000", "2030-03-04T12:00:00.0000000"),
                Item("oof", "2030-03-04T13:00:00.0000000", "2030-03-04T14:00:00.0000000"))}}]}""",
        };
        var (service, _) = Create(Configured(), graph);

        var result = (await service.GetBusy(["a@sirket.com"], From, To, default))["a@sirket.com"];

        result.Status.ShouldBe(OutlookStatus.Ok);
        result.Busy.Select(b => (b.StartsAt.UtcDateTime.Hour, b.EndsAt.UtcDateTime.Hour)).ShouldBe([(7, 8), (11, 12), (13, 14)]);
        result.Busy.ShouldAllBe(b => b.StartsAt.Offset == TimeSpan.Zero);
    }

    [Fact]
    public async Task SendsAppTokenAndQueriesThroughOrganizerMailbox()
    {
        var (service, graph) = Create(Configured());

        await service.GetBusy(["a@sirket.com", "b@sirket.com"], From, To, default);

        var token = graph.Requests.First(r => r.Uri.Contains("oauth2/v2.0/token"));
        token.Body.ShouldContain("grant_type=client_credentials");
        token.Body.ShouldContain("graph.microsoft.com%2F.default");

        var call = graph.Requests.Single(r => r.Uri.Contains("calendar/getSchedule"));
        call.Uri.ShouldContain("users/ik%40sirket.com/calendar/getSchedule");
        call.Authorization.ShouldBe("Bearer jeton-1");
        call.Prefer.ShouldBe("outlook.timezone=\"UTC\"");
        call.Body.ShouldContain("a@sirket.com");
        call.Body.ShouldContain("b@sirket.com");
        call.Body.ShouldContain("2030-03-04T00:00:00");
    }

    [Fact]
    public async Task MapsMissingMailbox_AndPerMailboxErrors()
    {
        var graph = new StubGraph
        {
            Schedules = """
                {"value":[
                  {"scheduleId":"yok@sirket.com","error":{"responseCode":"ErrorMailRecipientNotFound","message":"x"}},
                  {"scheduleId":"bozuk@sirket.com","error":{"responseCode":"ErrorInternalServerError","message":"x"}}
                ]}
                """,
        };
        var (service, _) = Create(Configured(), graph);

        var result = await service.GetBusy(["yok@sirket.com", "bozuk@sirket.com", "eksik@sirket.com"], From, To, default);

        result["yok@sirket.com"].Status.ShouldBe(OutlookStatus.NoMailbox);
        result["bozuk@sirket.com"].Status.ShouldBe(OutlookStatus.Unavailable);
        result["eksik@sirket.com"].Status.ShouldBe(OutlookStatus.Unavailable);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GraphFailure_IsUnavailable_NotAnException(HttpStatusCode status)
    {
        var (service, _) = Create(Configured(), new StubGraph { ScheduleStatus = status });

        var result = await service.GetBusy(["a@sirket.com"], From, To, default);

        result["a@sirket.com"].Status.ShouldBe(OutlookStatus.Unavailable);
    }

    [Fact]
    public async Task TokenFailure_IsUnavailable_AndNothingIsCached()
    {
        var graph = new StubGraph { TokenStatus = HttpStatusCode.BadRequest };
        var (service, _) = Create(Configured(), graph);

        (await service.GetBusy(["a@sirket.com"], From, To, default))["a@sirket.com"].Status.ShouldBe(OutlookStatus.Unavailable);

        graph.TokenStatus = HttpStatusCode.OK;
        (await service.GetBusy(["a@sirket.com"], From, To, default))["a@sirket.com"].Status.ShouldBe(OutlookStatus.Ok);
    }

    [Fact]
    public async Task CachesResults_AndReusesTheToken()
    {
        var (service, graph) = Create(Configured());

        await service.GetBusy(["a@sirket.com"], From, To, default);
        await service.GetBusy(["A@sirket.com"], From, To, default);
        await service.GetBusy(["b@sirket.com"], From, To, default);

        graph.Requests.Count(r => r.Uri.Contains("getSchedule")).ShouldBe(2);
        graph.Requests.Count(r => r.Uri.Contains("oauth2")).ShouldBe(1);
    }

    private sealed record Recorded(string Uri, string Body, string? Authorization, string? Prefer);

    private sealed class StubGraph : HttpMessageHandler
    {
        public List<Recorded> Requests { get; } = [];
        public HttpStatusCode TokenStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode ScheduleStatus { get; set; } = HttpStatusCode.OK;
        public string Schedules { get; set; } = """{"value":[]}""";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.ToString();
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
            request.Headers.TryGetValues("Prefer", out var prefer);
            lock (Requests)
                Requests.Add(new Recorded(uri, body, request.Headers.Authorization?.ToString(), prefer?.SingleOrDefault()));

            if (uri.Contains("oauth2"))
                return Json(TokenStatus, $$"""{"access_token":"jeton-{{Requests.Count(r => r.Uri.Contains("oauth2"))}}","expires_in":3600}""");

            // Önbellek testlerinde her kişi için "boş ama tamam" yanıtı döner.
            var requested = System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("schedules").EnumerateArray().Select(e => e.GetString()!).ToList();
            var response = Schedules == """{"value":[]}"""
                ? $$"""{"value":[{{string.Join(',', requested.Select(id => Schedule(id)))}}]}"""
                : Schedules;
            return Json(ScheduleStatus, response);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string content) =>
            new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri(name == GraphCalendarService.TokenClientName
                ? "https://login.microsoftonline.com/"
                : "https://graph.microsoft.com/v1.0/"),
        };
    }
}
