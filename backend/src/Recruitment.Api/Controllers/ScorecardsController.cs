using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Scorecards.GetInterviewScorecards;
using Recruitment.Application.Features.Scorecards.GetPositionComparison;
using Recruitment.Application.Features.Scorecards.GetScorecard;
using Recruitment.Application.Features.Scorecards.RecordDebrief;
using Recruitment.Application.Features.Scorecards.ReopenScorecard;
using Recruitment.Application.Features.Scorecards.StartScorecard;
using Recruitment.Application.Features.Scorecards.SubmitScorecard;
using Recruitment.Application.Features.Scorecards.UpdateScorecard;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Api.Controllers;

/// <summary>
/// Mülakat değerlendirmeleri. Kim neyi görür (bağımsız değerlendirme) handler'da uygulanır:
/// mülakatçı kendi değerlendirmesini göndermeden diğerlerini göremez.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize(Policy = Policies.CanInterview)]
public sealed class ScorecardsController(IDispatcher dispatcher) : ControllerBase
{
    public sealed record UpdateRequest(IReadOnlyList<ScorecardItemInput> Items, string? GeneralNote, Recommendation? Recommendation);

    public sealed record ReasonRequest(string Reason);

    public sealed record DebriefRequest(DebriefDecision Decision, string Reason);

    /// <summary>Mülakatçının bu mülakat için değerlendirmesini açar; yoksa oluşturur.</summary>
    [HttpPost("interviews/{interviewId:guid}/scorecard")]
    public async Task<IActionResult> Start(Guid interviewId, CancellationToken ct)
    {
        var result = await dispatcher.Send(new StartScorecardCommand(interviewId), ct);
        return result.IsFailure ? result.Error.ToProblemResult(this) : Ok(new { id = result.Value });
    }

    [HttpGet("interviews/{interviewId:guid}/scorecards")]
    public async Task<IActionResult> GetForInterview(Guid interviewId, CancellationToken ct) =>
        (await dispatcher.Query(new GetInterviewScorecardsQuery(interviewId), ct)).ToActionResult(this);

    [HttpPost("interviews/{interviewId:guid}/debrief")]
    [Authorize(Policy = Policies.HrOnly)]
    public async Task<IActionResult> Debrief(Guid interviewId, DebriefRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new RecordDebriefCommand(interviewId, request.Decision, request.Reason), ct)).ToActionResult(this);

    [HttpGet("scorecards/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        (await dispatcher.Query(new GetScorecardQuery(id), ct)).ToActionResult(this);

    [HttpPut("scorecards/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new UpdateScorecardCommand(id, request.Items, request.GeneralNote, request.Recommendation), ct))
        .ToActionResult(this);

    [HttpPost("scorecards/{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct) =>
        (await dispatcher.Send(new SubmitScorecardCommand(id), ct)).ToActionResult(this);

    [HttpPost("scorecards/{id:guid}/reopen")]
    [Authorize(Policy = Policies.HrManagerOnly)]
    public async Task<IActionResult> Reopen(Guid id, ReasonRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new ReopenScorecardCommand(id, request.Reason), ct)).ToActionResult(this);

    /// <summary>Pozisyondaki adayların değerlendirme puanlarıyla karşılaştırması.</summary>
    [HttpGet("positions/{positionId:guid}/comparison")]
    [Authorize(Policy = Policies.CanViewPositions)]
    public async Task<IActionResult> Compare(Guid positionId, CancellationToken ct) =>
        (await dispatcher.Query(new GetPositionComparisonQuery(positionId), ct)).ToActionResult(this);
}
