using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Blacklist.ApproveBlacklistEntry;

public sealed record ApproveBlacklistEntryCommand(Guid Id) : ICommand<Unit>;

internal sealed class ApproveBlacklistEntryCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<ApproveBlacklistEntryCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ApproveBlacklistEntryCommand command, CancellationToken ct)
    {
        var entry = await db.BlacklistEntries.FirstOrDefaultAsync(e => e.Id == command.Id, ct);
        if (entry is null)
            return BlacklistErrors.NotFound;

        entry.Approve(currentUser.UserId, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
