using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Interviews.CancelInterview;
using Recruitment.Application.Features.Interviews.CreateInterviewDocumentLink;
using Recruitment.Application.Features.Interviews.GetInterviewDetail;
using Recruitment.Application.Features.Interviews.GetInterviewerSchedules;
using Recruitment.Application.Features.Interviews.GetInterviews;
using Recruitment.Application.Features.Interviews.RecordInterviewOutcome;
using Recruitment.Application.Features.Interviews.RescheduleInterview;
using Recruitment.Application.Features.Interviews.ScheduleInterview;
using Recruitment.Domain.Interviews;

namespace Recruitment.Api.Controllers;

/// <summary>
/// Mülakat planlama. Planlama, değişiklik ve sonuç girişi İK'dadır; mülakatçılar yalnızca
/// atandıkları mülakatları görür (handler'da süzülür).
/// </summary>
[ApiController]
[Route("api/v1/interviews")]
[Authorize(Policy = Policies.CanInterview)]
public sealed class InterviewsController(IDispatcher dispatcher) : ControllerBase
{
    public sealed record CancelRequest(string Reason);

    public sealed record OutcomeRequest(InterviewStatus Outcome);

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? interviewerUserId,
        [FromQuery] Guid? roomId,
        [FromQuery] Guid? candidateId,
        [FromQuery] Guid? positionId,
        [FromQuery] InterviewStatus? status,
        [FromQuery] bool mine,
        [FromQuery] bool awaitingOutcome,
        [FromQuery] bool awaitingMyScorecard,
        CancellationToken ct)
    {
        var query = new GetInterviewsQuery(
            from, to, interviewerUserId, roomId, candidateId, positionId, status, mine, awaitingOutcome, awaitingMyScorecard);
        return (await dispatcher.Query(query, ct)).ToActionResult(this);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct) =>
        (await dispatcher.Query(new GetInterviewDetailQuery(id), ct)).ToActionResult(this);

    /// <summary>Planlama için mülakatçıların müsaitlikleri ve dolu saatleri.</summary>
    [HttpGet("schedules")]
    [Authorize(Policy = Policies.HrOnly)]
    public async Task<IActionResult> GetSchedules(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] Guid[]? userIds,
        [FromQuery] Guid? roomId,
        [FromQuery] Guid? excludeInterviewId,
        CancellationToken ct)
    {
        var query = new GetInterviewerSchedulesQuery(from, to, userIds ?? [], roomId, excludeInterviewId);
        return (await dispatcher.Query(query, ct)).ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Policy = Policies.HrOnly)]
    public async Task<IActionResult> Schedule(ScheduleInterviewCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command, ct)).ToCreatedResult(this);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.HrOnly)]
    public async Task<IActionResult> Reschedule(Guid id, RescheduleInterviewCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command with { Id = id }, ct)).ToActionResult(this);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.HrOnly)]
    public async Task<IActionResult> Cancel(Guid id, CancelRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new CancelInterviewCommand(id, request.Reason), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/outcome")]
    [Authorize(Policy = Policies.HrOnly)]
    public async Task<IActionResult> RecordOutcome(Guid id, OutcomeRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new RecordInterviewOutcomeCommand(id, request.Outcome), ct)).ToActionResult(this);

    /// <summary>Adayın dosyası için kısa süreli indirme bağlantısı (mülakatı görebilen kullanıcıya).</summary>
    [HttpPost("{id:guid}/documents/{documentId:guid}/download-link")]
    public async Task<IActionResult> CreateDocumentLink(Guid id, Guid documentId, CancellationToken ct)
    {
        var result = await dispatcher.Send(new CreateInterviewDocumentLinkCommand(id, documentId), ct);
        if (result.IsFailure)
            return result.Error.ToProblemResult(this);

        var url = Url.Action(nameof(FilesController.Download), "Files", new { token = result.Value.Token })!;
        return Ok(new CandidatesController.DownloadLinkResponse(url, result.Value.ExpiresAt));
    }
}
