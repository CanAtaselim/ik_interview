using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Dictionaries.GetDictionaryItems;

internal sealed class GetDictionaryItemsQueryHandler(IAppDbContext db)
    : IQueryHandler<GetDictionaryItemsQuery, IReadOnlyList<DictionaryItemDto>>
{
    // Sözlükler kısa seçim listeleridir; bu yüzden sayfalanmaz.
    public async Task<Result<IReadOnlyList<DictionaryItemDto>>> Handle(GetDictionaryItemsQuery query, CancellationToken ct)
    {
        var items = await db.DictionaryItems.AsNoTracking()
            .Where(d => d.Type == query.Type && (query.IncludeInactive || d.IsActive))
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .Select(d => new DictionaryItemDto(d.Id, d.Name, d.SortOrder, d.IsActive))
            .ToListAsync(ct);

        return items;
    }
}
