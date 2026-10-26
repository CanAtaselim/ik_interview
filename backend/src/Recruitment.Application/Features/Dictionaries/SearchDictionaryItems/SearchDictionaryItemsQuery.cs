using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Dictionaries;

namespace Recruitment.Application.Features.Dictionaries.SearchDictionaryItems;

/// <summary>Yönetim ekranındaki satır: öğe, kullanım dökümü ve sistemce korunup korunmadığı.</summary>
public sealed record DictionaryAdminItemDto(
    Guid Id,
    string Name,
    int SortOrder,
    bool IsActive,
    int UsageCount,
    IReadOnlyList<DictionaryUsageDto> Usages,
    bool IsSystem);

/// <param name="Search">Ad içinde, büyük/küçük harf ayırmadan aranır.</param>
/// <param name="IsActive">Verilirse yalnızca aktif (true) veya pasif (false) öğeler.</param>
public sealed record SearchDictionaryItemsQuery(
    DictionaryType Type,
    string? Search,
    bool? IsActive,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<DictionaryAdminItemDto>>;

internal sealed class SearchDictionaryItemsQueryHandler(IAppDbContext db)
    : IQueryHandler<SearchDictionaryItemsQuery, PagedResult<DictionaryAdminItemDto>>
{
    public async Task<Result<PagedResult<DictionaryAdminItemDto>>> Handle(
        SearchDictionaryItemsQuery query, CancellationToken ct)
    {
        var items = db.DictionaryItems.AsNoTracking().Where(d => d.Type == query.Type);

        if (query.IsActive is { } isActive)
            items = items.Where(d => d.IsActive == isActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            items = items.Where(d => d.Name.ToLower().Contains(term));
        }

        var page = await items
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .ToPagedResult(query.Page, query.PageSize, ct);

        var usages = await db.UsagesOf(
            query.Type, [.. page.Items.Select(i => new DictionaryUsage.ItemKey(i.Id, i.Name))], ct);

        var rows = page.Items
            .Select(i => new DictionaryAdminItemDto(
                i.Id, i.Name, i.SortOrder, i.IsActive, usages[i.Id].Sum(u => u.Count), usages[i.Id], DictionaryUsage.IsSystemItem(i)))
            .ToList();
        return new PagedResult<DictionaryAdminItemDto>(rows, page.TotalCount, page.Page, page.PageSize);
    }
}
