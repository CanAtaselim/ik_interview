using System.Net;
using System.Text.Json;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Pipeline;

public sealed class PipelineEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Department = "Yazılım";

    private Task<ApiUser> Hr() => factory.LoginAs(Department, Role.HrSpecialist);

    private static async Task<Guid> CreatePosition(ApiUser hr, string title = "Backend Geliştirici") =>
        await hr.Create("/api/v1/positions", new
        {
            title,
            departmentId = await hr.DictionaryItemId("Department", Department),
            levelId = await hr.DictionaryItemId("Level", "L3"),
            headcount = 2,
        });

    private static Task<Guid> CreateCandidate(ApiUser hr, string firstName = "Deniz") =>
        hr.Create("/api/v1/candidates", new
        {
            firstName,
            lastName = $"Aday{Guid.NewGuid():N}",
            email = $"{Guid.NewGuid():N}@ornek.com",
        });

    private static Task<Guid> Start(ApiUser hr, Guid candidateId, Guid positionId) =>
        hr.Create("/api/v1/applications", new { candidateId, positionId });

    private static async Task<Dictionary<string, Guid>> Stages(ApiUser hr, Guid positionId) =>
        (await hr.GetJson($"/api/v1/positions/{positionId}/pipeline")).GetProperty("stages").EnumerateArray()
        .ToDictionary(s => s.GetProperty("kind").GetString()!, s => s.GetProperty("id").GetGuid());

    /// <summary>"CV değerlendirme" aşamasından çıkış koşulu: bir takım liderinin olumlu kararı.</summary>
    private async Task ApproveCv(ApiUser hr, Guid candidateId, Guid applicationId)
    {
        var lead = await factory.LoginAs(Department, Role.TeamLead);
        var reviewId = await hr.SuggestCv(candidateId, lead, applicationId);
        (await lead.DecideCv(reviewId, "Positive")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static Task<HttpResponseMessage> Move(ApiUser hr, Guid applicationId, Guid stageId) =>
        hr.Post($"/api/v1/applications/{applicationId}/move", new { stageId });

    private static async Task<JsonElement> Card(ApiUser hr, Guid positionId, Guid applicationId, string list = "active") =>
        (await hr.GetJson($"/api/v1/positions/{positionId}/pipeline")).GetProperty(list).EnumerateArray()
        .Single(c => c.GetProperty("applicationId").GetGuid() == applicationId);

    [Fact]
    public async Task Start_PutsCandidateInFirstStageOfThePositionBoard()
    {
        var hr = await Hr();
        var positionId = await CreatePosition(hr);
        var candidateId = await CreateCandidate(hr);

        var applicationId = await Start(hr, candidateId, positionId);

        var board = await hr.GetJson($"/api/v1/positions/{positionId}/pipeline");
        board.GetProperty("positionTitle").GetString().ShouldBe("Backend Geliştirici");
        board.GetProperty("stages").EnumerateArray().Select(s => s.GetProperty("name").GetString()).ShouldBe(
        [
            "Başvuru", "İK ön görüşme", "CV değerlendirme", "Teknik mülakat", "Case / ödev", "Final mülakat",
            "Referans kontrolü", "Teklif", "İşe alındı",
        ]);
        var card = board.GetProperty("active").EnumerateArray().Single();
        card.GetProperty("applicationId").GetGuid().ShouldBe(applicationId);
        card.GetProperty("candidateId").GetGuid().ShouldBe(candidateId);
        card.GetProperty("candidateName").GetString()!.ShouldStartWith("Deniz ");
        card.GetProperty("stageId").GetGuid().ShouldBe((await Stages(hr, positionId))["Applied"]);
        card.GetProperty("status").GetString().ShouldBe("Active");
        card.GetProperty("canUndo").GetBoolean().ShouldBeFalse();
        board.GetProperty("closed").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Start_WhenAlreadyInProcess_Returns409_ButOtherPositionIsAllowed()
    {
        var hr = await Hr();
        var first = await CreatePosition(hr);
        var second = await CreatePosition(hr, "Veri Mühendisi");
        var candidateId = await CreateCandidate(hr);
        await Start(hr, candidateId, first);

        var again = await hr.Post("/api/v1/applications", new { candidateId, positionId = first });
        var other = await hr.Post("/api/v1/applications", new { candidateId, positionId = second });

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.Json()).GetProperty("detail").GetString().ShouldBe("Adayın bu pozisyonda devam eden bir süreci zaten var.");
        other.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await hr.GetJson($"/api/v1/candidates/{candidateId}/applications")).EnumerateArray()
            .Select(a => a.GetProperty("positionTitle").GetString())
            .ShouldBe(["Veri Mühendisi", "Backend Geliştirici"]);
    }

    [Fact]
    public async Task Start_OnPositionThatIsNotOpen_Returns422_AndUnknownIdsReturn404()
    {
        var hr = await Hr();
        var positionId = await CreatePosition(hr);
        var candidateId = await CreateCandidate(hr);
        await hr.Post($"/api/v1/positions/{positionId}/status", new { status = "OnHold" });

        var onHold = await hr.Post("/api/v1/applications", new { candidateId, positionId });
        var unknownCandidate = await hr.Post("/api/v1/applications", new { candidateId = Guid.NewGuid(), positionId });
        var unknownPosition = await hr.Post("/api/v1/applications", new { candidateId, positionId = Guid.NewGuid() });

        onHold.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await onHold.Json()).GetProperty("detail").GetString().ShouldBe("Yalnızca açık pozisyona aday eklenebilir.");
        unknownCandidate.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownPosition.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Move_FollowsStageOrder_AndExplainsInvalidMoves()
    {
        var hr = await Hr();
        var positionId = await CreatePosition(hr);
        var candidateId = await CreateCandidate(hr);
        var applicationId = await Start(hr, candidateId, positionId);
        var stages = await Stages(hr, positionId);

        var skipRequired = await Move(hr, applicationId, stages["CvReview"]);
        (await Move(hr, applicationId, stages["HrScreening"])).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Move(hr, applicationId, stages["CvReview"])).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ApproveCv(hr, candidateId, applicationId);
        (await Move(hr, applicationId, stages["TechnicalInterview"])).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        // Opsiyonel "Case / ödev" atlanabilir.
        (await Move(hr, applicationId, stages["FinalInterview"])).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var backwards = await Move(hr, applicationId, stages["HrScreening"]);
        var unknownStage = await Move(hr, applicationId, Guid.NewGuid());

        skipRequired.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await skipRequired.Json()).GetProperty("detail").GetString()
            .ShouldBe("“İK ön görüşme” aşaması atlanamaz; aday önce o aşamadan geçmeli.");
        backwards.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        unknownStage.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var card = await Card(hr, positionId, applicationId);
        card.GetProperty("stageId").GetGuid().ShouldBe(stages["FinalInterview"]);
        card.GetProperty("canUndo").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Close_RequiresReason_ThenMovesCardToClosedList()
    {
        var hr = await Hr();
        var positionId = await CreatePosition(hr);
        var candidateId = await CreateCandidate(hr);
        var applicationId = await Start(hr, candidateId, positionId);
        var reasonId = await hr.DictionaryItemId("RejectionReason", "Deneyim yetersiz");

        var noReason = await hr.Post($"/api/v1/applications/{applicationId}/close", new { status = "Rejected" });
        var invalidStatus = await hr.Post(
            $"/api/v1/applications/{applicationId}/close", new { status = "Hired", rejectionReasonId = reasonId });
        var closed = await hr.Post(
            $"/api/v1/applications/{applicationId}/close",
            new { status = "Rejected", rejectionReasonId = reasonId, note = "Beş yıl aranıyordu." });

        noReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await noReason.Json()).GetProperty("errors").GetProperty("rejectionReasonId")[0].GetString()
            .ShouldBe("Ret nedeni seçmelisiniz.");
        invalidStatus.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        closed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var card = await Card(hr, positionId, applicationId, "closed");
        card.GetProperty("status").GetString().ShouldBe("Rejected");
        card.GetProperty("closingReason").GetString().ShouldBe("Deneyim yetersiz");
        var listed = (await hr.GetJson($"/api/v1/candidates/{candidateId}/applications")).EnumerateArray().Single();
        listed.GetProperty("closingNote").GetString().ShouldBe("Beş yıl aranıyordu.");
        listed.GetProperty("stageName").GetString().ShouldBe("Başvuru");
        // Sonlanan süreç taşınamaz; aday aynı pozisyona yeniden eklenebilir.
        (await Move(hr, applicationId, (await Stages(hr, positionId))["HrScreening"])).StatusCode
            .ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await hr.Post("/api/v1/applications", new { candidateId, positionId })).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Undo_RevertsLastMove_AndReopensClosedApplication()
    {
        var hr = await Hr();
        var positionId = await CreatePosition(hr);
        var applicationId = await Start(hr, await CreateCandidate(hr), positionId);
        var stages = await Stages(hr, positionId);
        var reasonId = await hr.DictionaryItemId("RejectionReason", "Aday süreçten çekildi");

        (await hr.Post($"/api/v1/applications/{applicationId}/undo")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        await Move(hr, applicationId, stages["HrScreening"]);
        (await hr.Post($"/api/v1/applications/{applicationId}/undo")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Card(hr, positionId, applicationId)).GetProperty("stageId").GetGuid().ShouldBe(stages["Applied"]);
        // Geri alınan değişiklik bir kez daha geri alınamaz.
        (await hr.Post($"/api/v1/applications/{applicationId}/undo")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        await hr.Post($"/api/v1/applications/{applicationId}/close", new { status = "Withdrawn", rejectionReasonId = reasonId });
        (await hr.Post($"/api/v1/applications/{applicationId}/undo")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var card = await Card(hr, positionId, applicationId);
        card.GetProperty("status").GetString().ShouldBe("Active");
        card.GetProperty("closingReason").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Hire_IncreasesPositionFill_AndUndoTakesItBack()
    {
        var hr = await Hr();
        var positionId = await CreatePosition(hr);
        var candidateId = await CreateCandidate(hr);
        var applicationId = await Start(hr, candidateId, positionId);
        var stages = await Stages(hr, positionId);
        await ApproveCv(hr, candidateId, applicationId);
        foreach (var kind in new[] { "HrScreening", "CvReview", "TechnicalInterview", "FinalInterview", "Offer", "Hired" })
            (await Move(hr, applicationId, stages[kind])).StatusCode.ShouldBe(HttpStatusCode.NoContent, kind);
        await factory.ProcessOutbox();

        var hired = await Card(hr, positionId, applicationId);
        hired.GetProperty("status").GetString().ShouldBe("Hired");
        hired.GetProperty("stageId").GetGuid().ShouldBe(stages["Hired"]);
        (await hr.GetJson($"/api/v1/positions/{positionId}")).GetProperty("filledCount").GetInt32().ShouldBe(1);

        (await hr.Post($"/api/v1/applications/{applicationId}/undo")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await factory.ProcessOutbox();

        (await Card(hr, positionId, applicationId)).GetProperty("status").GetString().ShouldBe("Active");
        (await hr.GetJson($"/api/v1/positions/{positionId}")).GetProperty("filledCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Timeline_RecordsEveryStepWithPositionAndStageNames()
    {
        var hr = await Hr();
        var positionId = await CreatePosition(hr, "Ürün Yöneticisi");
        var candidateId = await CreateCandidate(hr);
        var applicationId = await Start(hr, candidateId, positionId);
        var stages = await Stages(hr, positionId);
        await Move(hr, applicationId, stages["HrScreening"]);
        await hr.Post($"/api/v1/applications/{applicationId}/undo");
        await hr.Post(
            $"/api/v1/applications/{applicationId}/close",
            new { status = "Rejected", rejectionReasonId = await hr.DictionaryItemId("RejectionReason", "Kültür uyumu") });
        await factory.ProcessOutbox();

        var summaries = (await hr.GetJson($"/api/v1/candidates/{candidateId}/timeline")).GetProperty("items")
            .EnumerateArray().Select(t => t.GetProperty("summary").GetString()).ToList();

        summaries.ShouldBe(
        [
            "“Ürün Yöneticisi” süreci olumsuz sonuçlandı: Kültür uyumu",
            "“Ürün Yöneticisi” sürecindeki son değişiklik geri alındı.",
            "“Ürün Yöneticisi”: Başvuru → İK ön görüşme",
            "“Ürün Yöneticisi” pozisyonu için süreç başlatıldı.",
            "Aday eklendi.",
        ]);
    }

    [Fact]
    public async Task Pipeline_IsForHrOnly()
    {
        var hr = await Hr();
        var teamLead = await factory.LoginAs(Department, Role.TeamLead);
        var positionId = await CreatePosition(hr);
        var applicationId = await Start(hr, await CreateCandidate(hr), positionId);

        (await teamLead.Client.GetAsync($"/api/v1/positions/{positionId}/pipeline")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await teamLead.Post($"/api/v1/applications/{applicationId}/undo")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await teamLead.Post("/api/v1/applications", new { candidateId = Guid.NewGuid(), positionId }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
