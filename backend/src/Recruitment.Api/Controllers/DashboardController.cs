using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Dashboard;

namespace Recruitment.Api.Controllers;

/// <summary>
/// Dashboard kartlarının verisi. Her tema ayrı endpoint'tir; arayüz kartları birbirini beklemeden doldurur.
/// Şimdilik yalnızca IK Yöneticisi; diğer roller açılırken veri bazlı kurallar (birim, maaş gizliliği) burada eklenir.
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Policy = Policies.HrManagerOnly)]
public sealed class DashboardController(IDispatcher dispatcher) : ControllerBase
{
    private static DashboardFilter Filter(DateOnly? from, DateOnly? to, Guid? departmentId, Guid? positionId) =>
        new(from, to, departmentId, positionId);

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        DateOnly? from, DateOnly? to, Guid? departmentId, Guid? positionId, CancellationToken ct) =>
        (await dispatcher.Query(new GetDashboardSummaryQuery(Filter(from, to, departmentId, positionId)), ct)).ToActionResult(this);

    [HttpGet("requests")]
    public async Task<IActionResult> Requests(
        DateOnly? from, DateOnly? to, Guid? departmentId, Guid? positionId, CancellationToken ct) =>
        (await dispatcher.Query(new GetDashboardRequestsQuery(Filter(from, to, departmentId, positionId)), ct)).ToActionResult(this);

    [HttpGet("candidates")]
    public async Task<IActionResult> Candidates(
        DateOnly? from, DateOnly? to, Guid? departmentId, Guid? positionId, CancellationToken ct) =>
        (await dispatcher.Query(new GetDashboardCandidatesQuery(Filter(from, to, departmentId, positionId)), ct)).ToActionResult(this);

    [HttpGet("pipeline")]
    public async Task<IActionResult> Pipeline(
        DateOnly? from, DateOnly? to, Guid? departmentId, Guid? positionId, CancellationToken ct) =>
        (await dispatcher.Query(new GetDashboardPipelineQuery(Filter(from, to, departmentId, positionId)), ct)).ToActionResult(this);

    [HttpGet("cv-reviews")]
    public async Task<IActionResult> CvReviews(
        DateOnly? from, DateOnly? to, Guid? departmentId, Guid? positionId, CancellationToken ct) =>
        (await dispatcher.Query(new GetDashboardCvReviewsQuery(Filter(from, to, departmentId, positionId)), ct)).ToActionResult(this);

    [HttpGet("interviews")]
    public async Task<IActionResult> Interviews(
        DateOnly? from, DateOnly? to, Guid? departmentId, Guid? positionId, CancellationToken ct) =>
        (await dispatcher.Query(new GetDashboardInterviewsQuery(Filter(from, to, departmentId, positionId)), ct)).ToActionResult(this);

    [HttpGet("scorecards")]
    public async Task<IActionResult> Scorecards(
        DateOnly? from, DateOnly? to, Guid? departmentId, Guid? positionId, CancellationToken ct) =>
        (await dispatcher.Query(new GetDashboardScorecardsQuery(Filter(from, to, departmentId, positionId)), ct)).ToActionResult(this);

    [HttpGet("blacklist")]
    public async Task<IActionResult> Blacklist(
        DateOnly? from, DateOnly? to, Guid? departmentId, Guid? positionId, CancellationToken ct) =>
        (await dispatcher.Query(new GetDashboardBlacklistQuery(Filter(from, to, departmentId, positionId)), ct)).ToActionResult(this);
}
