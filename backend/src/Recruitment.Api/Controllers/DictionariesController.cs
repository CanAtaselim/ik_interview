using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries.CreateDictionaryItem;
using Recruitment.Application.Features.Dictionaries.DeleteDictionaryItem;
using Recruitment.Application.Features.Dictionaries.SearchDictionaryItems;
using Recruitment.Application.Features.Dictionaries.GetDictionaryItems;
using Recruitment.Application.Features.Dictionaries.UpdateDictionaryItem;
using Recruitment.Domain.Dictionaries;

namespace Recruitment.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class DictionariesController(IDispatcher dispatcher) : ControllerBase
{
    public sealed record CreateItemRequest(string Name);

    public sealed record UpdateItemRequest(string Name, int SortOrder, bool IsActive);

    [HttpGet("dictionaries/{type}/items")]
    public async Task<IActionResult> GetItems(
        DictionaryType type, [FromQuery] bool includeInactive, CancellationToken ct)
    {
        var result = await dispatcher.Query(new GetDictionaryItemsQuery(type, includeInactive), ct);
        return result.ToActionResult(this);
    }

    /// <summary>Yönetim ekranı listesi: sayfalı, aramalı, kullanım sayılarıyla.</summary>
    [HttpGet("dictionaries/{type}/admin-items")]
    [Authorize(Policy = Policies.CanManageDictionaries)]
    public async Task<IActionResult> SearchItems(
        DictionaryType type,
        CancellationToken ct,
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize) =>
        (await dispatcher.Query(new SearchDictionaryItemsQuery(type, search, isActive, page, pageSize), ct))
        .ToActionResult(this);

    [HttpPost("dictionaries/{type}/items")]
    [Authorize(Policy = Policies.CanManageDictionaries)]
    public async Task<IActionResult> CreateItem(DictionaryType type, CreateItemRequest request, CancellationToken ct)
    {
        var result = await dispatcher.Send(new CreateDictionaryItemCommand(type, request.Name), ct);
        return result.ToCreatedResult(this);
    }

    [HttpPut("dictionary-items/{id:guid}")]
    [Authorize(Policy = Policies.CanManageDictionaries)]
    public async Task<IActionResult> UpdateItem(Guid id, UpdateItemRequest request, CancellationToken ct)
    {
        var command = new UpdateDictionaryItemCommand(id, request.Name, request.SortOrder, request.IsActive);
        var result = await dispatcher.Send(command, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("dictionary-items/{id:guid}")]
    [Authorize(Policy = Policies.CanManageDictionaries)]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken ct) =>
        (await dispatcher.Send(new DeleteDictionaryItemCommand(id), ct)).ToActionResult(this);
}
