using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Pipeline.CloseApplication;
using Recruitment.Application.Features.Pipeline.GetCandidateApplications;
using Recruitment.Application.Features.Pipeline.GetPositionPipeline;
using Recruitment.Application.Features.Pipeline.MoveApplication;
using Recruitment.Application.Features.Pipeline.SearchActiveApplications;
using Recruitment.Application.Features.Pipeline.StartApplication;
using Recruitment.Application.Features.Pipeline.UndoApplicationChange;
using Recruitment.Domain.Pipeline;

namespace Recruitment.Api.Controllers;

/// <summary>Süreç hattı: adayın pozisyondaki süreci ve aşama geçişleri. Yalnızca İK.</summary>
[ApiController]
[Route("api/v1")]
[Authorize(Policy = Policies.HrOnly)]
public sealed class ApplicationsController(IDispatcher dispatcher) : ControllerBase
{
    /// <param name="BypassReason">İK Yöneticisinin bekleyen değerlendirmelere rağmen geçirme gerekçesi.</param>
    public sealed record MoveRequest(Guid StageId, string? BypassReason = null);

    public sealed record CloseRequest(ApplicationStatus Status, Guid RejectionReasonId, string? Note);

    [HttpGet("positions/{positionId:guid}/pipeline")]
    public async Task<IActionResult> GetPositionPipeline(Guid positionId, CancellationToken ct) =>
        (await dispatcher.Query(new GetPositionPipelineQuery(positionId), ct)).ToActionResult(this);

    [HttpGet("candidates/{candidateId:guid}/applications")]
    public async Task<IActionResult> GetCandidateApplications(Guid candidateId, CancellationToken ct) =>
        (await dispatcher.Query(new GetCandidateApplicationsQuery(candidateId), ct)).ToActionResult(this);

    /// <summary>Devam eden süreçleri aday adıyla arar (takvimden mülakat planlama).</summary>
    [HttpGet("applications/active")]
    public async Task<IActionResult> SearchActive([FromQuery] string? search, CancellationToken ct) =>
        (await dispatcher.Query(new SearchActiveApplicationsQuery(search), ct)).ToActionResult(this);

    [HttpPost("applications")]
    public async Task<IActionResult> Start(StartApplicationCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command, ct)).ToCreatedResult(this);

    [HttpPost("applications/{id:guid}/move")]
    public async Task<IActionResult> Move(Guid id, MoveRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new MoveApplicationCommand(id, request.StageId, request.BypassReason), ct)).ToActionResult(this);

    [HttpPost("applications/{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CloseRequest request, CancellationToken ct)
    {
        var command = new CloseApplicationCommand(id, request.Status, request.RejectionReasonId, request.Note);
        return (await dispatcher.Send(command, ct)).ToActionResult(this);
    }

    [HttpPost("applications/{id:guid}/undo")]
    public async Task<IActionResult> Undo(Guid id, CancellationToken ct) =>
        (await dispatcher.Send(new UndoApplicationChangeCommand(id), ct)).ToActionResult(this);
}
