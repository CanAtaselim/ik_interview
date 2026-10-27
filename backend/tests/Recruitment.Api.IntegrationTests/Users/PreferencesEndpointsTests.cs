using System.Net;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Users;

public sealed class PreferencesEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/v1/me/preferences/dashboard.layout";

    [Fact]
    public async Task Preference_IsSavedPerUser_AndCanBeReset()
    {
        var first = await factory.LoginAs("Yazılım", Role.HrManager);
        var second = await factory.LoginAs("Yazılım", Role.HrManager);

        (await first.GetJson(Url)).GetProperty("value").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);

        (await first.Put(Url, new { value = new { sections = new[] { new { id = "pipeline", cards = new[] { "b", "a" } } } } }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await first.Put(Url, new { value = new { sections = new[] { new { id = "pipeline", cards = new[] { "a", "b" } } } } }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var saved = await first.GetJson(Url);
        saved.GetProperty("value").GetProperty("sections")[0].GetProperty("cards")[0].GetString().ShouldBe("a");
        (await second.GetJson(Url)).GetProperty("value").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);

        (await first.Delete(Url)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await first.GetJson(Url)).GetProperty("value").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Preference_RejectsUnknownKeyAndNonObjectValue()
    {
        var user = await factory.LoginAs("Yazılım", Role.TeamLead);

        (await user.Client.GetAsync("/api/v1/me/preferences/bilinmeyen")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await user.Put("/api/v1/me/preferences/bilinmeyen", new { value = new { } })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await user.Put(Url, new { value = new[] { 1, 2 } })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
