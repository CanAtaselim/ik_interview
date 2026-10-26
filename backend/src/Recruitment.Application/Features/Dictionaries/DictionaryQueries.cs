using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Dictionaries;

namespace Recruitment.Application.Features.Dictionaries;

internal static class DictionaryQueries
{
    /// <summary>Verilen kimlik, istenen türde ve seçilebilir (aktif) bir sözlük öğesi mi?</summary>
    public static Task<bool> IsSelectable(
        this IAppDbContext db, Guid id, DictionaryType type, CancellationToken ct) =>
        db.DictionaryItems.AnyAsync(d => d.Id == id && d.Type == type && d.IsActive, ct);

    /// <summary>Verilen kimliklerin adları; pasif öğeler de dahildir (eski kayıtlar adını göstersin).</summary>
    public static Task<Dictionary<Guid, string>> ItemNames(
        this IAppDbContext db, IEnumerable<Guid> ids, CancellationToken ct) =>
        db.DictionaryItems.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);
}
