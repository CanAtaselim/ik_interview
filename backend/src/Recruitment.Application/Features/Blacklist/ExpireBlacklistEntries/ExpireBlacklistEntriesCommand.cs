using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Blacklist;

namespace Recruitment.Application.Features.Blacklist.ExpireBlacklistEntries;

/// <summary>Süresi dolan kara liste kayıtlarını sonlandırır. Günlük zamanlanmış işten çağrılır.</summary>
public sealed record ExpireBlacklistEntriesCommand : ICommand<int>;

internal sealed class ExpireBlacklistEntriesCommandHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<ExpireBlacklistEntriesCommand, int>
{
    public async Task<Result<int>> Handle(ExpireBlacklistEntriesCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = await db.BlacklistEntries
            .Where(e => e.Status == BlacklistStatus.Active && e.ExpiresAt != null && e.ExpiresAt <= now)
            .ToListAsync(ct);

        var expired = due.Count(entry => entry.ExpireIfDue(clock));
        await db.SaveChangesAsync(ct);
        return expired;
    }
}
