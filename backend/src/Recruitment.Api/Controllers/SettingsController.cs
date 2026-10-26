using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Settings.ApprovalFlow;
using Recruitment.Application.Features.Settings.EmailTemplates;
using Recruitment.Application.Features.Settings.Holidays;
using Recruitment.Application.Features.Settings.PipelineStages;
using Recruitment.Application.Features.Settings.RoleMappings;
using Recruitment.Domain.Users;

namespace Recruitment.Api.Controllers;

/// <summary>
/// Yönetim ayarları. Sistem Admin ve İK Yöneticisi e-posta şablonlarını, onay akışını, süreç
/// aşamalarını ve resmi tatilleri; yalnızca Sistem Admin AD grubu → rol eşlemesini yönetir.
/// </summary>
[ApiController]
[Route("api/v1/settings")]
[Authorize(Policy = Policies.CanManageSettings)]
public sealed class SettingsController(IDispatcher dispatcher) : ControllerBase
{
    public sealed record EmailTemplateRequest(string Subject, string Body);

    public sealed record ApprovalFlowRequest(IReadOnlyList<Role> Roles);

    public sealed record PipelineStagesRequest(IReadOnlyList<StageEditInput> Stages);

    public sealed record RoleMappingRequest(string GroupName, Role Role);

    public sealed record HolidayRequest(string Name, DateOnly Date, bool Recurring);

    [HttpGet("email-templates")]
    public async Task<IActionResult> GetEmailTemplates(CancellationToken ct) =>
        (await dispatcher.Query(new GetEmailTemplatesQuery(), ct)).ToActionResult(this);

    [HttpPut("email-templates/{id:guid}")]
    public async Task<IActionResult> UpdateEmailTemplate(Guid id, EmailTemplateRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new UpdateEmailTemplateCommand(id, request.Subject, request.Body), ct)).ToActionResult(this);

    [HttpPost("email-templates/{id:guid}/reset")]
    public async Task<IActionResult> ResetEmailTemplate(Guid id, CancellationToken ct) =>
        (await dispatcher.Send(new ResetEmailTemplateCommand(id), ct)).ToActionResult(this);

    [HttpPost("email-templates/{id:guid}/preview")]
    public async Task<IActionResult> PreviewEmailTemplate(Guid id, EmailTemplateRequest request, CancellationToken ct) =>
        (await dispatcher.Query(new PreviewEmailTemplateQuery(id, request.Subject, request.Body), ct)).ToActionResult(this);

    [HttpGet("approval-flow")]
    public async Task<IActionResult> GetApprovalFlow(CancellationToken ct) =>
        (await dispatcher.Query(new GetApprovalFlowQuery(), ct)).ToActionResult(this);

    [HttpPut("approval-flow")]
    public async Task<IActionResult> UpdateApprovalFlow(ApprovalFlowRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new UpdateApprovalFlowCommand(request.Roles), ct)).ToActionResult(this);

    [HttpGet("pipeline-stages")]
    public async Task<IActionResult> GetPipelineStages(CancellationToken ct) =>
        (await dispatcher.Query(new GetPipelineSettingsQuery(), ct)).ToActionResult(this);

    [HttpPut("pipeline-stages")]
    public async Task<IActionResult> UpdatePipelineStages(PipelineStagesRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new UpdatePipelineStagesCommand(request.Stages), ct)).ToActionResult(this);

    [HttpGet("holidays")]
    public async Task<IActionResult> GetHolidays([FromQuery] int? year, CancellationToken ct) =>
        (await dispatcher.Query(new GetHolidaysQuery(year ?? DateTime.UtcNow.Year), ct)).ToActionResult(this);

    [HttpPost("holidays")]
    public async Task<IActionResult> CreateHoliday(HolidayRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new CreateHolidayCommand(request.Name, request.Date, request.Recurring), ct)).ToCreatedResult(this);

    [HttpDelete("holidays/{id:guid}")]
    public async Task<IActionResult> DeleteHoliday(Guid id, CancellationToken ct) =>
        (await dispatcher.Send(new DeleteHolidayCommand(id), ct)).ToActionResult(this);

    [HttpGet("role-mappings")]
    [Authorize(Policy = Policies.SystemAdmin)]
    public async Task<IActionResult> GetRoleMappings(CancellationToken ct) =>
        (await dispatcher.Query(new GetRoleMappingsQuery(), ct)).ToActionResult(this);

    [HttpPost("role-mappings")]
    [Authorize(Policy = Policies.SystemAdmin)]
    public async Task<IActionResult> CreateRoleMapping(RoleMappingRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new CreateRoleMappingCommand(request.GroupName, request.Role), ct)).ToCreatedResult(this);

    [HttpDelete("role-mappings/{id:guid}")]
    [Authorize(Policy = Policies.SystemAdmin)]
    public async Task<IActionResult> DeleteRoleMapping(Guid id, CancellationToken ct) =>
        (await dispatcher.Send(new DeleteRoleMappingCommand(id), ct)).ToActionResult(this);
}
