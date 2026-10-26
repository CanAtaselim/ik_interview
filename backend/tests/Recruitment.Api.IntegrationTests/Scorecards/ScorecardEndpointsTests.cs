using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Users;
using Recruitment.Infrastructure.Persistence;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Scorecards;

public sealed class ScorecardEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Department = "Yazılım";

    private Task<ApiUser> Hr() => factory.LoginAs("İnsan Kaynakları", Role.HrSpecialist);

    private Task<ApiUser> Lead() => factory.LoginAs(Department, Role.TeamLead);

    private sealed record Setup(Guid PositionId, Guid CandidateId, Guid ApplicationId, Guid InterviewId);

    /// <summary>Şablonlu bir teknik mülakat planlar ve saatini geçmişe çeker (değerlendirilebilir hale gelir).</summary>
    private async Task<Setup> Interview(ApiUser hr, ApiUser author, ApiUser[] interviewers, bool past = true, Guid? positionId = null)
    {
        var departmentId = await author.DictionaryItemId("Department", Department);
        var technical = await author.DictionaryItemId("Competency", "Teknik bilgi");
        var communication = await author.DictionaryItemId("Competency", "İletişim");
        var q1 = await author.Create("/api/v1/questions", new
        {
            departmentId, competencyId = technical, type = "Technical", difficulty = 2, weight = 2, text = "REST sürümleme?", guidance = "URL veya başlık",
        });
        var q2 = await author.Create("/api/v1/questions", new
        {
            departmentId, competencyId = communication, type = "Behavioral", difficulty = 1, weight = 1, text = "Zor bir ekip anı?",
        });
        var templateId = await author.Create("/api/v1/interview-templates", new { name = $"Şablon {Guid.NewGuid():N}", departmentId, questionIds = new[] { q1, q2 } });

        positionId ??= await hr.Create("/api/v1/positions", new
        {
            title = "Backend Geliştirici", departmentId, levelId = await hr.DictionaryItemId("Level", "L3"), headcount = 1,
        });
        var candidateId = await hr.Create("/api/v1/candidates", new { firstName = "Deniz", lastName = $"Aday{Guid.NewGuid():N}", email = $"{Guid.NewGuid():N}@ornek.com" });
        var applicationId = await hr.Create("/api/v1/applications", new { candidateId, positionId });
        var interviewId = await hr.Create("/api/v1/interviews", new
        {
            applicationId,
            typeId = await hr.DictionaryItemId("InterviewType", "Teknik"),
            mode = "Online",
            startsAt = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero).AddDays(Random.Shared.Next(30, 3000)).AddHours(9),
            durationMinutes = 60,
            interviewerUserIds = interviewers.Select(i => i.Id).ToArray(),
            templateId,
            confirmConflicts = true,
        });
        if (past)
            await ShiftToPast(interviewId);
        return new Setup(positionId.Value, candidateId, applicationId, interviewId);
    }

    private async Task ShiftToPast(Guid interviewId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"update interviews set starts_at = now() - interval '2 hours', ends_at = now() - interval '1 hour' where id = {interviewId}");
    }

    private static async Task<Guid> Start(ApiUser interviewer, Guid interviewId)
    {
        var response = await interviewer.Post($"/api/v1/interviews/{interviewId}/scorecard");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Json()).GetProperty("id").GetGuid();
    }

    /// <summary>Şablon sorularına sırayla verilen puanlarla taslağı kaydedip gönderir.</summary>
    private static async Task Score(ApiUser interviewer, Guid scorecardId, string recommendation, params int?[] scores)
    {
        var scorecard = await interviewer.GetJson($"/api/v1/scorecards/{scorecardId}");
        var items = scorecard.GetProperty("items").EnumerateArray()
            .Select((item, index) => new { id = item.GetProperty("id").GetGuid(), score = index < scores.Length ? scores[index] : null, note = "Not" })
            .ToArray();
        (await interviewer.Put($"/api/v1/scorecards/{scorecardId}", new { items, generalNote = "Genel değerlendirme", recommendation }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var submit = await interviewer.Post($"/api/v1/scorecards/{scorecardId}/submit");
        submit.StatusCode.ShouldBe(HttpStatusCode.NoContent, await submit.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Start_CopiesTemplateQuestions_AndIsIdempotent()
    {
        var hr = await Hr();
        var lead = await Lead();
        var other = await Lead();
        var setup = await Interview(hr, lead, [lead]);

        var id = await Start(lead, setup.InterviewId);
        (await Start(lead, setup.InterviewId)).ShouldBe(id);

        var scorecard = await lead.GetJson($"/api/v1/scorecards/{id}");
        scorecard.GetProperty("status").GetString().ShouldBe("Draft");
        scorecard.GetProperty("canEdit").GetBoolean().ShouldBeTrue();
        scorecard.GetProperty("canSubmit").GetBoolean().ShouldBeTrue();
        var items = scorecard.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("text").GetString()).ShouldBe(["REST sürümleme?", "Zor bir ekip anı?"]);
        items[0].GetProperty("guidance").GetString().ShouldBe("URL veya başlık");
        items[0].GetProperty("competencyName").GetString().ShouldBe("Teknik bilgi");
        items[0].GetProperty("weight").GetDecimal().ShouldBe(2m);

        (await other.Post($"/api/v1/interviews/{setup.InterviewId}/scorecard")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await hr.Post($"/api/v1/interviews/{setup.InterviewId}/scorecard")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Submit_CalculatesWeightedScore_LocksScorecard_AndWritesTimeline()
    {
        var hr = await Hr();
        var lead = await Lead();
        var setup = await Interview(hr, lead, [lead]);
        var id = await Start(lead, setup.InterviewId);

        // Öneri olmadan gönderilemez.
        (await lead.Post($"/api/v1/scorecards/{id}/submit")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await Score(lead, id, "Hire", 5, 2);

        var scorecard = await lead.GetJson($"/api/v1/scorecards/{id}");
        scorecard.GetProperty("status").GetString().ShouldBe("Submitted");
        // (5×2 + 2×1) / 3 = 4
        scorecard.GetProperty("weightedScore").GetDecimal().ShouldBe(4m);
        scorecard.GetProperty("competencyScores").EnumerateArray()
            .ToDictionary(c => c.GetProperty("name").GetString()!, c => c.GetProperty("score").GetDecimal())
            .ShouldBe(new Dictionary<string, decimal> { ["Teknik bilgi"] = 5m, ["İletişim"] = 2m }, ignoreOrder: true);
        scorecard.GetProperty("canEdit").GetBoolean().ShouldBeFalse();

        var locked = await lead.Put($"/api/v1/scorecards/{id}", new { items = Array.Empty<object>(), recommendation = "NoHire" });
        locked.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await locked.Json()).GetProperty("detail").GetString().ShouldBe("Gönderilmiş değerlendirme değiştirilemez.");

        await factory.ProcessOutbox();
        (await hr.GetJson($"/api/v1/candidates/{setup.CandidateId}/timeline")).GetProperty("items").EnumerateArray()
            .Select(t => t.GetProperty("summary").GetString())
            .ShouldContain("Test Kullanıcısı Teknik mülakatı değerlendirmesini gönderdi: Alınsın.");
    }

    [Fact]
    public async Task Submit_BeforeInterviewStarts_IsRejected_ButDraftCanBeSaved()
    {
        var hr = await Hr();
        var lead = await Lead();
        var setup = await Interview(hr, lead, [lead], past: false);
        var id = await Start(lead, setup.InterviewId);

        (await lead.GetJson($"/api/v1/scorecards/{id}")).GetProperty("canSubmit").GetBoolean().ShouldBeFalse();
        var items = (await lead.GetJson($"/api/v1/scorecards/{id}")).GetProperty("items").EnumerateArray()
            .Select(i => new { id = i.GetProperty("id").GetGuid(), score = (int?)3 }).ToArray();
        (await lead.Put($"/api/v1/scorecards/{id}", new { items, recommendation = "Hire" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var early = await lead.Post($"/api/v1/scorecards/{id}/submit");
        early.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await early.Json()).GetProperty("detail").GetString().ShouldBe("Değerlendirmeyi mülakat başladıktan sonra gönderebilirsiniz.");
    }

    [Fact]
    public async Task OthersScorecards_StayHiddenUntilOwnIsSubmitted_HrSeesAll()
    {
        var hr = await Hr();
        var first = await Lead();
        var second = await Lead();
        var setup = await Interview(hr, first, [first, second]);
        var firstCard = await Start(first, setup.InterviewId);
        var secondCard = await Start(second, setup.InterviewId);
        await Score(first, firstCard, "StrongHire", 5, 5);

        // İkinci mülakatçı kendi değerlendirmesini göndermeden birincininkini göremez.
        (await second.Client.GetAsync($"/api/v1/scorecards/{firstCard}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var hidden = await second.GetJson($"/api/v1/interviews/{setup.InterviewId}/scorecards");
        hidden.GetProperty("othersHidden").GetBoolean().ShouldBeTrue();
        hidden.GetProperty("averageScore").ValueKind.ShouldBe(JsonValueKind.Null);
        var firstRow = hidden.GetProperty("interviewers").EnumerateArray().Single(i => i.GetProperty("userId").GetGuid() == first.Id);
        firstRow.GetProperty("progress").GetString().ShouldBe("Submitted");
        firstRow.GetProperty("recommendation").ValueKind.ShouldBe(JsonValueKind.Null);
        firstRow.GetProperty("canView").GetBoolean().ShouldBeFalse();

        // İK hepsini görür; taslağı da.
        (await hr.Client.GetAsync($"/api/v1/scorecards/{secondCard}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var forHr = await hr.GetJson($"/api/v1/interviews/{setup.InterviewId}/scorecards");
        forHr.GetProperty("averageScore").GetDecimal().ShouldBe(5m);
        forHr.GetProperty("canDebrief").GetBoolean().ShouldBeFalse();

        await Score(second, secondCard, "Hire", 3, 4);
        var afterSubmit = await second.GetJson($"/api/v1/interviews/{setup.InterviewId}/scorecards");
        afterSubmit.GetProperty("othersHidden").GetBoolean().ShouldBeFalse();
        afterSubmit.GetProperty("interviewers").EnumerateArray().Single(i => i.GetProperty("userId").GetGuid() == first.Id)
            .GetProperty("recommendation").GetString().ShouldBe("StrongHire");
        (await second.Client.GetAsync($"/api/v1/scorecards/{firstCard}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        // Ortalama: (5 + 3,33) / 2
        afterSubmit.GetProperty("averageScore").GetDecimal().ShouldBe(4.17m);
    }

    [Fact]
    public async Task Reopen_IsForHrManagerWithReason()
    {
        var hr = await Hr();
        var manager = await factory.LoginAs("İnsan Kaynakları", Role.HrManager);
        var lead = await Lead();
        var setup = await Interview(hr, lead, [lead]);
        var id = await Start(lead, setup.InterviewId);
        await Score(lead, id, "Hire", 4, 4);

        (await hr.Post($"/api/v1/scorecards/{id}/reopen", new { reason = "Düzeltme" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await manager.Post($"/api/v1/scorecards/{id}/reopen", new { reason = " " })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await manager.Post($"/api/v1/scorecards/{id}/reopen", new { reason = "Yanlış puan girilmiş" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var reopened = await lead.GetJson($"/api/v1/scorecards/{id}");
        reopened.GetProperty("status").GetString().ShouldBe("Draft");
        reopened.GetProperty("reopenReason").GetString().ShouldBe("Yanlış puan girilmiş");
        await Score(lead, id, "NoHire", 2, 2);
        (await lead.GetJson($"/api/v1/scorecards/{id}")).GetProperty("recommendation").GetString().ShouldBe("NoHire");
    }

    [Fact]
    public async Task Debrief_RequiresCompletedInterviewAndAllScorecards()
    {
        var hr = await Hr();
        var first = await Lead();
        var second = await Lead();
        var setup = await Interview(hr, first, [first, second]);
        var firstCard = await Start(first, setup.InterviewId);
        await Score(first, firstCard, "Hire", 4, 4);

        var notCompleted = await hr.Post($"/api/v1/interviews/{setup.InterviewId}/debrief", new { decision = "Proceed", reason = "Uygun" });
        notCompleted.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await hr.Post($"/api/v1/interviews/{setup.InterviewId}/outcome", new { outcome = "Completed" });
        var missing = await hr.Post($"/api/v1/interviews/{setup.InterviewId}/debrief", new { decision = "Proceed", reason = "Uygun" });
        missing.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await missing.Json()).GetProperty("detail").GetString().ShouldBe("Ortak karar için tüm mülakatçıların değerlendirmesini göndermesi gerekir.");

        await Score(second, await Start(second, setup.InterviewId), "Hire", 4, 3);
        (await hr.GetJson($"/api/v1/interviews/{setup.InterviewId}/scorecards")).GetProperty("canDebrief").GetBoolean().ShouldBeTrue();
        (await first.Post($"/api/v1/interviews/{setup.InterviewId}/debrief", new { decision = "Proceed", reason = "Uygun" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await hr.Post($"/api/v1/interviews/{setup.InterviewId}/debrief", new { decision = "Proceed", reason = " Final mülakata geçsin. " }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var debrief = (await hr.GetJson($"/api/v1/interviews/{setup.InterviewId}/scorecards")).GetProperty("debrief");
        debrief.GetProperty("decision").GetString().ShouldBe("Proceed");
        debrief.GetProperty("reason").GetString().ShouldBe("Final mülakata geçsin.");
        await factory.ProcessOutbox();
        (await hr.GetJson($"/api/v1/candidates/{setup.CandidateId}/timeline")).GetProperty("items").EnumerateArray()
            .Select(t => t.GetProperty("summary").GetString()).ShouldContain("Teknik mülakatı ortak kararı: Sonraki aşamaya geçsin.");
    }

    [Fact]
    public async Task Pipeline_InterviewStageNeedsScorecards_HrManagerCanBypassWithReason()
    {
        var hr = await Hr();
        var manager = await factory.LoginAs("İnsan Kaynakları", Role.HrManager);
        var lead = await Lead();
        var setup = await Interview(hr, lead, [lead]);
        var stages = (await hr.GetJson($"/api/v1/positions/{setup.PositionId}/pipeline")).GetProperty("stages").EnumerateArray()
            .ToDictionary(s => s.GetProperty("kind").GetString()!, s => s.GetProperty("id").GetGuid());
        Task<HttpResponseMessage> Move(ApiUser user, string kind, string? bypassReason = null) =>
            user.Post($"/api/v1/applications/{setup.ApplicationId}/move", new { stageId = stages[kind], bypassReason });

        await Move(hr, "HrScreening");
        await Move(hr, "CvReview");
        await lead.DecideCv(await hr.SuggestCv(setup.CandidateId, lead, setup.ApplicationId), "Positive");
        (await Move(hr, "TechnicalInterview")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var blocked = await Move(hr, "FinalInterview");
        blocked.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = await blocked.Json();
        problem.GetProperty("code").GetString().ShouldBe("Application.ScorecardsPending");
        problem.GetProperty("pendingCount").GetInt32().ShouldBe(1);
        problem.GetProperty("canBypass").GetBoolean().ShouldBeFalse();
        (await Move(hr, "FinalInterview", "Acil")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await (await Move(manager, "FinalInterview")).Json()).GetProperty("canBypass").GetBoolean().ShouldBeTrue();

        (await Move(manager, "FinalInterview", "Mülakatçı izinde, aday kaçmasın.")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await factory.ProcessOutbox();
        (await hr.GetJson($"/api/v1/candidates/{setup.CandidateId}/timeline")).GetProperty("items").EnumerateArray()
            .Select(t => t.GetProperty("summary").GetString())
            .ShouldContain(s => s!.EndsWith("(değerlendirmeler beklenmeden geçirildi: Mülakatçı izinde, aday kaçmasın.)"));

        // Değerlendirme gönderilince kural engel olmaz.
        await hr.Post($"/api/v1/applications/{setup.ApplicationId}/undo");
        await Score(lead, await Start(lead, setup.InterviewId), "Hire", 4, 4);
        (await Move(hr, "FinalInterview")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AwaitingMyScorecard_ListsInterviewsUntilSubmitted()
    {
        var hr = await Hr();
        var lead = await Lead();
        var setup = await Interview(hr, lead, [lead]);

        (await lead.GetJson("/api/v1/interviews?awaitingMyScorecard=true")).EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid()).ShouldContain(setup.InterviewId);
        await Score(lead, await Start(lead, setup.InterviewId), "Hire", 4, 4);
        (await lead.GetJson("/api/v1/interviews?awaitingMyScorecard=true")).EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid()).ShouldNotContain(setup.InterviewId);
    }

    [Fact]
    public async Task Comparison_RanksCandidates_AndHidesScoresFromInterviewerWhoHasNotSubmitted()
    {
        var hr = await Hr();
        var lead = await Lead();
        var other = await Lead();
        var strong = await Interview(hr, lead, [lead]);
        var weak = await Interview(hr, lead, [lead, other], positionId: strong.PositionId);
        await Score(lead, await Start(lead, strong.InterviewId), "StrongHire", 5, 5);
        await Score(lead, await Start(lead, weak.InterviewId), "NoHire", 2, 2);

        var forHr = await hr.GetJson($"/api/v1/positions/{strong.PositionId}/comparison");
        var rows = forHr.GetProperty("candidates").EnumerateArray().ToList();
        rows.Select(r => r.GetProperty("applicationId").GetGuid()).ShouldBe([strong.ApplicationId, weak.ApplicationId]);
        rows[0].GetProperty("averageScore").GetDecimal().ShouldBe(5m);
        rows[0].GetProperty("recommendations").GetProperty("StrongHire").GetInt32().ShouldBe(1);
        rows[1].GetProperty("submittedCount").GetInt32().ShouldBe(1);
        rows[1].GetProperty("expectedCount").GetInt32().ShouldBe(2);
        forHr.GetProperty("competencies").EnumerateArray().Select(c => c.GetProperty("name").GetString())
            .ShouldBe(["İletişim", "Teknik bilgi"]);

        var forOther = (await other.GetJson($"/api/v1/positions/{strong.PositionId}/comparison")).GetProperty("candidates").EnumerateArray().ToList();
        var hiddenRow = forOther.Single(r => r.GetProperty("applicationId").GetGuid() == weak.ApplicationId);
        hiddenRow.GetProperty("hidden").GetBoolean().ShouldBeTrue();
        hiddenRow.GetProperty("averageScore").ValueKind.ShouldBe(JsonValueKind.Null);
        forOther.Single(r => r.GetProperty("applicationId").GetGuid() == strong.ApplicationId)
            .GetProperty("averageScore").GetDecimal().ShouldBe(5m);
    }
}
