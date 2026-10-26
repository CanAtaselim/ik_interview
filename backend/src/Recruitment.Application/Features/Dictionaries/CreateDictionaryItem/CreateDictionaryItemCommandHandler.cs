using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Dictionaries;

namespace Recruitment.Application.Features.Dictionaries.CreateDictionaryItem;

internal sealed class CreateDictionaryItemCommandHandler(IAppDbContext db)
    : ICommandHandler<CreateDictionaryItemCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateDictionaryItemCommand command, CancellationToken ct)
    {
        var name = command.Name.Trim();
        var lowered = name.ToLowerInvariant();

        var sameType = db.DictionaryItems.Where(d => d.Type == command.Type);
        if (await sameType.AnyAsync(d => d.Name.ToLower() == lowered, ct))
            return DictionaryErrors.Duplicate;

        var lastOrder = await sameType.MaxAsync(d => (int?)d.SortOrder, ct) ?? 0;
        var item = DictionaryItem.Create(command.Type, name, lastOrder + 1);

        db.DictionaryItems.Add(item);
        await db.SaveChangesAsync(ct);
        return item.Id;
    }
}
