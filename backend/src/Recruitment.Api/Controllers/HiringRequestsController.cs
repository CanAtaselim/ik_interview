using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.HiringRequests.ApproveHiringRequest;
using Recruitment.Application.Features.HiringRequests.CancelHiringRequest;
using Recruitment.Application.Features.HiringRequests.CreateHiringRequest;
using Recruitment.Application.Features.HiringRequests.GetHiringRequestDetail;
using Recruitment.Application.Features.HiringRequests.GetHiringRequests;
using Recruitment.Application.Features.HiringRequests.RejectHiringRequest;
using Recruitment.Application.Features.HiringRequests.SubmitHiringRequest;
using Recruitment.Application.Features.HiringRequests.UpdateHiringRequest;
using Recruitment.Domain.HiringRequests;

namespace Recruitment.Api.Controllers;

/// <summary>
/// Kadro talepleri. Politikalar role bakar; "bu talep benim mi, sıradaki onay bende mi" gibi
/// veri bazlı kurallar handler'da uygulanır.
/// </summary>
[ApiController]
[Route("api/v1/hiring-requests")]
[Authorize(Policy = Policies.CanViewHiringRequests)]
public sealed class HiringRequestsController(IDispatcher dispatcher) : ControllerBase
{
    public sealed record ApproveRequest(string? Note);

    public sealed record RejectRequest(string Reason);

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] HiringRequestStatus? status,
        [FromQuery] string? search,
        [FromQuery] bool awaitingMyDecision,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize)
    {
        var query = new GetHiringRequestsQuery(status, search, awaitingMyDecision, page, pageSize);
        return (await dispatcher.Query(query, ct)).ToActionResult(this);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct) =>
        (await dispatcher.Query(new GetHiringRequestDetailQuery(id), ct)).ToActionResult(this);

    [HttpPost]
    [Authorize(Policy = Policies.CanRequestHeadcount)]
    public async Task<IActionResult> Create(CreateHiringRequestCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command, ct)).ToCreatedResult(this);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanRequestHeadcount)]
    public async Task<IActionResult> Update(Guid id, UpdateHiringRequestCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command with { Id = id }, ct)).ToActionResult(this);

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = Policies.CanRequestHeadcount)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct) =>
        (await dispatcher.Send(new SubmitHiringRequestCommand(id), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Policies.CanApproveRequest)]
    public async Task<IActionResult> Approve(Guid id, ApproveRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new ApproveHiringRequestCommand(id, request.Note), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = Policies.CanApproveRequest)]
    public async Task<IActionResult> Reject(Guid id, RejectRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new RejectHiringRequestCommand(id, request.Reason), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        (await dispatcher.Send(new CancelHiringRequestCommand(id), ct)).ToActionResult(this);
}
