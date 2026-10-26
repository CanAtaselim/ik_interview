using Microsoft.EntityFrameworkCore;

namespace Recruitment.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Sorgunun istenen sayfasını ve toplam kayıt sayısını getirir; sınır dışı değerleri düzeltir.</summary>
    public static async Task<PagedResult<T>> ToPagedResult<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var totalCount = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<T>(items, totalCount, page, pageSize);
    }
}
