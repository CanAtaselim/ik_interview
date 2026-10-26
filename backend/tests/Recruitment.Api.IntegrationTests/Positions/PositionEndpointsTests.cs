using System.Net;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Positions;

public sealed class PositionEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/v1/positions";
    private const string Engineering = "Yazılım";

    private static async Task<Guid> CreatePosition(ApiUser hr, Guid? responsibleHrUserId = null) =>
        await hr.Create(Url, new
        {
            title = "Ürün Yöneticisi",
            departmentId = await hr.DictionaryItemId("Department", Engineering),
            levelId = await hr.DictionaryItemId("Level", "L2"),
            headcount = 1,
            jobDescription = "Ürün yol haritasını yönetir.",
            responsibleHrUserId,
        });

    [Fact]
    public async Task Create_ByHr_OpensPositionWithoutHiringRequest()
    {
        var hr = await factory.LoginAs(Engineering, Role.HrSpecialist);

        var id = await CreatePosition(hr, hr.Id);

        var position = await hr.GetJson($"{Url}/{id}");
        position.GetProperty("status").GetString().ShouldBe("Open");
        position.GetProperty("levelName").GetString().ShouldBe("L2");
        position.GetProperty("responsibleHrName").GetString().ShouldBe("Test Kullanıcısı");
        position.GetProperty("canManage").GetBoolean().ShouldBeTrue();
        (await hr.GetJson(Url)).Ids().ShouldContain(id);
    }

    [Fact]
    public async Task Create_ByNonHr_Returns403()
    {
        var teamLead = await factory.LoginAs(Engineering, Role.TeamLead);

        var response = await teamLead.Post(Url, new { title = "Ürün Yöneticisi", headcount = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_WithNonHrResponsible_Returns400()
    {
        var hr = await factory.LoginAs(Engineering, Role.HrSpecialist);
        var teamLead = await factory.LoginAs(Engineering, Role.TeamLead);

        var response = await hr.Post(Url, new
        {
            title = "Ürün Yöneticisi",
            departmentId = await hr.DictionaryItemId("Department", Engineering),
            levelId = await hr.DictionaryItemId("Level", "L2"),
            headcount = 1,
            responsibleHrUserId = teamLead.Id,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Json()).GetProperty("errors").GetProperty("responsibleHrUserId")[0].GetString()
            .ShouldBe("Sorumlu olarak bir İK çalışanı seçmelisiniz.");
    }

    [Fact]
    public async Task Position_IsVisibleToOwnDepartmentButNotToOthers()
    {
        var hr = await factory.LoginAs("İnsan Kaynakları", Role.HrSpecialist);
        var sameDepartment = await factory.LoginAs(Engineering, Role.TeamLead);
        var otherDepartment = await factory.LoginAs("Finans", Role.TeamLead);
        var id = await CreatePosition(hr);

        var seen = await sameDepartment.GetJson($"{Url}/{id}");

        seen.GetProperty("canManage").GetBoolean().ShouldBeFalse();
        (await sameDepartment.GetJson(Url)).Ids().ShouldContain(id);
        (await otherDepartment.GetJson(Url)).Ids().ShouldNotContain(id);
        (await otherDepartment.Client.GetAsync($"{Url}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ByHr_ChangesDetails_ButTeamLeadCannot()
    {
        var hr = await factory.LoginAs(Engineering, Role.HrManager);
        var teamLead = await factory.LoginAs(Engineering, Role.TeamLead);
        var id = await CreatePosition(hr);
        var body = new { title = "Kıdemli Ürün Yöneticisi", headcount = 2, jobDescription = (string?)null, responsibleHrUserId = hr.Id };

        (await teamLead.Put($"{Url}/{id}", body)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await hr.Put($"{Url}/{id}", body)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var position = await hr.GetJson($"{Url}/{id}");
        position.GetProperty("title").GetString().ShouldBe("Kıdemli Ürün Yöneticisi");
        position.GetProperty("headcount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task ChangeStatus_FollowsAllowedTransitions()
    {
        var hr = await factory.LoginAs(Engineering, Role.HrSpecialist);
        var id = await CreatePosition(hr);

        (await hr.Post($"{Url}/{id}/status", new { status = "OnHold" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await hr.Post($"{Url}/{id}/status", new { status = "Open" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await hr.Post($"{Url}/{id}/status", new { status = "Closed" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var invalid = await hr.Post($"{Url}/{id}/status", new { status = "OnHold" });

        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await invalid.Json()).GetProperty("detail").GetString()
            .ShouldBe("Yalnızca açık pozisyon beklemeye alınabilir.");
        (await hr.GetJson($"{Url}?status=Closed")).Ids().ShouldContain(id);
    }
}
