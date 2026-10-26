using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Dictionaries.DeleteDictionaryItem;

public sealed record DeleteDictionaryItemCommand(Guid Id) : ICommand<Unit>;

/// <summary>
/// Sözlük öğesini siler (soft delete). Herhangi bir kayıtta kullanılan öğe silinmez; kullanıcı önce
/// o kayıtları siler veya değiştirir. Silinen öğenin adı yeniden kullanılabilir.
/// </summary>
internal sealed class DeleteDictionaryItemCommandHandler(IAppDbContext db)
    : ICommandHandler<DeleteDictionaryItemCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteDictionaryItemCommand command, CancellationToken ct)
    {
        var item = await db.DictionaryItems.FirstOrDefaultAsync(d => d.Id == command.Id, ct);
        if (item is null)
            return DictionaryErrors.NotFound;
        if (DictionaryUsage.IsSystemItem(item))
            return DictionaryErrors.SystemItem;

        var usages = (await db.Of(item, ct))[item.Id];
        if (usages.Count > 0)
            return DictionaryErrors.InUse("silinemez", usages);

        db.DictionaryItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
