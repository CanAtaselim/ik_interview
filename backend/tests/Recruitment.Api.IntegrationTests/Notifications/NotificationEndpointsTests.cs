using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.CvReviews;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Notifications;

public sealed class NotificationEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Department = "Yazılım";

    private Task<ApiUser> Hr() => factory.LoginAs("İnsan Kaynakları", Role.HrSpecialist);

    private Task<ApiUser> Lead() => factory.LoginAs(Department, Role.TeamLead);

    private static async Task<(Guid CandidateId, Guid ApplicationId)> Application(ApiUser hr, string firstName = "Deniz", string? email = null)
    {
        var positionId = await hr.Create("/api/v1/positions", new
        {
            title = "Backend Geliştirici",
            departmentId = await hr.DictionaryItemId("Department", Department),
            levelId = await hr.DictionaryItemId("Level", "L3"),
            headcount = 1,
        });
        var candidateId = await hr.Create("/api/v1/candidates", new
        {
            firstName, lastName = $"Aday{Guid.NewGuid():N}", email = email ?? $"{Guid.NewGuid():N}@aday.com",
        });
        return (candidateId, await hr.Create("/api/v1/applications", new { candidateId, positionId }));
    }

    private static DateTimeOffset Slot() =>
        new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero).AddDays(Random.Shared.Next(30, 3000)).AddHours(9);

    [Fact]
    public async Task CvSuggestion_NotifiesReviewer_InAppLiveAndByEmail_OnlyOnce()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (candidateId, applicationId) = await Application(hr);

        var reviewId = await hr.SuggestCv(candidateId, lead, applicationId);
        await factory.ProcessOutboxAndEmails();

        var list = await lead.GetJson("/api/v1/me/notifications");
        list.GetProperty("unreadCount").GetInt32().ShouldBe(1);
        var notification = list.GetProperty("items").EnumerateArray().Single();
        notification.GetProperty("type").GetString().ShouldBe("CvReviewRequested");
        notification.GetProperty("title").GetString()!.ShouldStartWith("Size bir CV önerildi: Deniz ");
        notification.GetProperty("link").GetString().ShouldBe($"/cv-reviews/{reviewId}");
        notification.GetProperty("isRead").GetBoolean().ShouldBeFalse();
        factory.Pushes.Pushed.Count(p => p.UserId == lead.Id).ShouldBe(1);

        var email = factory.Emails.To(lead.Email()).Single();
        email.Subject.ShouldStartWith("Size bir CV önerildi");
        email.HtmlBody.ShouldContain($"/cv-reviews/{reviewId}");

        // Aynı olay yeniden işlense de (outbox tekrarı) ikinci bildirim veya e-posta oluşmaz.
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>().Dispatch(
                new CvReviewRequested(reviewId, candidateId, lead.Id, hr.Id, DateTimeOffset.UtcNow), CancellationToken.None);
        }
        await factory.ProcessOutboxAndEmails();
        (await lead.NotificationTitles()).Count.ShouldBe(1);
        factory.Emails.To(lead.Email()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task MarkRead_SingleAndAll_OnlyForOwnNotifications()
    {
        var hr = await Hr();
        var lead = await Lead();
        var other = await Lead();
        var (first, _) = await Application(hr);
        var (second, _) = await Application(hr);
        await hr.SuggestCv(first, lead);
        await hr.SuggestCv(second, lead);
        await factory.ProcessOutbox();

        var items = (await lead.GetJson("/api/v1/me/notifications")).GetProperty("items").EnumerateArray().ToList();
        var firstId = items[0].GetProperty("id").GetGuid();
        (await other.Post($"/api/v1/me/notifications/{firstId}/read")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await lead.Post($"/api/v1/me/notifications/{firstId}/read")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterOne = await lead.GetJson("/api/v1/me/notifications?unreadOnly=true");
        afterOne.GetProperty("unreadCount").GetInt32().ShouldBe(1);
        afterOne.GetProperty("items").GetArrayLength().ShouldBe(1);

        (await lead.Post("/api/v1/me/notifications/read-all")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await lead.GetJson("/api/v1/me/notifications")).GetProperty("unreadCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Preferences_TurnOffEmail_ButKeepInAppNotification()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (candidateId, _) = await Application(hr);

        var preferences = await lead.GetJson("/api/v1/me/notification-preferences");
        preferences.EnumerateArray().ShouldAllBe(p => p.GetProperty("emailEnabled").GetBoolean());
        (await lead.Put("/api/v1/me/notification-preferences", new
        {
            preferences = new[] { new { type = "CvReviewRequested", emailEnabled = false } },
        })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await hr.SuggestCv(candidateId, lead);
        await factory.ProcessOutboxAndEmails();

        (await lead.NotificationTitles()).ShouldHaveSingleItem();
        factory.Emails.To(lead.Email()).ShouldBeEmpty();
        (await lead.GetJson("/api/v1/me/notification-preferences")).EnumerateArray()
            .Single(p => p.GetProperty("type").GetString() == "CvReviewRequested")
            .GetProperty("emailEnabled").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task PositiveCvDecision_TellsHrToScheduleInterview()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (candidateId, _) = await Application(hr);
        var reviewId = await hr.SuggestCv(candidateId, lead);
        await lead.DecideCv(reviewId, "Positive", "Teknik olarak güçlü");
        await factory.ProcessOutbox();

        var notification = (await hr.GetJson("/api/v1/me/notifications")).GetProperty("items").EnumerateArray().First();
        notification.GetProperty("title").GetString()!.ShouldContain("mülakat planlayın");
        notification.GetProperty("link").GetString().ShouldBe($"/candidates/{candidateId}?tab=mulakatlar");
        notification.GetProperty("body").GetString()!.ShouldContain("Teknik olarak güçlü");
    }

    [Fact]
    public async Task InterviewLifecycle_SendsCalendarInvites_ToInterviewersAndCandidate()
    {
        var hr = await Hr();
        var lead = await Lead();
        var colleague = await Lead();
        var candidateEmail = $"{Guid.NewGuid():N}@aday.com";
        var (candidateId, applicationId) = await Application(hr, "<b>Ali</b>", candidateEmail);
        var startsAt = Slot();
        var typeId = await hr.DictionaryItemId("InterviewType", "Teknik");
        object Plan(DateTimeOffset at, Guid[] interviewers) => new
        {
            applicationId, typeId, mode = "Online", startsAt = at, durationMinutes = 60,
            meetingUrl = "https://teams.example.com/toplanti", note = "İç not: mimariye odaklanın",
            interviewerUserIds = interviewers, notifyCandidate = true, confirmConflicts = true,
        };

        var interviewId = await hr.Create("/api/v1/interviews", Plan(startsAt, [lead.Id, colleague.Id]));
        await factory.ProcessOutboxAndEmails();

        var leadInvite = factory.Emails.To(lead.Email()).Single();
        leadInvite.Subject.ShouldStartWith("Mülakat daveti: <b>Ali</b>");
        leadInvite.CalendarMethod.ShouldBe(Domain.Notifications.CalendarMethod.Request);
        var leadCalendar = leadInvite.CalendarContent.ShouldNotBeNull();
        leadCalendar.ShouldContain($"UID:interview-{interviewId}@ik-ise-alim");
        leadCalendar.ShouldContain("METHOD:REQUEST");
        leadCalendar.ShouldContain($"DTSTART:{startsAt.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}");
        leadCalendar.ShouldContain("İç not");

        var candidateInvite = factory.Emails.To(candidateEmail).Single();
        candidateInvite.Subject.ShouldBe("Mülakat daveti: Backend Geliştirici");
        // Aday adı HTML olarak kodlanır; iç not ve mülakatçı bilgisi adaya gitmez.
        candidateInvite.HtmlBody.ShouldContain("&lt;b&gt;Ali&lt;/b&gt;");
        candidateInvite.HtmlBody.ShouldContain("https://teams.example.com/toplanti");
        var candidateCalendar = candidateInvite.CalendarContent.ShouldNotBeNull();
        candidateCalendar.ShouldNotContain("İç not");
        candidateCalendar.ShouldNotContain(lead.Email());
        (await hr.GetJson($"/api/v1/candidates/{candidateId}/timeline")).GetProperty("items").EnumerateArray()
            .Select(t => t.GetProperty("summary").GetString()).ShouldContain("E-posta gönderildi: Mülakat daveti: Backend Geliştirici");

        // Değişiklik: mülakatçıdan biri çıkarıldı, saat değişti.
        (await hr.Put($"/api/v1/interviews/{interviewId}", Plan(startsAt.AddHours(2), [lead.Id]))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await factory.ProcessOutboxAndEmails();
        var update = factory.Emails.To(lead.Email()).Single(e => e.Subject.StartsWith("Mülakat güncellendi"));
        update.CalendarContent.ShouldNotBeNull().ShouldContain("SEQUENCE:1");
        factory.Emails.To(colleague.Email()).Single(e => e.Subject.StartsWith("Mülakattan çıkarıldınız"))
            .CalendarContent.ShouldNotBeNull().ShouldContain("METHOD:CANCEL");
        factory.Emails.To(candidateEmail).Single(e => e.Subject.StartsWith("Mülakat bilgileriniz güncellendi"));

        (await hr.Post($"/api/v1/interviews/{interviewId}/cancel", new { reason = "Aday erteleme istedi" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await factory.ProcessOutboxAndEmails();
        factory.Emails.To(lead.Email()).Single(e => e.Subject.StartsWith("Mülakat iptal edildi")).CalendarContent.ShouldNotBeNull().ShouldContain("SEQUENCE:2");
        factory.Emails.To(candidateEmail).Single(e => e.Subject.StartsWith("Mülakat iptal edildi")).CalendarMethod
            .ShouldBe(Domain.Notifications.CalendarMethod.Cancel);
    }

    [Fact]
    public async Task Interview_WithoutCandidateNotification_EmailsOnlyInterviewers()
    {
        var hr = await Hr();
        var lead = await Lead();
        var candidateEmail = $"{Guid.NewGuid():N}@aday.com";
        var (_, applicationId) = await Application(hr, email: candidateEmail);

        await hr.Create("/api/v1/interviews", new
        {
            applicationId, typeId = await hr.DictionaryItemId("InterviewType", "Teknik"), mode = "Online",
            startsAt = Slot(), durationMinutes = 60, interviewerUserIds = new[] { lead.Id }, confirmConflicts = true,
        });
        await factory.ProcessOutboxAndEmails();

        factory.Emails.To(lead.Email()).ShouldHaveSingleItem();
        factory.Emails.To(candidateEmail).ShouldBeEmpty();
    }

    [Fact]
    public async Task HiringRequest_NotifiesApproversInOrder_ThenRequester()
    {
        var lead = await Lead();
        var manager = await factory.LoginAs(Department, Role.DepartmentManager);
        var otherManager = await factory.LoginAs("Finans", Role.DepartmentManager);
        var hrManager = await factory.LoginAs("İnsan Kaynakları", Role.HrManager);
        var requestId = await lead.Create("/api/v1/hiring-requests", new
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
        (await lead.Post($"/api/v1/hiring-requests/{requestId}/submit")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await factory.ProcessOutbox();

        (await manager.NotificationTitles()).ShouldContain("Onayınızı bekleyen kadro talebi: Mobil Geliştirici");
        (await otherManager.NotificationTitles()).ShouldNotContain("Onayınızı bekleyen kadro talebi: Mobil Geliştirici");
        (await hrManager.NotificationTitles()).ShouldNotContain("Onayınızı bekleyen kadro talebi: Mobil Geliştirici");

        (await manager.Post($"/api/v1/hiring-requests/{requestId}/approve", new { note = "Uygun" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await factory.ProcessOutbox();
        (await hrManager.NotificationTitles()).ShouldContain("Onayınızı bekleyen kadro talebi: Mobil Geliştirici");

        (await hrManager.Post($"/api/v1/hiring-requests/{requestId}/approve", new { note = "Uygun" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await factory.ProcessOutbox();
        (await lead.NotificationTitles()).ShouldContain("Kadro talebiniz onaylandı: Mobil Geliştirici");
    }

    [Fact]
    public async Task HiringRequestRejection_AlsoNotifiesEarlierApprovers_ButNotTheRejecter()
    {
        var lead = await Lead();
        var manager = await factory.LoginAs(Department, Role.DepartmentManager);
        var hrManager = await factory.LoginAs("İnsan Kaynakları", Role.HrManager);
        var title = await factory.PositionTitle("Veri Analisti");
        var requestId = await lead.Create("/api/v1/hiring-requests", new
        {
            title,
            departmentId = await lead.DictionaryItemId("Department", Department),
            levelId = await lead.DictionaryItemId("Level", "L3"),
            headcount = 1,
            reason = "NewHeadcount",
            urgency = "Normal",
            expectedStartDate = "2026-12-01",
            competencies = new[] { "SQL" },
            description = "Analitik ekip",
        });
        await lead.Post($"/api/v1/hiring-requests/{requestId}/submit");
        await manager.Post($"/api/v1/hiring-requests/{requestId}/approve", new { note = "Uygun" });
        await factory.ProcessOutbox();

        (await hrManager.Post($"/api/v1/hiring-requests/{requestId}/reject", new { reason = "Bütçe yok" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await factory.ProcessOutbox();

        (await lead.NotificationTitles()).ShouldContain($"Kadro talebiniz reddedildi: {title}");
        (await manager.NotificationTitles()).ShouldContain($"Onayladığınız kadro talebi reddedildi: {title}");
        (await manager.NotificationTitles()).ShouldNotContain($"Kadro talebiniz reddedildi: {title}");
        (await hrManager.NotificationTitles()).ShouldNotContain($"Onayladığınız kadro talebi reddedildi: {title}");
        (await lead.NotificationTitles()).ShouldNotContain($"Onayladığınız kadro talebi reddedildi: {title}");
    }

    [Fact]
    public async Task BlacklistRequestBySpecialist_NotifiesHrManagers()
    {
        var hr = await Hr();
        var hrManager = await factory.LoginAs("İnsan Kaynakları", Role.HrManager);
        var (candidateId, _) = await Application(hr, "Kara");

        await hr.Create("/api/v1/blacklist-entries", new
        {
            candidateId,
            reasonId = await hr.DictionaryItemId("BlacklistReason", "Etik olmayan davranış"),
            description = "Sahte belge sundu.",
        });
        await factory.ProcessOutbox();

        (await hrManager.NotificationTitles()).ShouldContain(t => t.StartsWith("Onay bekleyen kara liste talebi: Kara "));
    }

    [Fact]
    public async Task EmailsAreSkipped_WhenSmtpIsNotConfigured()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (candidateId, _) = await Application(hr);
        factory.Emails.IsConfigured = false;
        try
        {
            await hr.SuggestCv(candidateId, lead);
            await factory.ProcessOutboxAndEmails();

            factory.Emails.To(lead.Email()).ShouldBeEmpty();
            (await lead.NotificationTitles()).ShouldHaveSingleItem();
        }
        finally
        {
            factory.Emails.IsConfigured = true;
        }
    }
}
