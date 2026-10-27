using System.Net;
using System.Text.Json;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Dashboard;

public sealed class DashboardEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Department = "Yazılım";

    private static readonly string[] Sections =
        ["summary", "requests", "candidates", "pipeline", "cv-reviews", "interviews", "scorecards", "blacklist"];

    private Task<ApiUser> Manager() => factory.LoginAs(Department, Role.HrManager);

    private static async Task SeedProcess(ApiUser hr)
    {
        var positionId = await hr.Create("/api/v1/positions", new
        {
            title = "Dashboard Geliştirici",
            departmentId = await hr.DictionaryItemId("Department", Department),
            levelId = await hr.DictionaryItemId("Level", "L3"),
            headcount = 2,
        });
        var candidateId = await hr.Create("/api/v1/candidates", new
        {
            firstName = "Deniz",
            lastName = $"Aday{Guid.NewGuid():N}",
            email = $"{Guid.NewGuid():N}@ornek.com",
            tags = new[] { "C#", "Docker" },
        });
        await hr.Create("/api/v1/applications", new { candidateId, positionId });
    }

    [Fact]
    public async Task EverySection_ReturnsDataForHrManager()
    {
        var manager = await Manager();
        await SeedProcess(manager);

        foreach (var section in Sections)
        {
            var response = await manager.Client.GetAsync($"/api/v1/dashboard/{section}");
            response.StatusCode.ShouldBe(HttpStatusCode.OK, section);
        }

        var summary = await manager.GetJson("/api/v1/dashboard/summary");
        summary.GetProperty("openPositions").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        summary.GetProperty("activeApplications").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        summary.GetProperty("newCandidates").GetProperty("value").GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        var candidates = await manager.GetJson("/api/v1/dashboard/candidates");
        candidates.GetProperty("topTags").EnumerateArray()
            .Select(t => t.GetProperty("label").GetString())
            .ShouldContain("Docker");
        candidates.GetProperty("timeline").GetArrayLength().ShouldBeGreaterThan(0);

        var pipeline = await manager.GetJson("/api/v1/dashboard/pipeline");
        pipeline.GetProperty("funnel")[0].GetProperty("count").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        pipeline.GetProperty("stageDurations").EnumerateArray()
            .Sum(s => s.GetProperty("waiting").GetInt32()).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Filters_ByDepartmentAndPosition_ReturnOk()
    {
        var manager = await Manager();
        await SeedProcess(manager);
        var departmentId = await manager.DictionaryItemId("Department", Department);

        foreach (var section in Sections)
        {
            var response = await manager.Client.GetAsync(
                $"/api/v1/dashboard/{section}?from=2026-01-01&to=2026-12-31&departmentId={departmentId}");
            response.StatusCode.ShouldBe(HttpStatusCode.OK, section);

            var byPosition = await manager.Client.GetAsync($"/api/v1/dashboard/{section}?positionId={Guid.NewGuid()}");
            byPosition.StatusCode.ShouldBe(HttpStatusCode.OK, section);
        }
    }

    [Fact]
    public async Task InvalidRange_Returns400()
    {
        var manager = await Manager();

        (await manager.Client.GetAsync("/api/v1/dashboard/summary?from=2026-12-31&to=2026-01-01"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await manager.Client.GetAsync("/api/v1/dashboard/summary?from=2020-01-01&to=2026-01-01"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OtherRoles_AreForbidden()
    {
        foreach (var role in new[] { Role.HrSpecialist, Role.TeamLead, Role.Executive })
        {
            var user = await factory.LoginAs(Department, role);
            (await user.Client.GetAsync("/api/v1/dashboard/summary")).StatusCode.ShouldBe(HttpStatusCode.Forbidden, role.ToString());
        }
    }
}
