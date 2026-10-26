using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Positions.ChangePositionStatus;
using Recruitment.Application.Features.Positions.CreatePosition;
using Recruitment.Application.Features.Positions.GetPositionDetail;
using Recruitment.Application.Features.Positions.GetPositions;
using Recruitment.Application.Features.Positions.SetPositionInterviewTemplates;
using Recruitment.Application.Features.Positions.UpdatePosition;
using Recruitment.Domain.Positions;

namespace Recruitment.Api.Controllers;

[ApiController]
[Route("api/v1/positions")]
[Authorize(Policy = Policies.CanViewPositions)]
public sealed class PositionsController(IDispatcher dispatcher) : ControllerBase
{
    public sealed record ChangeStatusRequest(PositionStatus Status);

    public sealed record TemplatesRequest(IReadOnlyList<Guid> TemplateIds);

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] PositionStatus? status,
        [FromQuery] string? search,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize) =>
        (await dispatcher.Query(new GetPositionsQuery(status, search, page, pageSize), ct)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct) =>
        (await dispatcher.Query(new GetPositionDetailQuery(id), ct)).ToActionResult(this);

    [HttpPost]
    [Authorize(Policy = Policies.HrOnly)]
    public async Task<IActionResult> Create(CreatePositionCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command, ct)).ToCreatedResult(this);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.HrOnly)]
    public async Task<IActionResult> Update(Guid id, UpdatePositionCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command with { Id = id }, ct)).ToActionResult(this);

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = Policies.HrOnly)]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeStatusRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new ChangePositionStatusCommand(id, request.Status), ct)).ToActionResult(this);

    /// <summary>Pozisyonun mülakat soru şablonları.</summary>
    [HttpPut("{id:guid}/interview-templates")]
    [Authorize(Policy = Policies.HrOnly)]
    public async Task<IActionResult> SetInterviewTemplates(Guid id, TemplatesRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new SetPositionInterviewTemplatesCommand(id, request.TemplateIds), ct)).ToActionResult(this);
}
