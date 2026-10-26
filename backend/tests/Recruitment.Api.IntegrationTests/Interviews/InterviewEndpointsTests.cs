using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Interviews.SendInterviewReminders;
using Recruitment.Domain.Users;
using Recruitment.Infrastructure.Persistence;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Interviews;

public sealed class InterviewEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/v1/interviews";
    private const string Department = "Yazılım";

    private Task<ApiUser> Hr() => factory.LoginAs("İnsan Kaynakları", Role.HrSpecialist);

    private Task<ApiUser> Lead() => factory.LoginAs(Department, Role.TeamLead);

    // Her test kendi zaman dilimini kullanır ki testler birbirinin mülakatıyla çakışmasın.
    private static DateTimeOffset Slot(int hoursFromNow = 0) =>
        new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero)
            .AddDays(Random.Shared.Next(30, 3000))
            .AddHours(9 + hoursFromNow);

    private static async Task<(Guid CandidateId, Guid ApplicationId, Guid PositionId)> Application(ApiUser hr, string title = "Backend Geliştirici")
    {
        var positionId = await hr.Create("/api/v1/positions", new
        {
            title,
            departmentId = await hr.DictionaryItemId("Department", Department),
            levelId = await hr.DictionaryItemId("Level", "L3"),
            headcount = 1,
        });
        var candidateId = await hr.Create("/api/v1/candidates", new
        {
            firstName = "Deniz",
            lastName = $"Aday{Guid.NewGuid():N}",
            email = $"{Guid.NewGuid():N}@ornek.com",
            notes = "İK'ya özel not",
        });
        var applicationId = await hr.Create("/api/v1/applications", new { candidateId, positionId });
        return (candidateId, applicationId, positionId);
    }

    private async Task<Guid> Room(string name)
    {
        var manager = await factory.LoginAs("İnsan Kaynakları", Role.HrManager);
        return await manager.Create("/api/v1/dictionaries/Room/items", new { name = $"{name} {Guid.NewGuid():N}" });
    }

    private static async Task<object> Plan(
        ApiUser hr,
        Guid applicationId,
        DateTimeOffset startsAt,
        Guid[] interviewers,
        Guid? roomId = null,
        string mode = "InPerson",
        int durationMinutes = 60,
        bool confirmConflicts = false) => new
    {
        applicationId,
        typeId = await hr.DictionaryItemId("InterviewType", "Teknik"),
        mode,
        startsAt,
        durationMinutes,
        roomId,
        meetingUrl = mode == "Online" ? "https://teams.example.com/toplanti" : null,
        note = "Mimariye ağırlık verin.",
        interviewerUserIds = interviewers,
        confirmConflicts,
    };

    private static async Task<JsonElement> Detail(ApiUser user, Guid id) => await user.GetJson($"{Url}/{id}");

    [Fact]
    public async Task Schedule_CreatesInterview_VisibleToHrAndAssignedInterviewerOnly()
    {
        var hr = await Hr();
        var lead = await Lead();
        var other = await Lead();
        var (candidateId, applicationId, positionId) = await Application(hr);
        var startsAt = Slot();
        var roomId = await Room("Toplantı Odası");

        var id = await hr.Create(Url, await Plan(hr, applicationId, startsAt, [lead.Id], roomId));

        var interview = (await Detail(lead, id)).GetProperty("interview");
        interview.GetProperty("status").GetString().ShouldBe("Scheduled");
        interview.GetProperty("typeName").GetString().ShouldBe("Teknik");
        interview.GetProperty("positionTitle").GetString().ShouldBe("Backend Geliştirici");
        interview.GetProperty("startsAt").GetDateTimeOffset().ShouldBe(startsAt);
        interview.GetProperty("endsAt").GetDateTimeOffset().ShouldBe(startsAt.AddHours(1));
        interview.GetProperty("roomName").GetString()!.ShouldStartWith("Toplantı Odası");
        interview.GetProperty("meetingUrl").ValueKind.ShouldBe(JsonValueKind.Null);
        interview.GetProperty("isMine").GetBoolean().ShouldBeTrue();
        interview.GetProperty("interviewers")[0].GetProperty("displayName").GetString().ShouldBe("Test Kullanıcısı");

        var range = $"from={Uri.EscapeDataString(startsAt.AddDays(-1).ToString("O"))}&to={Uri.EscapeDataString(startsAt.AddDays(1).ToString("O"))}";
        (await lead.GetJson($"{Url}?{range}")).EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ShouldContain(id);
        (await other.GetJson($"{Url}?{range}")).EnumerateArray().ShouldBeEmpty();
        (await other.Client.GetAsync($"{Url}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await hr.GetJson($"{Url}?candidateId={candidateId}")).EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(id);
        (await hr.GetJson($"{Url}?positionId={positionId}")).GetArrayLength().ShouldBe(1);

        await factory.ProcessOutbox();
        var timeline = (await hr.GetJson($"/api/v1/candidates/{candidateId}/timeline")).GetProperty("items")
            .EnumerateArray().Select(t => t.GetProperty("summary").GetString()).ToList();
        timeline.ShouldContain(s => s!.StartsWith("Mülakat planlandı: Teknik · “Backend Geliştirici” · "));
    }

    [Fact]
    public async Task Schedule_RejectsInvalidPlans()
    {
        var hr = await Hr();
        var lead = await Lead();
        var admin = await factory.LoginAs(Department, Role.SystemAdmin);
        var (_, applicationId, _) = await Application(hr);

        var past = await hr.Post(Url, await Plan(hr, applicationId, DateTimeOffset.UtcNow.AddHours(-1), [lead.Id]));
        var noInterviewer = await hr.Post(Url, await Plan(hr, applicationId, Slot(), []));
        var notInterviewer = await hr.Post(Url, await Plan(hr, applicationId, Slot(), [admin.Id]));
        var shortInterview = await hr.Post(Url, await Plan(hr, applicationId, Slot(), [lead.Id], durationMinutes: 5));
        var badLink = await hr.Post(Url, new
        {
            applicationId,
            typeId = await hr.DictionaryItemId("InterviewType", "Teknik"),
            mode = "Online",
            startsAt = Slot(),
            durationMinutes = 60,
            meetingUrl = "toplanti-linki",
            interviewerUserIds = new[] { lead.Id },
        });
        var unknownApplication = await hr.Post(Url, await Plan(hr, Guid.NewGuid(), Slot(), [lead.Id]));

        past.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await past.Json()).GetProperty("detail").GetString().ShouldBe("Geçmiş bir saate mülakat planlanamaz.");
        noInterviewer.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await noInterviewer.Json()).GetProperty("errors").GetProperty("interviewerUserIds")[0].GetString()
            .ShouldBe("En az bir mülakatçı seçmelisiniz.");
        notInterviewer.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        shortInterview.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        badLink.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await badLink.Json()).GetProperty("errors").GetProperty("meetingUrl")[0].GetString()
            .ShouldBe("Toplantı bağlantısı https:// ile başlayan bir adres olmalı.");
        unknownApplication.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Schedule_WarnsAboutInterviewerAndRoomConflicts_AndConfirmationOverrides()
    {
        var hr = await Hr();
        var lead = await Lead();
        var freeLead = await Lead();
        var (_, firstApplication, _) = await Application(hr);
        var (_, secondApplication, _) = await Application(hr);
        var roomId = await Room("Ada");
        var startsAt = Slot();
        await hr.Create(Url, await Plan(hr, firstApplication, startsAt, [lead.Id], roomId));

        var overlapping = await hr.Post(Url, await Plan(hr, secondApplication, startsAt.AddMinutes(30), [lead.Id], roomId));
        var adjacent = await hr.Post(Url, await Plan(hr, secondApplication, startsAt.AddHours(1), [lead.Id], roomId));
        var roomOnly = await hr.Post(Url, await Plan(hr, secondApplication, startsAt.AddMinutes(15), [freeLead.Id], roomId, durationMinutes: 30));
        var online = await hr.Post(Url, await Plan(hr, secondApplication, startsAt.AddMinutes(15), [freeLead.Id], roomId, mode: "Online", durationMinutes: 30));

        overlapping.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await overlapping.Json();
        problem.GetProperty("code").GetString().ShouldBe("Interview.Conflicts");
        problem.GetProperty("conflicts").EnumerateArray().Select(c => c.GetProperty("kind").GetString())
            .ShouldBe(["Interviewer", "Room"], ignoreOrder: true);
        // Bitişik saat çakışma sayılmaz.
        adjacent.StatusCode.ShouldBe(HttpStatusCode.Created);
        roomOnly.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await roomOnly.Json()).GetProperty("conflicts").EnumerateArray().Single().GetProperty("kind").GetString().ShouldBe("Room");
        // Online mülakat odayı kullanmaz.
        online.StatusCode.ShouldBe(HttpStatusCode.Created);

        var confirmed = await hr.Post(Url, await Plan(hr, secondApplication, startsAt.AddMinutes(30), [lead.Id], roomId, confirmConflicts: true));
        confirmed.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Schedule_ForBlacklistedCandidate_NeedsConfirmation()
    {
        var hr = await Hr();
        var manager = await factory.LoginAs("İnsan Kaynakları", Role.HrManager);
        var lead = await Lead();
        var (candidateId, applicationId, _) = await Application(hr);
        await manager.Create("/api/v1/blacklist-entries", new
        {
            candidateId,
            reasonId = await manager.DictionaryItemId("BlacklistReason", "Etik olmayan davranış"),
            description = "Sahte belge sundu.",
        });
        var plan = await Plan(hr, applicationId, Slot(), [lead.Id]);

        var warned = await hr.Post(Url, plan);
        warned.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await warned.Json()).GetProperty("code").GetString().ShouldBe("Interview.BlacklistMatch");

        var json = JsonSerializer.SerializeToElement(plan);
        var confirmedBody = json.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value);
        confirmedBody["confirmBlacklist"] = true;
        (await hr.Post(Url, confirmedBody)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Reschedule_ChangesTimeAndInterviewers_IgnoringItsOwnSlot()
    {
        var hr = await Hr();
        var lead = await Lead();
        var manager = await factory.LoginAs(Department, Role.DepartmentManager);
        var (candidateId, applicationId, _) = await Application(hr);
        var startsAt = Slot();
        var id = await hr.Create(Url, await Plan(hr, applicationId, startsAt, [lead.Id]));

        // Kendi saatiyle çakışma sayılmaz; 30 dakika kaydırmak serbest.
        var moved = await hr.Put($"{Url}/{id}", await Plan(hr, applicationId, startsAt.AddMinutes(30), [lead.Id, manager.Id]));

        moved.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var interview = (await Detail(manager, id)).GetProperty("interview");
        interview.GetProperty("startsAt").GetDateTimeOffset().ShouldBe(startsAt.AddMinutes(30));
        interview.GetProperty("interviewers").GetArrayLength().ShouldBe(2);

        (await hr.Put($"{Url}/{id}", await Plan(hr, applicationId, startsAt, [manager.Id]))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await lead.Client.GetAsync($"{Url}/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await factory.ProcessOutbox();
        var timeline = (await hr.GetJson($"/api/v1/candidates/{candidateId}/timeline")).GetProperty("items")
            .EnumerateArray().Select(t => t.GetProperty("summary").GetString()).ToList();
        timeline.Count(s => s!.StartsWith("Mülakat ") && s.Contains("yerine yeni saate alındı")).ShouldBe(2);
    }

    [Fact]
    public async Task Cancel_RequiresReason_AndFreesTheSlot()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (_, applicationId, _) = await Application(hr);
        var (_, otherApplication, _) = await Application(hr);
        var startsAt = Slot();
        var id = await hr.Create(Url, await Plan(hr, applicationId, startsAt, [lead.Id]));

        (await hr.Post($"{Url}/{id}/cancel", new { reason = " " })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await hr.Post($"{Url}/{id}/cancel", new { reason = "Aday erteleme istedi." })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await Detail(hr, id);
        detail.GetProperty("interview").GetProperty("status").GetString().ShouldBe("Cancelled");
        detail.GetProperty("cancellationReason").GetString().ShouldBe("Aday erteleme istedi.");
        (await hr.Post(Url, await Plan(hr, otherApplication, startsAt, [lead.Id]))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await hr.Post($"{Url}/{id}/cancel", new { reason = "Tekrar" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Outcome_CanBeRecordedAfterStart_AndAppearsInAwaitingList()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (candidateId, applicationId, _) = await Application(hr);
        var id = await hr.Create(Url, await Plan(hr, applicationId, Slot(), [lead.Id]));

        var early = await hr.Post($"{Url}/{id}/outcome", new { outcome = "Completed" });
        early.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        await ShiftToPast(id);
        (await hr.GetJson($"{Url}?awaitingOutcome=true")).EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ShouldContain(id);
        (await Detail(hr, id)).GetProperty("interview").GetProperty("awaitingOutcome").GetBoolean().ShouldBeTrue();
        (await hr.Post($"{Url}/{id}/outcome", new { outcome = "Cancelled" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await lead.Post($"{Url}/{id}/outcome", new { outcome = "NoShow" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await hr.Post($"{Url}/{id}/outcome", new { outcome = "NoShow" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await hr.GetJson($"{Url}?awaitingOutcome=true")).EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ShouldNotContain(id);
        await factory.ProcessOutbox();
        (await hr.GetJson($"/api/v1/candidates/{candidateId}/timeline")).GetProperty("items").EnumerateArray()
            .Select(t => t.GetProperty("summary").GetString()).ShouldContain(s => s!.StartsWith("Aday mülakata gelmedi: Teknik"));
    }

    [Fact]
    public async Task Detail_ForInterviewer_HidesSalaryHrCommentContactAndNotes_AndAllowsCvDownload()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (candidateId, applicationId, _) = await Application(hr);
        var screeningId = await hr.Create($"/api/v1/candidates/{candidateId}/screenings", new { });
        await hr.Put($"/api/v1/candidates/{candidateId}/screenings/{screeningId}", new
        {
            experienceSummary = "7 yıl backend",
            expectedSalary = new { amount = 185000, currency = "TRY", isNet = true },
            hrComment = "Pazarlık payı var",
        });
        var pdf = TestFiles.Pdf("Kafka deneyimi");
        var documentId = (await (await hr.Client.PostAsync(
                $"/api/v1/candidates/{candidateId}/documents", TestFiles.AsUpload(pdf, "cv.pdf"))).Json())
            .GetProperty("id").GetGuid();
        var id = await hr.Create(Url, await Plan(hr, applicationId, Slot(), [lead.Id]));

        var raw = await (await lead.Client.GetAsync($"{Url}/{id}")).Content.ReadAsStringAsync();
        raw.ShouldContain("7 yıl backend");
        raw.ShouldContain("Mimariye ağırlık verin.");
        foreach (var hidden in new[] { "salary", "185000", "hrComment", "Pazarlık", "ornek.com", "özel not", "phone" })
            raw.ShouldNotContain(hidden, Case.Insensitive);

        var link = await lead.Post($"{Url}/{id}/documents/{documentId}/download-link");
        link.StatusCode.ShouldBe(HttpStatusCode.OK);
        var download = await factory.CreateApiClient().GetAsync((await link.Json()).GetProperty("url").GetString());
        (await download.Content.ReadAsByteArrayAsync()).ShouldBe(pdf);
        (await (await Lead()).Post($"{Url}/{id}/documents/{documentId}/download-link")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Availability_IsPersonal_AndShownInPlanningSchedule()
    {
        var hr = await Hr();
        var lead = await Lead();
        var colleague = await Lead();
        var (_, applicationId, _) = await Application(hr);
        var day = Slot();
        const string Path = "/api/v1/me/availability";

        var slotId = await lead.Create(Path, new { startsAt = day, endsAt = day.AddHours(3) });
        var overlapping = await lead.Post(Path, new { startsAt = day.AddHours(2), endsAt = day.AddHours(4) });
        var reversed = await lead.Post(Path, new { startsAt = day.AddHours(5), endsAt = day.AddHours(4) });
        await colleague.Create(Path, new { startsAt = day, endsAt = day.AddHours(1) });
        var interviewId = await hr.Create(Url, await Plan(hr, applicationId, day.AddHours(1), [lead.Id]));

        overlapping.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        reversed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var range = $"from={Uri.EscapeDataString(day.AddDays(-1).ToString("O"))}&to={Uri.EscapeDataString(day.AddDays(1).ToString("O"))}";
        (await lead.GetJson($"{Path}?{range}")).EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).ShouldBe([slotId]);

        var schedule = await hr.GetJson($"{Url}/schedules?{range}&userIds={lead.Id}&userIds={colleague.Id}");
        var leadSchedule = schedule.GetProperty("interviewers").EnumerateArray().Single(u => u.GetProperty("userId").GetGuid() == lead.Id);
        leadSchedule.GetProperty("available").EnumerateArray().Single().GetProperty("startsAt").GetDateTimeOffset().ShouldBe(day);
        var busy = leadSchedule.GetProperty("busy").EnumerateArray().Single();
        busy.GetProperty("interviewId").GetGuid().ShouldBe(interviewId);
        busy.GetProperty("title").GetString()!.ShouldStartWith("Teknik · Deniz ");
        schedule.GetProperty("interviewers").GetArrayLength().ShouldBe(2);
        (await lead.Client.GetAsync($"{Url}/schedules?{range}&userIds={lead.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Başkasının kaydı silinemez.
        (await colleague.Client.DeleteAsync($"{Path}/{slotId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await lead.Client.DeleteAsync($"{Path}/{slotId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await lead.GetJson($"{Path}?{range}")).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Pipeline_ShowsNextInterview_AndActiveApplicationSearchFindsCandidate()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (candidateId, applicationId, positionId) = await Application(hr, "Veri Mühendisi");
        var startsAt = Slot();
        await hr.Create(Url, await Plan(hr, applicationId, startsAt.AddDays(1), [lead.Id]));
        await hr.Create(Url, await Plan(hr, applicationId, startsAt, [lead.Id], confirmConflicts: true));

        var card = (await hr.GetJson($"/api/v1/positions/{positionId}/pipeline")).GetProperty("active").EnumerateArray().Single();
        card.GetProperty("nextInterviewAt").GetDateTimeOffset().ShouldBe(startsAt);

        var lastName = (await hr.GetJson($"/api/v1/candidates/{candidateId}")).GetProperty("lastName").GetString()!;
        var found = (await hr.GetJson($"/api/v1/applications/active?search={lastName.ToUpperInvariant()}")).EnumerateArray().Single();
        found.GetProperty("applicationId").GetGuid().ShouldBe(applicationId);
        found.GetProperty("positionTitle").GetString().ShouldBe("Veri Mühendisi");
    }

    [Fact]
    public async Task Reminders_AreProducedOnceForInterviewsWithinADay()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (_, applicationId, _) = await Application(hr);
        var id = await hr.Create(Url, await Plan(hr, applicationId, Slot(), [lead.Id]));

        async Task<int> RunJob()
        {
            using var scope = factory.Services.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<IDispatcher>()
                .Send(new SendInterviewRemindersCommand(), CancellationToken.None)).Value;
        }

        await RunJob();
        await SetStart(id, DateTimeOffset.UtcNow.AddHours(20));
        (await RunJob()).ShouldBeGreaterThanOrEqualTo(1);
        (await RunJob()).ShouldBe(0);
        await SetStart(id, DateTimeOffset.UtcNow.AddMinutes(40));
        (await RunJob()).ShouldBe(1);
        (await RunJob()).ShouldBe(0);
    }

    [Fact]
    public async Task Access_FollowsRoles()
    {
        var hr = await Hr();
        var lead = await Lead();
        var admin = await factory.LoginAs(Role.SystemAdmin);
        var (_, applicationId, _) = await Application(hr);

        (await lead.Post(Url, await Plan(hr, applicationId, Slot(), [lead.Id]))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.Client.GetAsync("/api/v1/me/availability?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await lead.Client.GetAsync("/api/v1/applications/active")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private Task ShiftToPast(Guid id) => SetStart(id, DateTimeOffset.UtcNow.AddHours(-2));

    private async Task SetStart(Guid id, DateTimeOffset startsAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var endsAt = startsAt.AddHours(1);
        await db.Database.ExecuteSqlAsync(
            $"update interviews set starts_at = {startsAt}, ends_at = {endsAt}, day_reminder_sent_at = null, hour_reminder_sent_at = null where id = {id}");
    }
}
