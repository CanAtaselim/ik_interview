using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Blacklist.ApproveBlacklistEntry;
using Recruitment.Application.Features.Blacklist.FindBlacklistMatches;
using Recruitment.Application.Features.Blacklist.RemoveBlacklistEntry;
using Recruitment.Application.Features.Blacklist.RequestBlacklistEntry;
using Recruitment.Application.Features.Blacklist.ViewBlacklistEntries;
using Recruitment.Application.Features.Blacklist.ViewBlacklistEntry;
using Recruitment.Domain.Blacklist;

namespace Recruitment.Api.Controllers;

/// <summary>
/// Kara liste. Kayıtlar ve nedenleri yalnızca İK ve Sistem Admin tarafından görülür; her
/// görüntüleme erişim kaydına yazılır. Onay ve çıkarma yalnızca İK Yöneticisindedir.
/// </summary>
[ApiController]
[Route("api/v1/blacklist-entries")]
[Authorize(Policy = Policies.CanViewBlacklist)]
public sealed class BlacklistController(IDispatcher dispatcher) : ControllerBase
{
    public sealed record RemoveRequest(string Reason);

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] BlacklistStatus? status,
        [FromQuery] string? search,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize) =>
        (await dispatcher.Send(new ViewBlacklistEntriesCommand(status, search, page, pageSize), ct)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct) =>
        (await dispatcher.Send(new ViewBlacklistEntryCommand(id), ct)).ToActionResult(this);

    /// <summary>Verilen kişi kara listedeki biriyle eşleşiyor mu? (Aday formu yazılırken çağrılır.)</summary>
    [HttpGet("matches")]
    [Authorize(Policy = Policies.HrOnly)]
    public async Task<IActionResult> FindMatches(
        [FromQuery] string? email,
        [FromQuery] string? phone,
        [FromQuery] string? firstName,
        [FromQuery] string? lastName,
        [FromQuery] Guid? excludeCandidateId,
        CancellationToken ct)
    {
        var query = new FindBlacklistMatchesQuery(email, phone, firstName, lastName, excludeCandidateId);
        return (await dispatcher.Query(query, ct)).ToActionResult(this);
    }

    [HttpPost]
    [Authorize(Policy = Policies.HrOnly)]
    public async Task<IActionResult> Create(RequestBlacklistEntryCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command, ct)).ToCreatedResult(this);

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Policies.HrManagerOnly)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) =>
        (await dispatcher.Send(new ApproveBlacklistEntryCommand(id), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/remove")]
    [Authorize(Policy = Policies.HrManagerOnly)]
    public async Task<IActionResult> Remove(Guid id, RemoveRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new RemoveBlacklistEntryCommand(id, request.Reason), ct)).ToActionResult(this);
}
