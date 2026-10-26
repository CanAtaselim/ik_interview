using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.CvReviews.ProcessCvReviewDeadlines;
using Recruitment.Domain.Users;
using Recruitment.Infrastructure.Persistence;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.CvReviews;

public sealed class CvReviewEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/v1/cv-reviews";
    private const string Department = "Yazılım";
    private const string OtherDepartment = "Finans";

    private Task<ApiUser> Hr() => factory.LoginAs("İnsan Kaynakları", Role.HrSpecialist);

    private Task<ApiUser> Lead(string department = Department) => factory.LoginAs(department, Role.TeamLead);

    private static async Task<(Guid Id, string Email)> CreateCandidate(ApiUser hr)
    {
        var email = $"{Guid.NewGuid():N}@ornek.com";
        var id = await hr.Create(
            "/api/v1/candidates", new { firstName = "Deniz", lastName = $"Aday{Guid.NewGuid():N}", email, notes = "İK'ya özel not" });
        return (id, email);
    }

    private static async Task<Guid> CreatePosition(ApiUser hr, string department = Department) =>
        await hr.Create("/api/v1/positions", new
        {
            title = "Backend Geliştirici",
            departmentId = await hr.DictionaryItemId("Department", department),
            levelId = await hr.DictionaryItemId("Level", "L3"),
            headcount = 1,
        });

    private static Task<Guid> Start(ApiUser hr, Guid candidateId, Guid positionId) =>
        hr.Create("/api/v1/applications", new { candidateId, positionId });

    private static async Task<JsonElement> Review(ApiUser user, Guid reviewId) =>
        (await user.GetJson($"{Url}/{reviewId}")).GetProperty("review");

    private static async Task<List<Guid>> ListIds(ApiUser user, string query = "") =>
        [.. (await user.GetJson($"{Url}?pageSize=100{query}")).Ids()];

    [Fact]
    public async Task Suggest_ToSeveralLeads_CreatesPendingReviewForEach_WithDueDate()
    {
        var hr = await Hr();
        var first = await Lead();
        var second = await Lead();
        var (candidateId, _) = await CreateCandidate(hr);
        var applicationId = await Start(hr, candidateId, await CreatePosition(hr));

        var response = await hr.Post(Url, new
        {
            candidateId,
            applicationId,
            reviewerUserIds = new[] { first.Id, second.Id, first.Id },
            note = " Go deneyimine bakar mısınız? ",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Json()).GetProperty("reviewIds").GetArrayLength().ShouldBe(2);

        var reviews = (await hr.GetJson($"{Url}?candidateId={candidateId}")).GetProperty("items").EnumerateArray().ToList();
        reviews.Count.ShouldBe(2);
        foreach (var review in reviews)
        {
            review.GetProperty("decision").GetString().ShouldBe("Pending");
            review.GetProperty("positionTitle").GetString().ShouldBe("Backend Geliştirici");
            review.GetProperty("departmentName").GetString().ShouldBe(Department);
            review.GetProperty("suggestionNote").GetString().ShouldBe("Go deneyimine bakar mısınız?");
            review.GetProperty("isOverdue").GetBoolean().ShouldBeFalse();
            // İK öneriyi yapar ama karar veremez.
            review.GetProperty("canDecide").GetBoolean().ShouldBeFalse();
            var suggestedAt = review.GetProperty("suggestedAt").GetDateTimeOffset();
            var dueAt = review.GetProperty("dueAt").GetDateTimeOffset();
            // 3 iş günü: araya hafta sonu girerse 5 takvim günü olur. Son tarih öneri anından birkaç
            // milisaniye önce alınan saatten hesaplanır.
            (dueAt - suggestedAt).ShouldBeInRange(TimeSpan.FromDays(3) - TimeSpan.FromSeconds(1), TimeSpan.FromDays(6));
        }

        await factory.ProcessOutbox();
        var timeline = (await hr.GetJson($"/api/v1/candidates/{candidateId}/timeline")).GetProperty("items")
            .EnumerateArray().Select(t => t.GetProperty("summary").GetString()).ToList();
        timeline.Count(s => s == "CV, “Backend Geliştirici” pozisyonu için Test Kullanıcısı adlı takım liderine önerildi.")
            .ShouldBe(2);
    }

    [Fact]
    public async Task Suggest_WithoutPosition_UsesLeadsOwnDepartment()
    {
        var hr = await Hr();
        var lead = await Lead(OtherDepartment);
        var (candidateId, _) = await CreateCandidate(hr);

        var reviewId = await hr.SuggestCv(candidateId, lead);

        var review = await Review(lead, reviewId);
        review.GetProperty("departmentName").GetString().ShouldBe(OtherDepartment);
        review.GetProperty("positionTitle").ValueKind.ShouldBe(JsonValueKind.Null);
        review.GetProperty("canDecide").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Suggest_RejectsInvalidRequests()
    {
        var hr = await Hr();
        var lead = await Lead();
        var manager = await factory.LoginAs(Department, Role.DepartmentManager);
        var (candidateId, _) = await CreateCandidate(hr);
        var (otherCandidateId, _) = await CreateCandidate(hr);
        var positionId = await CreatePosition(hr);
        var othersApplication = await Start(hr, otherCandidateId, positionId);
        var closedApplication = await Start(hr, candidateId, positionId);
        await hr.Post($"/api/v1/applications/{closedApplication}/close", new
        {
            status = "Rejected",
            rejectionReasonId = await hr.DictionaryItemId("RejectionReason", "Deneyim yetersiz"),
        });

        var noReviewer = await hr.Post(Url, new { candidateId, reviewerUserIds = Array.Empty<Guid>() });
        var notALead = await hr.Post(Url, new { candidateId, reviewerUserIds = new[] { manager.Id } });
        var unknownCandidate = await hr.Post(Url, new { candidateId = Guid.NewGuid(), reviewerUserIds = new[] { lead.Id } });
        var foreignApplication = await hr.Post(
            Url, new { candidateId, applicationId = othersApplication, reviewerUserIds = new[] { lead.Id } });
        var closed = await hr.Post(
            Url, new { candidateId, applicationId = closedApplication, reviewerUserIds = new[] { lead.Id } });
        var unknownDepartment = await hr.Post(
            Url, new { candidateId, departmentId = Guid.NewGuid(), reviewerUserIds = new[] { lead.Id } });

        noReviewer.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await noReviewer.Json()).GetProperty("errors").GetProperty("reviewerUserIds")[0].GetString()
            .ShouldBe("En az bir takım lideri seçmelisiniz.");
        notALead.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await notALead.Json()).GetProperty("errors").GetProperty("reviewerUserIds")[0].GetString()
            .ShouldBe("Seçilen kişilerin tümü aktif takım lideri olmalı.");
        unknownCandidate.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreignApplication.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        closed.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await closed.Json()).GetProperty("detail").GetString().ShouldBe("Sonlanmış bir süreç için CV önerilemez.");
        unknownDepartment.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ListIds(hr, $"&candidateId={candidateId}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Suggest_ToSameLeadWhileWaiting_Returns409_ButNewSuggestionIsAllowedAfterDecision()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (candidateId, _) = await CreateCandidate(hr);
        var reviewId = await hr.SuggestCv(candidateId, lead);

        var again = await hr.Post(Url, new { candidateId, reviewerUserIds = new[] { lead.Id } });

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.Json()).GetProperty("detail").GetString()
            .ShouldBe("Bu aday Test Kullanıcısı adlı takım liderine zaten önerilmiş ve kararı bekleniyor.");

        var reasonId = await hr.DictionaryItemId("RejectionReason", "Deneyim yetersiz");
        (await lead.DecideCv(reviewId, "Negative", "Deneyimi az", reasonId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        // Karar değişmez; İK aynı lidere yeniden öneri açabilir.
        var reopened = await hr.SuggestCv(candidateId, lead);
        reopened.ShouldNotBe(reviewId);
    }

    [Fact]
    public async Task Suggest_BlacklistedCandidate_NeedsConfirmation_AndConfirmationIsLogged()
    {
        var hr = await Hr();
        var manager = await factory.LoginAs("İnsan Kaynakları", Role.HrManager);
        var lead = await Lead();
        var (candidateId, _) = await CreateCandidate(hr);
        await manager.Create("/api/v1/blacklist-entries", new
        {
            candidateId,
            reasonId = await manager.DictionaryItemId("BlacklistReason", "Etik olmayan davranış"),
            description = "Sahte belge sundu.",
        });

        var warned = await hr.Post(Url, new { candidateId, reviewerUserIds = new[] { lead.Id } });
        var confirmed = await hr.Post(Url, new { candidateId, reviewerUserIds = new[] { lead.Id }, confirmBlacklist = true });

        warned.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await warned.Json();
        problem.GetProperty("code").GetString().ShouldBe("CvReview.BlacklistMatch");
        problem.GetProperty("blacklistMatches")[0].GetProperty("candidateId").GetGuid().ShouldBe(candidateId);
        confirmed.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.AccessLogs.CountAsync(l =>
            l.CandidateId == candidateId && l.UserId == hr.Id &&
            l.Action == Domain.Audit.AccessAction.BlacklistWarningAcknowledged)).ShouldBe(1);

        // Takım lideri kara liste bilgisini göremez.
        var detail = await (await lead.Client.GetAsync($"{Url}/{(await confirmed.Json()).GetProperty("reviewIds")[0].GetGuid()}"))
            .Content.ReadAsStringAsync();
        detail.ShouldNotContain("blacklist", Case.Insensitive);
        detail.ShouldNotContain("Sahte belge");
    }

    [Fact]
    public async Task Decide_RequiresReason_AndRejectionReasonForNegative()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (candidateId, _) = await CreateCandidate(hr);
        var reviewId = await hr.SuggestCv(candidateId, lead);

        var noReason = await lead.DecideCv(reviewId, "Positive", " ");
        var noRejectionReason = await lead.DecideCv(reviewId, "Negative", "Deneyimi az");
        var unknownRejectionReason = await lead.DecideCv(reviewId, "Negative", "Deneyimi az", Guid.NewGuid());
        var pending = await lead.DecideCv(reviewId, "Pending", "Geri al");

        noReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await noReason.Json()).GetProperty("errors").GetProperty("reason")[0].GetString()
            .ShouldBe("Kararınızın gerekçesini yazmalısınız.");
        noRejectionReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await noRejectionReason.Json()).GetProperty("errors").GetProperty("rejectionReasonId")[0].GetString()
            .ShouldBe("Olumsuz karar için ret nedeni seçmelisiniz.");
        unknownRejectionReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        pending.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Review(lead, reviewId)).GetProperty("decision").GetString().ShouldBe("Pending");
    }

    [Fact]
    public async Task Decide_OnHoldCanBeFinalized_ButFinalDecisionCannotChange()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (candidateId, _) = await CreateCandidate(hr);
        var reviewId = await hr.SuggestCv(candidateId, lead);
        var reasonId = await hr.DictionaryItemId("RejectionReason", "Teknik yetkinlik yetersiz");

        (await lead.DecideCv(reviewId, "OnHold", "Önce diğer adayları görelim")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var onHold = await Review(lead, reviewId);
        onHold.GetProperty("decision").GetString().ShouldBe("OnHold");
        onHold.GetProperty("canDecide").GetBoolean().ShouldBeTrue();

        (await lead.DecideCv(reviewId, "Negative", " Go bilgisi yok. ", reasonId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var changed = await lead.DecideCv(reviewId, "Positive", "Fikrimi değiştirdim");

        changed.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await changed.Json()).GetProperty("detail").GetString()
            .ShouldBe("Bu değerlendirme için karar verilmiş; karar değiştirilemez.");
        var decided = await Review(hr, reviewId);
        decided.GetProperty("decision").GetString().ShouldBe("Negative");
        decided.GetProperty("decisionReason").GetString().ShouldBe("Go bilgisi yok.");
        decided.GetProperty("rejectionReasonName").GetString().ShouldBe("Teknik yetkinlik yetersiz");
        decided.GetProperty("decidedAt").ValueKind.ShouldBe(JsonValueKind.String);
        decided.GetProperty("canDecide").GetBoolean().ShouldBeFalse();

        await factory.ProcessOutbox();
        var timeline = (await hr.GetJson($"/api/v1/candidates/{candidateId}/timeline")).GetProperty("items")
            .EnumerateArray().Select(t => t.GetProperty("summary").GetString()).ToList();
        timeline.ShouldContain("Test Kullanıcısı CV'yi beklemeye aldı.");
        timeline.ShouldContain("Test Kullanıcısı CV'yi olumsuz değerlendirdi.");
        // Gerekçe çizelgeye yazılmaz.
        timeline.ShouldNotContain(s => s!.Contains("Go bilgisi yok"));
    }

    [Fact]
    public async Task Decide_IsOnlyForTheLeadTheCvWasSuggestedTo()
    {
        var hr = await Hr();
        var lead = await Lead();
        var colleague = await Lead();
        var outsider = await Lead(OtherDepartment);
        var (candidateId, _) = await CreateCandidate(hr);
        var applicationId = await Start(hr, candidateId, await CreatePosition(hr));
        var reviewId = await hr.SuggestCv(candidateId, lead, applicationId);

        // Aynı birimdeki lider öneriyi görür ama karar veremez; başka birimdeki hiç göremez.
        (await colleague.DecideCv(reviewId, "Positive")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await hr.DecideCv(reviewId, "Positive")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await outsider.DecideCv(reviewId, "Positive")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Review(colleague, reviewId)).GetProperty("canDecide").GetBoolean().ShouldBeFalse();

        (await lead.DecideCv(reviewId, "Positive")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TeamLead_SeesOnlyReviewsSuggestedToThemOrTheirDepartment()
    {
        var hr = await Hr();
        var lead = await Lead();
        var colleague = await Lead();
        var outsider = await Lead(OtherDepartment);
        var (candidateId, _) = await CreateCandidate(hr);
        var applicationId = await Start(hr, candidateId, await CreatePosition(hr));
        var reviewId = await hr.SuggestCv(candidateId, lead, applicationId);

        (await ListIds(lead)).ShouldContain(reviewId);
        (await ListIds(lead, "&mine=true&status=Waiting")).ShouldContain(reviewId);
        (await ListIds(colleague)).ShouldContain(reviewId);
        (await ListIds(colleague, "&mine=true")).ShouldNotContain(reviewId);
        (await ListIds(outsider)).ShouldNotContain(reviewId);
        (await outsider.Client.GetAsync($"{Url}/{reviewId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListIds(hr)).ShouldContain(reviewId);
        (await ListIds(hr, "&mine=true")).ShouldNotContain(reviewId);

        // Takım lideri aday havuzuna ve aday kartının İK görünümüne erişemez.
        (await lead.Client.GetAsync($"/api/v1/candidates/{candidateId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await lead.Client.GetAsync("/api/v1/candidates")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_FiltersByStatusAndCandidateName()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (waitingCandidate, _) = await CreateCandidate(hr);
        var (decidedCandidate, _) = await CreateCandidate(hr);
        var waiting = await hr.SuggestCv(waitingCandidate, lead);
        var decided = await hr.SuggestCv(decidedCandidate, lead);
        await lead.DecideCv(decided, "Positive");
        var lastName = (await hr.GetJson($"/api/v1/candidates/{waitingCandidate}")).GetProperty("lastName").GetString()!;

        var waitingIds = await ListIds(lead, "&mine=true&status=Waiting");
        var decidedIds = await ListIds(lead, "&mine=true&status=Decided");
        var found = await ListIds(hr, $"&search=deniz {lastName.ToUpperInvariant()}");

        waitingIds.ShouldBe([waiting]);
        decidedIds.ShouldBe([decided]);
        found.ShouldBe([waiting]);
        (await ListIds(lead, "&mine=true&status=Overdue")).ShouldBeEmpty();
        // Karar bekleyenler üstte.
        (await ListIds(lead, "&mine=true")).ShouldBe([waiting, decided]);
    }

    [Fact]
    public async Task Detail_ForTeamLead_NeverContainsSalaryHrCommentContactOrNotes()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (candidateId, email) = await CreateCandidate(hr);
        var screeningId = await hr.Create($"/api/v1/candidates/{candidateId}/screenings", new { });
        (await hr.Put($"/api/v1/candidates/{candidateId}/screenings/{screeningId}", new
        {
            technicalSkillTags = new[] { "Kafka", "Go" },
            experienceSummary = "7 yıl backend",
            totalExperienceYears = 7.5,
            expectedSalary = new { amount = 185000.50, currency = "TRY", isNet = true },
            hrComment = "Pazarlık payı var",
            currentSalary = new { amount = 150000, currency = "TRY", isNet = false },
            currentBenefits = "Özel sağlık sigortası",
            otherProcesses = "Bir teklif aşamasında",
            overallRating = 4,
            noticePeriodWeeks = 4,
        })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var reviewId = await hr.SuggestCv(candidateId, lead, note: "Kafka tarafına bakar mısın?");

        var raw = await (await lead.Client.GetAsync($"{Url}/{reviewId}")).Content.ReadAsStringAsync();
        var detail = JsonDocument.Parse(raw).RootElement;

        var screening = detail.GetProperty("candidate").GetProperty("screenings").EnumerateArray().Single();
        screening.GetProperty("experienceSummary").GetString().ShouldBe("7 yıl backend");
        screening.GetProperty("technicalSkillTags").EnumerateArray().Select(t => t.GetString()).ShouldBe(["Kafka", "Go"]);
        screening.GetProperty("noticePeriodWeeks").GetInt32().ShouldBe(4);
        detail.GetProperty("review").GetProperty("suggestionNote").GetString().ShouldBe("Kafka tarafına bakar mısın?");
        detail.GetProperty("candidate").GetProperty("firstName").GetString().ShouldBe("Deniz");

        foreach (var hidden in new[]
                 {
                     "salary", "185000", "150000", "hrComment", "Pazarlık", "benefits", "sağlık sigortası",
                     "otherProcesses", "teklif aşamasında", "overallRating", email, "notes", "özel not", "phone",
                 })
        {
            raw.ShouldNotContain(hidden, Case.Insensitive);
        }
    }

    [Fact]
    public async Task DocumentLink_LetsReviewerDownloadCv_AndLogsAccess()
    {
        var hr = await Hr();
        var lead = await Lead();
        var outsider = await Lead(OtherDepartment);
        var (candidateId, _) = await CreateCandidate(hr);
        var (otherCandidateId, _) = await CreateCandidate(hr);
        var pdf = TestFiles.Pdf("Kafka ve Go deneyimi");
        var documentId = (await (await hr.Client.PostAsync(
                $"/api/v1/candidates/{candidateId}/documents", TestFiles.AsUpload(pdf, "cv.pdf"))).Json())
            .GetProperty("id").GetGuid();
        var foreignDocumentId = (await (await hr.Client.PostAsync(
                $"/api/v1/candidates/{otherCandidateId}/documents", TestFiles.AsUpload(pdf, "baska.pdf"))).Json())
            .GetProperty("id").GetGuid();
        var reviewId = await hr.SuggestCv(candidateId, lead);

        var link = await lead.Post($"{Url}/{reviewId}/documents/{documentId}/download-link");
        var foreign = await lead.Post($"{Url}/{reviewId}/documents/{foreignDocumentId}/download-link");
        var notVisible = await outsider.Post($"{Url}/{reviewId}/documents/{documentId}/download-link");
        var viaCandidateCard = await lead.Post($"/api/v1/candidates/{candidateId}/documents/{documentId}/download-link");

        link.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        notVisible.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        viaCandidateCard.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var url = (await link.Json()).GetProperty("url").GetString()!;
        url.ShouldStartWith("/api/v1/files/");
        var download = await factory.CreateApiClient().GetAsync(url);
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).ShouldBe(pdf);

        var listed = (await lead.GetJson($"{Url}/{reviewId}")).GetProperty("candidate").GetProperty("documents");
        listed.EnumerateArray().Single().GetProperty("fileName").GetString().ShouldBe("cv.pdf");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.AccessLogs.CountAsync(l => l.DocumentId == documentId && l.UserId == lead.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Pipeline_LeavingCvReviewStage_NeedsPositiveDecision()
    {
        var hr = await Hr();
        var lead = await Lead();
        var second = await Lead();
        var (candidateId, _) = await CreateCandidate(hr);
        var positionId = await CreatePosition(hr);
        var applicationId = await Start(hr, candidateId, positionId);
        var stages = (await hr.GetJson($"/api/v1/positions/{positionId}/pipeline")).GetProperty("stages").EnumerateArray()
            .ToDictionary(s => s.GetProperty("kind").GetString()!, s => s.GetProperty("id").GetGuid());
        Task<HttpResponseMessage> Move(string kind) =>
            hr.Post($"/api/v1/applications/{applicationId}/move", new { stageId = stages[kind] });
        async Task<JsonElement> CvDecision() =>
            (await hr.GetJson($"/api/v1/positions/{positionId}/pipeline")).GetProperty("active").EnumerateArray()
            .Single().GetProperty("cvDecision");

        await Move("HrScreening");
        await Move("CvReview");
        (await CvDecision()).ValueKind.ShouldBe(JsonValueKind.Null);

        var withoutReview = await Move("TechnicalInterview");
        withoutReview.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await withoutReview.Json()).GetProperty("detail").GetString()
            .ShouldBe("Aday bu aşamadan çıkabilmek için en az bir olumlu CV değerlendirmesi almalı.");

        var negativeReview = await hr.SuggestCv(candidateId, lead, applicationId);
        var positiveReview = await hr.SuggestCv(candidateId, second, applicationId);
        (await CvDecision()).GetString().ShouldBe("Pending");
        (await Move("TechnicalInterview")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        await lead.DecideCv(negativeReview, "Negative", "Deneyimi az", await hr.DictionaryItemId("RejectionReason", "Deneyim yetersiz"));
        (await Move("TechnicalInterview")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        // Bir liderin olumlu kararı yeter.
        await second.DecideCv(positiveReview, "Positive", "Görüşelim");
        (await CvDecision()).GetString().ShouldBe("Positive");
        (await Move("TechnicalInterview")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Pipeline_PositiveDecisionGivenWithoutPosition_AlsoCounts()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (candidateId, _) = await CreateCandidate(hr);
        await lead.DecideCv(await hr.SuggestCv(candidateId, lead), "Positive", "Havuz için uygun");
        var positionId = await CreatePosition(hr);
        var applicationId = await Start(hr, candidateId, positionId);
        var stages = (await hr.GetJson($"/api/v1/positions/{positionId}/pipeline")).GetProperty("stages").EnumerateArray()
            .ToDictionary(s => s.GetProperty("kind").GetString()!, s => s.GetProperty("id").GetGuid());

        foreach (var kind in new[] { "HrScreening", "CvReview", "TechnicalInterview" })
        {
            (await hr.Post($"/api/v1/applications/{applicationId}/move", new { stageId = stages[kind] }))
                .StatusCode.ShouldBe(HttpStatusCode.NoContent, kind);
        }
    }

    [Fact]
    public async Task Deadlines_RemindLeadThenEscalateToHr_OnlyOnce()
    {
        var hr = await Hr();
        var lead = await Lead();
        var (candidateId, _) = await CreateCandidate(hr);
        var reviewId = await hr.SuggestCv(candidateId, lead);

        async Task<CvReviewDeadlineResult> RunJob()
        {
            using var scope = factory.Services.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IDispatcher>()
                .Send(new ProcessCvReviewDeadlinesCommand(), CancellationToken.None);
            return result.Value;
        }

        async Task Shift(TimeSpan dueAgo, TimeSpan escalateIn)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dueAt = DateTimeOffset.UtcNow - dueAgo;
            var escalateAt = DateTimeOffset.UtcNow + escalateIn;
            await db.Database.ExecuteSqlAsync(
                $"update cv_reviews set due_at = {dueAt}, escalate_at = {escalateAt} where id = {reviewId}");
        }

        // Süresi dolmamış değerlendirmeye dokunulmaz (diğer testlerin kayıtları da öyledir).
        (await RunJob()).ShouldBe(new CvReviewDeadlineResult(0, 0));

        await Shift(dueAgo: TimeSpan.FromHours(1), escalateIn: TimeSpan.FromDays(2));
        (await RunJob()).ShouldBe(new CvReviewDeadlineResult(1, 0));
        (await RunJob()).ShouldBe(new CvReviewDeadlineResult(0, 0));
        (await ListIds(hr, "&status=Overdue")).ShouldContain(reviewId);
        (await Review(lead, reviewId)).GetProperty("isOverdue").GetBoolean().ShouldBeTrue();

        await Shift(dueAgo: TimeSpan.FromDays(3), escalateIn: -TimeSpan.FromHours(1));
        (await RunJob()).ShouldBe(new CvReviewDeadlineResult(0, 1));
        (await RunJob()).ShouldBe(new CvReviewDeadlineResult(0, 0));

        // Karar verilince gecikme biter.
        await lead.DecideCv(reviewId, "Positive");
        (await ListIds(hr, "&status=Overdue")).ShouldNotContain(reviewId);
        (await factory.ProcessOutbox()).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Access_FollowsRoles()
    {
        var hr = await Hr();
        var lead = await Lead();
        var departmentManager = await factory.LoginAs(Department, Role.DepartmentManager);
        var executive = await factory.LoginAs(Department, Role.Executive);
        var (candidateId, _) = await CreateCandidate(hr);
        var reviewId = await hr.SuggestCv(candidateId, lead);

        foreach (var user in new[] { departmentManager, executive })
        {
            (await user.Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await user.Client.GetAsync($"{Url}/{reviewId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await user.DecideCv(reviewId, "Positive")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        // Öneriyi yalnızca İK yapar.
        (await lead.Post(Url, new { candidateId, reviewerUserIds = new[] { lead.Id } })).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
        (await factory.CreateApiClient().GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
