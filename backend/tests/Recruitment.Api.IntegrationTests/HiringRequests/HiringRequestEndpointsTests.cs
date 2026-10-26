using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.HiringRequests;
using Recruitment.Domain.Users;
using Recruitment.Infrastructure.Persistence;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.HiringRequests;

public sealed class HiringRequestEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/v1/hiring-requests";
    private const string Engineering = "Yazılım";
    private const string Finance = "Finans";

    private async Task<object> RequestBody(ApiUser user, string department = Engineering, string title = "Backend Geliştirici") =>
        new
        {
            title = await factory.PositionTitle(title),
            departmentId = await user.DictionaryItemId("Department", department),
            levelId = await user.DictionaryItemId("Level", "L3"),
            headcount = 2,
            reason = "NewHeadcount",
            urgency = "High",
            expectedStartDate = "2026-12-01",
            competencies = new[] { "C#", "PostgreSQL" },
            description = "Ödeme ekibi için.",
        };

    private async Task<Guid> CreateDraft(ApiUser owner, string department = Engineering, string title = "Backend Geliştirici") =>
        await owner.Create(Url, await RequestBody(owner, department, title));

    private async Task<Guid> CreateSubmitted(ApiUser owner, string department = Engineering, string title = "Backend Geliştirici")
    {
        var id = await CreateDraft(owner, department, title);
        (await owner.Post($"{Url}/{id}/submit")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        return id;
    }

    [Fact]
    public async Task Flow_WhenAllStepsApprove_ApprovesRequestAndOpensPosition()
    {
        var teamLead = await factory.LoginAs(Engineering, Role.TeamLead);
        var departmentManager = await factory.LoginAs(Engineering, Role.DepartmentManager);
        var hrManager = await factory.LoginAs("İnsan Kaynakları", Role.HrManager);
        var id = await CreateSubmitted(teamLead);

        (await departmentManager.Post($"{Url}/{id}/approve", new { note = "Bütçe uygun." }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await teamLead.GetJson($"{Url}/{id}")).GetProperty("awaitingRole").GetString().ShouldBe("HrManager");

        (await hrManager.Post($"{Url}/{id}/approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var approved = await teamLead.GetJson($"{Url}/{id}");
        approved.GetProperty("status").GetString().ShouldBe("Approved");
        approved.GetProperty("positionId").ValueKind.ShouldBe(JsonValueKind.Null);
        var steps = approved.GetProperty("steps").EnumerateArray().ToList();
        steps.Select(s => s.GetProperty("decision").GetString()).ShouldBe(["Approved", "Approved"]);
        steps[0].GetProperty("note").GetString().ShouldBe("Bütçe uygun.");
        steps[0].GetProperty("decidedByName").GetString().ShouldBe("Test Kullanıcısı");

        await factory.ProcessOutbox();

        var positionId = (await teamLead.GetJson($"{Url}/{id}")).GetProperty("positionId").GetGuid();
        var position = await hrManager.GetJson($"/api/v1/positions/{positionId}");
        position.GetProperty("title").GetString().ShouldBe("Backend Geliştirici");
        position.GetProperty("headcount").GetInt32().ShouldBe(2);
        position.GetProperty("status").GetString().ShouldBe("Open");
        position.GetProperty("departmentName").GetString().ShouldBe(Engineering);
        position.GetProperty("hiringRequestId").GetGuid().ShouldBe(id);
    }

    [Fact]
    public async Task Create_WithInvalidInput_Returns400WithFieldErrors()
    {
        var teamLead = await factory.LoginAs(Engineering, Role.TeamLead);

        var response = await teamLead.Post(Url, new
        {
            title = "",
            departmentId = Guid.Empty,
            levelId = Guid.NewGuid(),
            headcount = 0,
            reason = "Replacement",
            urgency = "Normal",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Json()).GetProperty("errors");
        errors.GetProperty("title")[0].GetString().ShouldBe("Pozisyon adını seçmelisiniz.");
        errors.GetProperty("expectedStartDate")[0].GetString().ShouldBe("Beklenen başlangıç tarihini seçmelisiniz.");
        errors.GetProperty("description")[0].GetString().ShouldBe("Açıklamayı yazmalısınız.");
        errors.GetProperty("competencies")[0].GetString().ShouldBe("En az bir yetkinlik eklemelisiniz.");
        errors.GetProperty("departmentId")[0].GetString().ShouldBe("Birim seçmelisiniz.");
        errors.GetProperty("headcount")[0].GetString().ShouldBe("Kişi sayısı 1 ile 1000 arasında olmalı.");
        errors.GetProperty("replacedEmployeeName")[0].GetString()
            .ShouldBe("Yerine alım yapılacak çalışanın adını yazmalısınız.");
    }

    [Fact]
    public async Task Create_WithUnknownLevel_Returns400ForThatField()
    {
        var teamLead = await factory.LoginAs(Engineering, Role.TeamLead);
        var departmentId = await teamLead.DictionaryItemId("Department", Engineering);

        var response = await teamLead.Post(Url, new
        {
            title = await factory.PositionTitle("Backend Geliştirici"), departmentId, levelId = departmentId, headcount = 1,
            reason = "NewHeadcount", urgency = "Normal", expectedStartDate = "2026-12-01",
            competencies = new[] { "C#" }, description = "Açıklama.",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Json()).GetProperty("errors").GetProperty("levelId")[0].GetString()
            .ShouldBe("Geçerli bir seviye seçmelisiniz.");
    }

    [Fact]
    public async Task Create_WithTitleOutsideTheList_Returns400ForThatField()
    {
        var teamLead = await factory.LoginAs(Engineering, Role.TeamLead);
        var body = await RequestBody(teamLead, title: "Listede Olmayan Unvan");
        // RequestBody adı listeye ekler; listede olmayan adı elle yazıyoruz.
        var departmentId = await teamLead.DictionaryItemId("Department", Engineering);

        var response = await teamLead.Post(Url, new
        {
            title = $"Serbest Metin {Guid.NewGuid():N}", departmentId,
            levelId = await teamLead.DictionaryItemId("Level", "L3"), headcount = 1,
            reason = "NewHeadcount", urgency = "Normal", expectedStartDate = "2026-12-01",
            competencies = new[] { "C#" }, description = "Açıklama.",
        });

        body.ShouldNotBeNull();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Json()).GetProperty("errors").GetProperty("title")[0].GetString()
            .ShouldBe("Listeden geçerli bir pozisyon adı seçmelisiniz.");
    }

    [Fact]
    public async Task Create_WithDeactivatedTitle_Returns400()
    {
        var teamLead = await factory.LoginAs(Engineering, Role.TeamLead);
        var manager = await factory.LoginAs("İnsan Kaynakları", Role.HrManager);
        var name = $"Kaldırılan Unvan {Guid.NewGuid():N}";
        var itemId = await manager.Create("/api/v1/dictionaries/PositionTitle/items", new { name });
        (await manager.Put($"/api/v1/dictionary-items/{itemId}", new { name, sortOrder = 1, isActive = false }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await teamLead.Post(Url, new
        {
            title = name, departmentId = await teamLead.DictionaryItemId("Department", Engineering),
            levelId = await teamLead.DictionaryItemId("Level", "L3"), headcount = 1,
            reason = "NewHeadcount", urgency = "Normal", expectedStartDate = "2026-12-01",
            competencies = new[] { "C#" }, description = "Açıklama.",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Json()).GetProperty("errors").GetProperty("title")[0].GetString()
            .ShouldBe("Listeden geçerli bir pozisyon adı seçmelisiniz.");
    }

    [Fact]
    public async Task Create_WithOnlyBlankCompetenciesAndWhitespaceDescription_Returns400()
    {
        var teamLead = await factory.LoginAs(Engineering, Role.TeamLead);

        var response = await teamLead.Post(Url, new
        {
            title = await factory.PositionTitle("Backend Geliştirici"),
            departmentId = await teamLead.DictionaryItemId("Department", Engineering),
            levelId = await teamLead.DictionaryItemId("Level", "L3"), headcount = 1,
            reason = "NewHeadcount", urgency = "Normal", expectedStartDate = "2026-12-01",
            competencies = new[] { "  " }, description = "   ",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Json()).GetProperty("errors");
        errors.GetProperty("description")[0].GetString().ShouldBe("Açıklamayı yazmalısınız.");
        errors.GetProperty("competencies")[0].GetString().ShouldBe("En az bir yetkinlik eklemelisiniz.");
    }

    [Fact]
    public async Task Submit_OfLegacyIncompleteDraft_Returns422UntilCompleted()
    {
        var teamLead = await factory.LoginAs(Engineering, Role.TeamLead);
        var departmentId = await teamLead.DictionaryItemId("Department", Engineering);
        var levelId = await teamLead.DictionaryItemId("Level", "L3");

        // Eski sürümde açılmış, açıklaması ve yetkinliği olmayan bir taslak.
        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var legacy = HiringRequest.Create(
                new HiringRequestDetails(
                    "Eski Taslak", departmentId, levelId, 1, HiringReason.NewHeadcount, null, Urgency.Normal, null, [], null),
                teamLead.Id);
            db.HiringRequests.Add(legacy);
            await db.SaveChangesAsync();
            id = legacy.Id;
        }

        var response = await teamLead.Post($"{Url}/{id}/submit");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Json()).GetProperty("code").GetString().ShouldBe("HiringRequest.Incomplete");

        // Tüm alanlar doldurulup kaydedilince gönderilebilir.
        (await teamLead.Put($"{Url}/{id}", await RequestBody(teamLead, title: "Backend Geliştirici")))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await teamLead.Post($"{Url}/{id}/submit")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Create_ByRoleThatCannotRequestHeadcount_Returns403()
    {
        var hrSpecialist = await factory.LoginAs(Engineering, Role.HrSpecialist);

        var response = await hrSpecialist.Post(Url, await RequestBody(hrSpecialist));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_ByRoleOutsideTheProcess_Returns403()
    {
        var executive = await factory.LoginAs(Engineering, Role.Executive);

        (await executive.Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Draft_IsVisibleOnlyToItsOwner()
    {
        var owner = await factory.LoginAs(Engineering, Role.TeamLead);
        var departmentManager = await factory.LoginAs(Engineering, Role.DepartmentManager);
        var hrManager = await factory.LoginAs(Engineering, Role.HrManager);
        var id = await CreateDraft(owner);

        (await owner.GetJson(Url)).Ids().ShouldContain(id);
        (await departmentManager.GetJson(Url)).Ids().ShouldNotContain(id);
        (await hrManager.GetJson(Url)).Ids().ShouldNotContain(id);
        (await hrManager.Client.GetAsync($"{Url}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmittedRequest_IsHiddenFromOtherTeamLeadsAndOtherDepartments()
    {
        var owner = await factory.LoginAs(Engineering, Role.TeamLead);
        var otherTeamLead = await factory.LoginAs(Engineering, Role.TeamLead);
        var otherDepartmentManager = await factory.LoginAs(Finance, Role.DepartmentManager);
        var hrSpecialist = await factory.LoginAs(Finance, Role.HrSpecialist);
        var id = await CreateSubmitted(owner);

        (await otherTeamLead.GetJson(Url)).Ids().ShouldNotContain(id);
        (await otherTeamLead.Client.GetAsync($"{Url}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await otherDepartmentManager.GetJson(Url)).Ids().ShouldNotContain(id);
        (await otherDepartmentManager.Client.GetAsync($"{Url}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await hrSpecialist.GetJson(Url)).Ids().ShouldContain(id);
    }

    [Fact]
    public async Task Approve_ByManagerOfAnotherDepartment_Returns403()
    {
        var owner = await factory.LoginAs(Engineering, Role.TeamLead);
        var otherDepartmentManager = await factory.LoginAs(Finance, Role.DepartmentManager);
        var id = await CreateSubmitted(owner);

        var response = await otherDepartmentManager.Post($"{Url}/{id}/approve");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await owner.GetJson($"{Url}/{id}")).GetProperty("awaitingRole").GetString().ShouldBe("DepartmentManager");
    }

    [Fact]
    public async Task Approve_ByHrManagerBeforeDepartmentManager_Returns403()
    {
        var owner = await factory.LoginAs(Engineering, Role.TeamLead);
        var hrManager = await factory.LoginAs(Engineering, Role.HrManager);
        var id = await CreateSubmitted(owner);

        (await hrManager.Post($"{Url}/{id}/approve")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await hrManager.GetJson($"{Url}/{id}")).GetProperty("permissions").GetProperty("canDecide")
            .GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Approve_ByTeamLead_Returns403()
    {
        var owner = await factory.LoginAs(Engineering, Role.TeamLead);
        var id = await CreateSubmitted(owner);

        (await owner.Post($"{Url}/{id}/approve")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reject_WithoutReason_Returns400AndKeepsRequestPending()
    {
        var owner = await factory.LoginAs(Engineering, Role.TeamLead);
        var departmentManager = await factory.LoginAs(Engineering, Role.DepartmentManager);
        var id = await CreateSubmitted(owner);

        var response = await departmentManager.Post($"{Url}/{id}/reject", new { reason = " " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Json()).GetProperty("errors").GetProperty("reason")[0].GetString()
            .ShouldBe("Ret gerekçesi yazmalısınız.");
        (await owner.GetJson($"{Url}/{id}")).GetProperty("status").GetString().ShouldBe("PendingApproval");
    }

    [Fact]
    public async Task Reject_WithReason_RejectsRequestAndNoPositionIsOpened()
    {
        var owner = await factory.LoginAs(Engineering, Role.TeamLead);
        var departmentManager = await factory.LoginAs(Engineering, Role.DepartmentManager);
        var id = await CreateSubmitted(owner);

        (await departmentManager.Post($"{Url}/{id}/reject", new { reason = "Bu çeyrekte bütçe yok." }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await factory.ProcessOutbox();

        var detail = await owner.GetJson($"{Url}/{id}");
        detail.GetProperty("status").GetString().ShouldBe("Rejected");
        detail.GetProperty("steps")[0].GetProperty("note").GetString().ShouldBe("Bu çeyrekte bütçe yok.");
        detail.GetProperty("positionId").ValueKind.ShouldBe(JsonValueKind.Null);
        (await departmentManager.Post($"{Url}/{id}/approve")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_OwnDraft_ChangesIt_ButSubmittedRequestIsLocked()
    {
        var owner = await factory.LoginAs(Engineering, Role.TeamLead);
        var id = await CreateDraft(owner);

        (await owner.Put($"{Url}/{id}", await RequestBody(owner, title: "Kıdemli Backend Geliştirici")))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await owner.GetJson($"{Url}/{id}")).GetProperty("title").GetString().ShouldBe("Kıdemli Backend Geliştirici");

        await owner.Post($"{Url}/{id}/submit");
        var locked = await owner.Put($"{Url}/{id}", await RequestBody(owner, title: "Başka ad"));

        locked.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await locked.Json()).GetProperty("detail").GetString()
            .ShouldBe("Yalnızca taslak durumundaki talep düzenlenebilir.");
    }

    [Fact]
    public async Task UpdateSubmitCancel_BySomeoneElse_Returns403()
    {
        var owner = await factory.LoginAs(Engineering, Role.TeamLead);
        var departmentManager = await factory.LoginAs(Engineering, Role.DepartmentManager);
        var id = await CreateSubmitted(owner);
        var draftId = await CreateDraft(owner);

        (await departmentManager.Put($"{Url}/{draftId}", await RequestBody(departmentManager)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await departmentManager.Post($"{Url}/{draftId}/submit")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await departmentManager.Post($"{Url}/{id}/cancel")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cancel_AfterAnApproverActed_IsRejected_AndRejectionStaysVisible()
    {
        var owner = await factory.LoginAs(Engineering, Role.TeamLead);
        var manager = await factory.LoginAs(Engineering, Role.DepartmentManager);
        var hrManager = await factory.LoginAs("İnsan Kaynakları", Role.HrManager);
        var untouched = await CreateSubmitted(owner);
        var approvedOnce = await CreateSubmitted(owner);
        var rejected = await CreateSubmitted(owner);

        // Kimse işlem yapmadıysa sahibi iptal edebilir ve düğme görünür.
        (await owner.GetJson($"{Url}/{untouched}")).GetProperty("permissions").GetProperty("canCancel").GetBoolean().ShouldBeTrue();

        (await manager.Post($"{Url}/{approvedOnce}/approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await manager.Post($"{Url}/{rejected}/reject", new { reason = "Bütçe yok." })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        foreach (var id in new[] { approvedOnce, rejected })
        {
            (await owner.GetJson($"{Url}/{id}")).GetProperty("permissions").GetProperty("canCancel").GetBoolean().ShouldBeFalse();
            (await hrManager.GetJson($"{Url}/{id}")).GetProperty("permissions").GetProperty("canCancel").GetBoolean().ShouldBeFalse();
            (await owner.Post($"{Url}/{id}/cancel")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await hrManager.Post($"{Url}/{id}/cancel")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        }

        (await owner.GetJson($"{Url}/{approvedOnce}")).GetProperty("status").GetString().ShouldBe("PendingApproval");
        var afterAttempt = await owner.GetJson($"{Url}/{rejected}");
        afterAttempt.GetProperty("status").GetString().ShouldBe("Rejected");
        afterAttempt.GetProperty("steps")[0].GetProperty("note").GetString().ShouldBe("Bütçe yok.");

        (await owner.Post($"{Url}/{untouched}/cancel")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Cancel_ByOwnerOrHrManager_CancelsRequest()
    {
        var owner = await factory.LoginAs(Engineering, Role.TeamLead);
        var hrManager = await factory.LoginAs(Engineering, Role.HrManager);
        var first = await CreateSubmitted(owner);
        var second = await CreateSubmitted(owner);

        (await owner.Post($"{Url}/{first}/cancel")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await hrManager.Post($"{Url}/{second}/cancel")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await owner.GetJson($"{Url}/{first}")).GetProperty("status").GetString().ShouldBe("Cancelled");
        (await owner.GetJson($"{Url}/{second}")).GetProperty("status").GetString().ShouldBe("Cancelled");
        (await owner.Post($"{Url}/{first}/cancel")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task List_AwaitingMyDecision_ReturnsOnlyRequestsWaitingForThatUser()
    {
        var owner = await factory.LoginAs(Engineering, Role.TeamLead);
        var financeOwner = await factory.LoginAs(Finance, Role.TeamLead);
        var departmentManager = await factory.LoginAs(Engineering, Role.DepartmentManager);
        var hrManager = await factory.LoginAs(Engineering, Role.HrManager);
        var waitingForManager = await CreateSubmitted(owner, title: "Bekleyen");
        var waitingForHr = await CreateSubmitted(owner, title: "İK'da");
        var otherDepartment = await CreateSubmitted(financeOwner, Finance);
        await departmentManager.Post($"{Url}/{waitingForHr}/approve");

        var forManager = (await departmentManager.GetJson($"{Url}?awaitingMyDecision=true")).Ids().ToList();
        var forHr = (await hrManager.GetJson($"{Url}?awaitingMyDecision=true")).Ids().ToList();

        forManager.ShouldContain(waitingForManager);
        forManager.ShouldNotContain(waitingForHr);
        forManager.ShouldNotContain(otherDepartment);
        forHr.ShouldContain(waitingForHr);
        forHr.ShouldNotContain(waitingForManager);
        forHr.ShouldNotContain(otherDepartment);
    }

    [Fact]
    public async Task List_WithStatusSearchAndPaging_FiltersOnServer()
    {
        var owner = await factory.LoginAs(Engineering, Role.TeamLead);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var draft = await CreateDraft(owner, title: $"Veri Analisti {marker}");
        var submitted = await CreateSubmitted(owner, title: $"Veri Mühendisi {marker}");

        var bySearch = await owner.GetJson($"{Url}?search={marker.ToUpperInvariant()}");
        var byStatus = await owner.GetJson($"{Url}?search={marker}&status=Draft");
        var paged = await owner.GetJson($"{Url}?search={marker}&page=2&pageSize=1");

        bySearch.Ids().ShouldBe([submitted, draft]);
        bySearch.GetProperty("totalCount").GetInt32().ShouldBe(2);
        byStatus.Ids().ShouldBe([draft]);
        paged.Ids().ShouldBe([draft]);
        paged.GetProperty("totalCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Outbox_ProcessedTwice_OpensSinglePosition()
    {
        var owner = await factory.LoginAs(Engineering, Role.TeamLead);
        var departmentManager = await factory.LoginAs(Engineering, Role.DepartmentManager);
        var hrManager = await factory.LoginAs(Engineering, Role.HrManager);
        var title = $"Tekil Pozisyon {Guid.NewGuid():N}";
        var id = await CreateSubmitted(owner, title: title);
        await departmentManager.Post($"{Url}/{id}/approve");
        await hrManager.Post($"{Url}/{id}/approve");

        await factory.ProcessOutbox();
        (await factory.ProcessOutbox()).ShouldBe(0);

        (await hrManager.GetJson($"/api/v1/positions?search={Uri.EscapeDataString(title)}"))
            .GetProperty("totalCount").GetInt32().ShouldBe(1);
    }
}
