using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Infrastructure;

internal static class ApiTestExtensions
{
    public static async Task<JsonElement> GetJson(this ApiUser user, string url)
    {
        var response = await user.Client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {url}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static Task<HttpResponseMessage> Post(this ApiUser user, string url, object? body = null) =>
        user.Client.PostAsJsonAsync(url, body ?? new { });

    public static Task<HttpResponseMessage> Delete(this ApiUser user, string url) => user.Client.DeleteAsync(url);

    public static Task<HttpResponseMessage> Put(this ApiUser user, string url, object body) =>
        user.Client.PutAsJsonAsync(url, body);

    /// <summary>201 bekler ve oluşturulan kaydın kimliğini döner.</summary>
    public static async Task<Guid> Create(this ApiUser user, string url, object body)
    {
        var response = await user.Post(url, body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public static async Task<JsonElement> Json(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<Guid> DictionaryItemId(this ApiUser user, string type, string name)
    {
        var items = await user.GetJson($"/api/v1/dictionaries/{type}/items");
        return items.EnumerateArray().Single(i => i.GetProperty("name").GetString() == name).GetProperty("id").GetGuid();
    }

    /// <summary>Adayı takım liderine önerir ve oluşan değerlendirmenin kimliğini döner.</summary>
    public static async Task<Guid> SuggestCv(
        this ApiUser hr, Guid candidateId, ApiUser reviewer, Guid? applicationId = null, string? note = null)
    {
        var response = await hr.Post(
            "/api/v1/cv-reviews", new { candidateId, applicationId, reviewerUserIds = new[] { reviewer.Id }, note });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Json()).GetProperty("reviewIds")[0].GetGuid();
    }

    public static Task<HttpResponseMessage> DecideCv(
        this ApiUser reviewer, Guid reviewId, string decision, string? reason = "Değerlendirildi.", Guid? rejectionReasonId = null) =>
        reviewer.Post($"/api/v1/cv-reviews/{reviewId}/decide", new { decision, reason, rejectionReasonId });

    /// <summary>Test kullanıcısının dizindeki e-posta adresi.</summary>
    public static string Email(this ApiUser user) => $"{user.Username}@test.local";

    /// <summary>Kullanıcının bildirim başlıkları (en yeni başta).</summary>
    public static async Task<List<string>> NotificationTitles(this ApiUser user) =>
        [.. (await user.GetJson("/api/v1/me/notifications?pageSize=100")).GetProperty("items").EnumerateArray()
            .Select(n => n.GetProperty("title").GetString()!)];

    public static IEnumerable<Guid> Ids(this JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid());
}
