using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Candidates.StartScreening;

/// <summary>Aday için boş bir ön görüşme kaydı açar; görüşmeyi yapan, isteği yapan İK'dır.</summary>
public sealed record StartScreeningCommand(Guid CandidateId) : ICommand<Guid>;

internal sealed class StartScreeningCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<StartScreeningCommand, Guid>
{
    public async Task<Result<Guid>> Handle(StartScreeningCommand command, CancellationToken ct)
    {
        var candidate = await db.FindCandidate(command.CandidateId, ct);
        if (candidate is null)
            return CandidateErrors.NotFound;

        var screening = candidate.StartScreening(currentUser.UserId, clock);
        await db.SaveChangesAsync(ct);
        return screening.Id;
    }
}
