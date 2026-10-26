using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Dictionaries.UpdateDictionaryItem;

internal sealed class UpdateDictionaryItemCommandHandler(IAppDbContext db)
    : ICommandHandler<UpdateDictionaryItemCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateDictionaryItemCommand command, CancellationToken ct)
    {
        var item = await db.DictionaryItems.FirstOrDefaultAsync(d => d.Id == command.Id, ct);
        if (item is null)
            return DictionaryErrors.NotFound;

        var lowered = command.Name.Trim().ToLowerInvariant();
        var nameTaken = await db.DictionaryItems.AnyAsync(
            d => d.Type == item.Type && d.Id != item.Id && d.Name.ToLower() == lowered, ct);
        if (nameTaken)
            return DictionaryErrors.Duplicate;

        // Kullanılan öğenin adı değişmez; eski kayıtlar başka bir adla görünmesin. Sıra ve aktiflik serbest.
        if (!string.Equals(item.Name, command.Name.Trim(), StringComparison.Ordinal))
        {
            if (DictionaryUsage.IsSystemItem(item))
                return DictionaryErrors.SystemItem;

            var usages = (await db.Of(item, ct))[item.Id];
            if (usages.Count > 0)
                return DictionaryErrors.InUse("adı değiştirilemez", usages);
        }

        item.Update(command.Name, command.SortOrder, command.IsActive);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
