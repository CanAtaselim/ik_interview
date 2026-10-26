using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Candidates.SetTalentPool;

/// <summary>Adayı yetenek havuzuna alır ("havuzda beklet") veya havuzdan çıkarır.</summary>
public sealed record SetTalentPoolCommand(Guid CandidateId, bool InTalentPool) : ICommand<Unit>;

internal sealed class SetTalentPoolCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<SetTalentPoolCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SetTalentPoolCommand command, CancellationToken ct)
    {
        var candidate = await db.Candidates.FindAsync([command.CandidateId], ct);
        if (candidate is null)
            return CandidateErrors.NotFound;

        candidate.SetTalentPool(command.InTalentPool, currentUser.UserId, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
