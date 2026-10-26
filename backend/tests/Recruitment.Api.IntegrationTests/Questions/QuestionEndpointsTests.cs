using System.Net;
using System.Text.Json;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Questions;

public sealed class QuestionEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/v1/questions";
    private const string TemplatesUrl = "/api/v1/interview-templates";
    private const string Department = "Yazılım";
    private const string OtherDepartment = "Finans";

    private Task<ApiUser> Hr() => factory.LoginAs("İnsan Kaynakları", Role.HrSpecialist);

    private Task<ApiUser> Lead(string department = Department) => factory.LoginAs(department, Role.TeamLead);

    private static async Task<object> QuestionBody(
        ApiUser user, string department = Department, string? text = null, string competency = "Teknik bilgi") => new
    {
        departmentId = await user.DictionaryItemId("Department", department),
        competencyId = await user.DictionaryItemId("Competency", competency),
        type = "Technical",
        difficulty = 2,
        weight = 1.5,
        text = text ?? $"Soru {Guid.NewGuid():N}",
        guidance = "Beklenen cevap",
    };

    private static async Task<Guid> Template(ApiUser user, Guid[] questionIds, string department = Department, string? type = null) =>
        await user.Create(TemplatesUrl, new
        {
            name = $"Şablon {Guid.NewGuid():N}",
            departmentId = await user.DictionaryItemId("Department", department),
            interviewTypeId = type is null ? (Guid?)null : await user.DictionaryItemId("InterviewType", type),
            questionIds,
        });

    [Fact]
    public async Task Create_ByTeamLead_ForOwnDepartmentOnly()
    {
        var lead = await Lead();
        await factory.LoginAs(OtherDepartment, Role.TeamLead);

        var id = await lead.Create(Url, await QuestionBody(lead, text: "Bir REST API'yi nasıl sürümlersiniz?"));
        var other = await lead.Post(Url, await QuestionBody(lead, OtherDepartment));

        other.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await other.Json()).GetProperty("detail").GetString().ShouldBe("Yalnızca kendi biriminizin soru ve şablonlarını yönetebilirsiniz.");
        var question = (await lead.GetJson($"{Url}/{id}")).GetProperty("question");
        question.GetProperty("text").GetString().ShouldBe("Bir REST API'yi nasıl sürümlersiniz?");
        question.GetProperty("competencyName").GetString().ShouldBe("Teknik bilgi");
        question.GetProperty("departmentName").GetString().ShouldBe(Department);
        question.GetProperty("weight").GetDecimal().ShouldBe(1.5m);
        question.GetProperty("version").GetInt32().ShouldBe(1);
        question.GetProperty("canManage").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Create_ValidatesFields()
    {
        var lead = await Lead();

        var response = await lead.Post(Url, new
        {
            departmentId = await lead.DictionaryItemId("Department", Department),
            competencyId = Guid.Empty,
            type = "Technical",
            difficulty = 5,
            weight = 1,
            text = " ",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Json()).GetProperty("errors");
        errors.GetProperty("text")[0].GetString().ShouldBe("Soru metnini yazmalısınız.");
        errors.GetProperty("difficulty")[0].GetString().ShouldBe("Zorluk 1 (kolay), 2 (orta) veya 3 (zor) olmalı.");
        errors.GetProperty("competencyId")[0].GetString().ShouldBe("Sorunun ölçtüğü yetkinliği seçmelisiniz.");
    }

    [Fact]
    public async Task List_TeamLeadSeesOwnDepartment_HrSeesAll()
    {
        var hr = await Hr();
        var lead = await Lead();
        var outsider = await Lead(OtherDepartment);
        var marker = Guid.NewGuid().ToString("N");
        var own = await lead.Create(Url, await QuestionBody(lead, text: $"Yazılım sorusu {marker}"));
        var foreign = await outsider.Create(Url, await QuestionBody(outsider, OtherDepartment, $"Finans sorusu {marker}"));

        (await lead.GetJson($"{Url}?search={marker}")).Ids().ShouldBe([own]);
        (await outsider.GetJson($"{Url}?search={marker}")).Ids().ShouldBe([foreign]);
        (await hr.GetJson($"{Url}?search={marker}")).Ids().ShouldBe([own, foreign], ignoreOrder: true);
        (await outsider.Client.GetAsync($"{Url}/{own}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await outsider.Put($"{Url}/{own}", await QuestionBody(outsider, OtherDepartment))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var departmentId = await hr.DictionaryItemId("Department", OtherDepartment);
        (await hr.GetJson($"{Url}?search={marker}&departmentId={departmentId}")).Ids().ShouldBe([foreign]);
    }

    [Fact]
    public async Task Update_CreatesVersionHistory()
    {
        var lead = await Lead();
        var id = await lead.Create(Url, await QuestionBody(lead, text: "İlk metin"));

        (await lead.Put($"{Url}/{id}", await QuestionBody(lead, text: "İkinci metin"))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await lead.Put($"{Url}/{id}", await QuestionBody(lead, text: "İkinci metin"))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await lead.GetJson($"{Url}/{id}");
        detail.GetProperty("question").GetProperty("version").GetInt32().ShouldBe(2);
        detail.GetProperty("versions").EnumerateArray().Select(v => v.GetProperty("text").GetString()).ShouldBe(["İkinci metin", "İlk metin"]);
        detail.GetProperty("versions")[0].GetProperty("createdByName").GetString().ShouldBe("Test Kullanıcısı");
        // Takım lideri soruyu başka birime taşıyamaz.
        (await lead.Put($"{Url}/{id}", await QuestionBody(lead, OtherDepartment))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UsedQuestion_CannotBeDeleted_ButCanBeDeactivated()
    {
        var lead = await Lead();
        var used = await lead.Create(Url, await QuestionBody(lead));
        var unused = await lead.Create(Url, await QuestionBody(lead));
        await Template(lead, [used]);

        var deleteUsed = await lead.Client.DeleteAsync($"{Url}/{used}");
        deleteUsed.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await deleteUsed.Json()).GetProperty("code").GetString().ShouldBe("Question.InUse");
        (await lead.GetJson($"{Url}/{used}")).GetProperty("question").GetProperty("templateCount").GetInt32().ShouldBe(1);

        (await lead.Post($"{Url}/{used}/active", new { isActive = false })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await lead.GetJson($"{Url}/{used}")).GetProperty("question").GetProperty("isActive").GetBoolean().ShouldBeFalse();
        (await lead.Client.DeleteAsync($"{Url}/{unused}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await lead.Client.GetAsync($"{Url}/{unused}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Template_KeepsOrder_AndAcceptsOnlyVisibleActiveQuestions()
    {
        var lead = await Lead();
        var outsider = await Lead(OtherDepartment);
        var first = await lead.Create(Url, await QuestionBody(lead, text: "Birinci"));
        var second = await lead.Create(Url, await QuestionBody(lead, text: "İkinci", competency: "Problem çözme"));
        var inactive = await lead.Create(Url, await QuestionBody(lead));
        await lead.Post($"{Url}/{inactive}/active", new { isActive = false });
        var foreign = await outsider.Create(Url, await QuestionBody(outsider, OtherDepartment));

        var templateId = await Template(lead, [second, first], type: "Teknik");
        var withInactive = await lead.Post(TemplatesUrl, new
        {
            name = "Pasif sorulu",
            departmentId = await lead.DictionaryItemId("Department", Department),
            questionIds = new[] { first, inactive },
        });
        var withForeign = await lead.Post(TemplatesUrl, new
        {
            name = "Başka birimin sorusuyla",
            departmentId = await lead.DictionaryItemId("Department", Department),
            questionIds = new[] { foreign },
        });

        withInactive.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        withForeign.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var detail = await lead.GetJson($"{TemplatesUrl}/{templateId}");
        detail.GetProperty("template").GetProperty("interviewTypeName").GetString().ShouldBe("Teknik");
        detail.GetProperty("template").GetProperty("questionCount").GetInt32().ShouldBe(2);
        detail.GetProperty("questions").EnumerateArray().Select(q => q.GetProperty("text").GetString()).ShouldBe(["İkinci", "Birinci"]);
        (await outsider.Client.GetAsync($"{TemplatesUrl}/{templateId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Şablonda kalan soru pasif yapıldıktan sonra da şablon güncellenebilir.
        await lead.Post($"{Url}/{first}/active", new { isActive = false });
        (await lead.Put($"{TemplatesUrl}/{templateId}", new
        {
            name = "Backend teknik",
            departmentId = await lead.DictionaryItemId("Department", Department),
            questionIds = new[] { first },
        })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await lead.GetJson($"{TemplatesUrl}/{templateId}")).GetProperty("template").GetProperty("name").GetString().ShouldBe("Backend teknik");
    }

    [Fact]
    public async Task Interview_GetsPositionTemplateForItsType_AndInterviewerSeesQuestions()
    {
        var hr = await Hr();
        var lead = await Lead();
        var question = await lead.Create(Url, await QuestionBody(lead, text: "Kafka'da tüketici grupları nasıl çalışır?"));
        var technical = await Template(lead, [question], type: "Teknik");
        var managerial = await Template(lead, [question], type: "Yönetici");
        var positionId = await hr.Create("/api/v1/positions", new
        {
            title = "Backend Geliştirici",
            departmentId = await hr.DictionaryItemId("Department", Department),
            levelId = await hr.DictionaryItemId("Level", "L3"),
            headcount = 1,
        });
        (await hr.Put($"/api/v1/positions/{positionId}/interview-templates", new { templateIds = new[] { technical, managerial } }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await hr.GetJson($"/api/v1/positions/{positionId}")).GetProperty("interviewTemplates").EnumerateArray()
            .Select(t => t.GetProperty("interviewTypeName").GetString()).ShouldBe(["Teknik", "Yönetici"]);

        var candidateId = await hr.Create("/api/v1/candidates", new { firstName = "Deniz", lastName = $"Aday{Guid.NewGuid():N}", email = $"{Guid.NewGuid():N}@ornek.com" });
        var applicationId = await hr.Create("/api/v1/applications", new { candidateId, positionId });
        var startsAt = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero).AddDays(Random.Shared.Next(30, 3000)).AddHours(9);
        var interviewId = await hr.Create("/api/v1/interviews", new
        {
            applicationId,
            typeId = await hr.DictionaryItemId("InterviewType", "Teknik"),
            mode = "Online",
            startsAt,
            durationMinutes = 60,
            interviewerUserIds = new[] { lead.Id },
        });

        var detail = await lead.GetJson($"/api/v1/interviews/{interviewId}");
        detail.GetProperty("interview").GetProperty("templateId").GetGuid().ShouldBe(technical);
        var template = detail.GetProperty("template");
        var asked = template.GetProperty("questions").EnumerateArray().Single();
        asked.GetProperty("text").GetString().ShouldBe("Kafka'da tüketici grupları nasıl çalışır?");
        asked.GetProperty("guidance").GetString().ShouldBe("Beklenen cevap");
        asked.GetProperty("competencyName").GetString().ShouldBe("Teknik bilgi");

        // Pozisyonda uygun şablon yoksa mülakat şablonsuz planlanır.
        var phone = await hr.Create("/api/v1/interviews", new
        {
            applicationId,
            typeId = await hr.DictionaryItemId("InterviewType", "Telefon"),
            mode = "Online",
            startsAt = startsAt.AddDays(1),
            durationMinutes = 30,
            interviewerUserIds = new[] { lead.Id },
        });
        (await hr.GetJson($"/api/v1/interviews/{phone}")).GetProperty("template").ValueKind.ShouldBe(JsonValueKind.Null);

        // Pasif şablon pozisyona bağlanamaz.
        await lead.Post($"{TemplatesUrl}/{managerial}/active", new { isActive = false });
        (await hr.Put($"/api/v1/positions/{positionId}/interview-templates", new { templateIds = new[] { managerial } }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Access_FollowsRoles()
    {
        var manager = await factory.LoginAs(Department, Role.DepartmentManager);
        var executive = await factory.LoginAs(Department, Role.Executive);
        var lead = await Lead();

        foreach (var user in new[] { manager, executive })
        {
            (await user.Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await user.Client.GetAsync(TemplatesUrl)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        (await lead.Put($"/api/v1/positions/{Guid.NewGuid()}/interview-templates", new { templateIds = Array.Empty<Guid>() }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
