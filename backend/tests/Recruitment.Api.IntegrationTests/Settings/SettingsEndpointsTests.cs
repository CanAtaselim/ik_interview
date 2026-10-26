using System.Net;
using System.Text.Json;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Settings;

public sealed class SettingsEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/v1/settings";
    private const string Department = "Yazılım";

    private Task<ApiUser> Manager() => factory.LoginAs("İnsan Kaynakları", Role.HrManager);

    private Task<ApiUser> Admin() => factory.LoginAs("Bilgi Teknolojileri", Role.SystemAdmin);

    private static async Task<JsonElement> Template(ApiUser user, string code) =>
        (await user.GetJson($"{Url}/email-templates")).EnumerateArray().Single(t => t.GetProperty("code").GetString() == code);

    [Fact]
    public async Task Access_FollowsRoles()
    {
        var specialist = await factory.LoginAs("İnsan Kaynakları", Role.HrSpecialist);
        var lead = await factory.LoginAs(Department, Role.TeamLead);
        var manager = await Manager();
        var admin = await Admin();

        foreach (var user in new[] { specialist, lead })
        {
            (await user.Client.GetAsync($"{Url}/email-templates")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await user.Client.GetAsync($"{Url}/holidays")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await user.Client.GetAsync($"{Url}/role-mappings")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        (await manager.Client.GetAsync($"{Url}/approval-flow")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.Client.GetAsync($"{Url}/pipeline-stages")).StatusCode.ShouldBe(HttpStatusCode.OK);
        // AD grubu → rol eşlemesi yalnızca Sistem Admin'de.
        (await manager.Client.GetAsync($"{Url}/role-mappings")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.Client.GetAsync($"{Url}/role-mappings")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EmailTemplates_AreEditable_WithKnownVariablesOnly_AndUsedForSending()
    {
        var manager = await Manager();
        var lead = await factory.LoginAs(Department, Role.TeamLead);
        var hr = await factory.LoginAs("İnsan Kaynakları", Role.HrSpecialist);
        var template = await Template(manager, "Notification");
        var id = template.GetProperty("id").GetGuid();
        var original = (Subject: template.GetProperty("subject").GetString()!, Body: template.GetProperty("body").GetString()!);
        template.GetProperty("isModified").GetBoolean().ShouldBeFalse();

        var unknown = await manager.Put($"{Url}/email-templates/{id}", new { subject = "{Baslik} {Yanlis}", body = original.Body });
        unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await unknown.Json()).GetProperty("errors").GetProperty("subject")[0].GetString()!.ShouldStartWith("Tanımsız alan: {Yanlis}.");
        (await manager.Put($"{Url}/email-templates/{id}", new { subject = " ", body = original.Body })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        try
        {
            (await manager.Put($"{Url}/email-templates/{id}", new { subject = "[İK] {Baslik}", body = "<p>{AdSoyad}: {Mesaj}</p>" }))
                .StatusCode.ShouldBe(HttpStatusCode.NoContent);
            (await Template(manager, "Notification")).GetProperty("isModified").GetBoolean().ShouldBeTrue();

            // Yeni şablon bir sonraki e-postada kullanılır.
            var candidateId = await hr.Create("/api/v1/candidates", new { firstName = "Deniz", lastName = $"Aday{Guid.NewGuid():N}", email = $"{Guid.NewGuid():N}@aday.com" });
            await hr.SuggestCv(candidateId, lead);
            await factory.ProcessOutboxAndEmails();
            var email = factory.Emails.To(lead.Email()).Single();
            email.Subject.ShouldStartWith("[İK] Size bir CV önerildi");
            email.HtmlBody.ShouldContain("<p>Test Kullanıcısı: ");
        }
        finally
        {
            (await manager.Post($"{Url}/email-templates/{id}/reset")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var reset = await Template(manager, "Notification");
        reset.GetProperty("isModified").GetBoolean().ShouldBeFalse();
        reset.GetProperty("subject").GetString().ShouldBe(original.Subject);
    }

    [Fact]
    public async Task EmailTemplatePreview_RendersWithSampleValues_AndEncodesHtml()
    {
        var manager = await Manager();
        var id = (await Template(manager, "InterviewInvitationCandidate")).GetProperty("id").GetGuid();

        var preview = await (await manager.Post($"{Url}/email-templates/{id}/preview", new
        {
            subject = "Davet: {Pozisyon}",
            body = "<p>{AdayAdi}, {MulakatTarihi}</p><p>{Bilinmeyen}</p>",
        })).Json();

        preview.GetProperty("subject").GetString().ShouldBe("Davet: Backend Geliştirici");
        var html = preview.GetProperty("html").GetString()!;
        html.ShouldContain("<p>Deniz Aday, 21 Ekim 2026 Çarşamba 14:00–15:00</p>");
        // Önizleme kaydetmez ve tanımsız alan olduğu gibi kalır.
        html.ShouldContain("{Bilinmeyen}");
        (await Template(manager, "InterviewInvitationCandidate")).GetProperty("subject").GetString().ShouldNotBe("Davet: {Pozisyon}");
    }

    [Fact]
    public async Task ApprovalFlow_ChangesOnlyNewlySubmittedRequests()
    {
        var manager = await Manager();
        var lead = await factory.LoginAs(Department, Role.TeamLead);
        var flow = await manager.GetJson($"{Url}/approval-flow");
        flow.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["DepartmentManager", "HrManager"]);
        flow.GetProperty("allowedRoles").GetArrayLength().ShouldBe(2);

        async Task<Guid> SubmittedRequest()
        {
            var id = await lead.Create("/api/v1/hiring-requests", new
            {
                title = await factory.PositionTitle("Mobil Geliştirici"),
                departmentId = await lead.DictionaryItemId("Department", Department),
                levelId = await lead.DictionaryItemId("Level", "L3"),
                headcount = 1,
                reason = "NewHeadcount",
                urgency = "Normal",
                expectedStartDate = "2026-12-01",
                competencies = new[] { "Kotlin" },
                description = "Ekip büyüyor",
            });
            (await lead.Post($"/api/v1/hiring-requests/{id}/submit")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
            return id;
        }

        async Task<string[]> Steps(Guid id) =>
            [.. (await lead.GetJson($"/api/v1/hiring-requests/{id}")).GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("role").GetString()!)];

        (await manager.Put($"{Url}/approval-flow", new { roles = Array.Empty<string>() })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await manager.Put($"{Url}/approval-flow", new { roles = new[] { "Executive" } })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await manager.Put($"{Url}/approval-flow", new { roles = new[] { "HrManager", "HrManager" } })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var before = await SubmittedRequest();
        try
        {
            (await manager.Put($"{Url}/approval-flow", new { roles = new[] { "HrManager" } })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
            var after = await SubmittedRequest();

            (await Steps(before)).ShouldBe(["DepartmentManager", "HrManager"]);
            (await Steps(after)).ShouldBe(["HrManager"]);
        }
        finally
        {
            (await manager.Put($"{Url}/approval-flow", new { roles = new[] { "DepartmentManager", "HrManager" } })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await manager.GetJson($"{Url}/approval-flow")).GetProperty("roles").EnumerateArray().Select(r => r.GetString())
            .ShouldBe(["DepartmentManager", "HrManager"]);
        (await Steps(await SubmittedRequest())).ShouldBe(["DepartmentManager", "HrManager"]);
    }

    [Fact]
    public async Task PipelineStages_CanBeRenamed_ButRuleStagesCannotBeOptional()
    {
        var manager = await Manager();
        var hr = await factory.LoginAs("İnsan Kaynakları", Role.HrSpecialist);
        var settings = await manager.GetJson($"{Url}/pipeline-stages");
        var stages = settings.GetProperty("stages").EnumerateArray().ToList();
        stages.Count.ShouldBe(9);
        stages.Single(s => s.GetProperty("kind").GetString() == "CaseStudy").GetProperty("canBeOptional").GetBoolean().ShouldBeTrue();
        stages.Single(s => s.GetProperty("kind").GetString() == "CvReview").GetProperty("canBeOptional").GetBoolean().ShouldBeFalse();

        object Body(Func<JsonElement, (string Name, bool Optional)> change) => new
        {
            stages = stages.Select(s =>
            {
                var (name, optional) = change(s);
                return new { id = s.GetProperty("id").GetGuid(), name, isOptional = optional };
            }).ToArray(),
        };
        (string, bool) Same(JsonElement s) => (s.GetProperty("name").GetString()!, s.GetProperty("isOptional").GetBoolean());

        var cvOptional = await manager.Put($"{Url}/pipeline-stages", Body(s =>
            s.GetProperty("kind").GetString() == "CvReview" ? ("CV değerlendirme", true) : Same(s)));
        cvOptional.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await manager.Put($"{Url}/pipeline-stages", Body(s =>
            s.GetProperty("kind").GetString() == "HrScreening" ? ("Başvuru", false) : Same(s)))).StatusCode
            .ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await manager.Put($"{Url}/pipeline-stages", new { stages = stages.Take(2).Select(s => new { id = s.GetProperty("id").GetGuid(), name = "X", isOptional = false }).ToArray() }))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        try
        {
            (await manager.Put($"{Url}/pipeline-stages", Body(s =>
                s.GetProperty("kind").GetString() == "TechnicalInterview" ? ("Teknik görüşme", false) : Same(s))))
                .StatusCode.ShouldBe(HttpStatusCode.NoContent);

            var positionId = await hr.Create("/api/v1/positions", new
            {
                title = "Backend Geliştirici",
                departmentId = await hr.DictionaryItemId("Department", Department),
                levelId = await hr.DictionaryItemId("Level", "L3"),
                headcount = 1,
            });
            (await hr.GetJson($"/api/v1/positions/{positionId}/pipeline")).GetProperty("stages").EnumerateArray()
                .Select(s => s.GetProperty("name").GetString()).ShouldContain("Teknik görüşme");
        }
        finally
        {
            (await manager.Put($"{Url}/pipeline-stages", Body(Same))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await manager.GetJson($"{Url}/pipeline-stages")).GetProperty("stages").EnumerateArray()
            .Select(s => s.GetProperty("name").GetString()).ShouldContain("Teknik mülakat");
    }

    [Fact]
    public async Task PipelineStages_ShowHowManyCandidatesWaitInEachStage()
    {
        var manager = await Manager();
        var hr = await factory.LoginAs("İnsan Kaynakları", Role.HrSpecialist);
        var positionId = await hr.Create("/api/v1/positions", new
        {
            title = "Veri Mühendisi",
            departmentId = await hr.DictionaryItemId("Department", Department),
            levelId = await hr.DictionaryItemId("Level", "L3"),
            headcount = 1,
        });
        var candidateId = await hr.Create("/api/v1/candidates", new { firstName = "Deniz", lastName = $"Aday{Guid.NewGuid():N}", email = $"{Guid.NewGuid():N}@aday.com" });
        await hr.Create("/api/v1/applications", new { candidateId, positionId });

        var applied = (await manager.GetJson($"{Url}/pipeline-stages")).GetProperty("stages").EnumerateArray()
            .Single(s => s.GetProperty("kind").GetString() == "Applied");

        applied.GetProperty("activeCount").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task RoleMappings_CanBeAddedAndRemoved_ButNotTheLastAdminMapping()
    {
        var admin = await Admin();
        var group = $"Ek-Ik-Grubu-{Guid.NewGuid():N}";

        var id = await admin.Create($"{Url}/role-mappings", new { groupName = $" {group} ", role = "HrSpecialist" });
        var duplicate = await admin.Post($"{Url}/role-mappings", new { groupName = group.ToUpperInvariant(), role = "HrSpecialist" });
        var blank = await admin.Post($"{Url}/role-mappings", new { groupName = " ", role = "HrSpecialist" });

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        blank.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var mappings = (await admin.GetJson($"{Url}/role-mappings")).EnumerateArray().ToList();
        mappings.Single(m => m.GetProperty("id").GetGuid() == id).GetProperty("groupName").GetString().ShouldBe(group);
        // Aynı grup başka bir role de eşlenebilir.
        var other = await admin.Create($"{Url}/role-mappings", new { groupName = group, role = "TeamLead" });

        (await admin.Client.DeleteAsync($"{Url}/role-mappings/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.Client.DeleteAsync($"{Url}/role-mappings/{other}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.Client.DeleteAsync($"{Url}/role-mappings/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Sistem Admin rolüne eşlenmiş tek grup silinemez; silinirse kimse ayarlara giremez.
        var adminMapping = (await admin.GetJson($"{Url}/role-mappings")).EnumerateArray()
            .Single(m => m.GetProperty("role").GetString() == "SystemAdmin");
        var last = await admin.Client.DeleteAsync($"{Url}/role-mappings/{adminMapping.GetProperty("id").GetGuid()}");
        last.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await last.Json()).GetProperty("code").GetString().ShouldBe("RoleMapping.LastAdminMapping");

        // İkinci bir Sistem Admin grubu varsa silinebilir.
        var extra = await admin.Create($"{Url}/role-mappings", new { groupName = group, role = "SystemAdmin" });
        (await admin.Client.DeleteAsync($"{Url}/role-mappings/{extra}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Holidays_AreListedPerYear_WithRecurringAndOneOffDays()
    {
        var manager = await Manager();

        var seeded = (await manager.GetJson($"{Url}/holidays?year=2027")).EnumerateArray().ToList();
        seeded.ShouldContain(h => h.GetProperty("name").GetString() == "Cumhuriyet Bayramı" && h.GetProperty("date").GetString() == "2027-10-29");
        seeded.ShouldAllBe(h => h.GetProperty("isRecurring").GetBoolean());
        seeded.Select(h => h.GetProperty("date").GetString()).ShouldBe([.. seeded.Select(h => h.GetProperty("date").GetString()).Order()]);

        var oneOffId = await manager.Create($"{Url}/holidays", new { name = "Ramazan Bayramı 1. gün", date = "2027-03-09", recurring = false });
        var recurringId = await manager.Create($"{Url}/holidays", new { name = "Şirket yıldönümü", date = "2028-06-15", recurring = true });

        var in2027 = (await manager.GetJson($"{Url}/holidays?year=2027")).EnumerateArray().ToList();
        in2027.Single(h => h.GetProperty("id").GetGuid() == oneOffId).GetProperty("isRecurring").GetBoolean().ShouldBeFalse();
        in2027.Single(h => h.GetProperty("id").GetGuid() == recurringId).GetProperty("date").GetString().ShouldBe("2027-06-15");
        (await manager.GetJson($"{Url}/holidays?year=2028")).EnumerateArray().Select(h => h.GetProperty("id").GetGuid()).ShouldNotContain(oneOffId);

        (await manager.Post($"{Url}/holidays", new { name = "Tekrar", date = "2027-03-09", recurring = false })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await manager.Post($"{Url}/holidays", new { name = "Tekrar", date = "2030-10-29", recurring = false })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await manager.Post($"{Url}/holidays", new { name = " ", date = "2027-03-10", recurring = false })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await manager.Post($"{Url}/holidays", new { name = "Artık gün", date = "2028-02-29", recurring = true })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await manager.Post($"{Url}/holidays", new { name = "Artık gün", date = "2028-02-29", recurring = false })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await manager.Client.GetAsync($"{Url}/holidays?year=1990")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await manager.Client.DeleteAsync($"{Url}/holidays/{oneOffId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await manager.Client.DeleteAsync($"{Url}/holidays/{oneOffId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await manager.GetJson($"{Url}/holidays?year=2027")).EnumerateArray().Select(h => h.GetProperty("id").GetGuid()).ShouldNotContain(oneOffId);
    }
}
