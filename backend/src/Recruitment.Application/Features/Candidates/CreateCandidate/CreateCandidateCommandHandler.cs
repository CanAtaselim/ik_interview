using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Candidates;

namespace Recruitment.Application.Features.Candidates.CreateCandidate;

internal sealed class CreateCandidateCommandHandler(
    IAppDbContext db, IBlacklistMatcher blacklist, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<CreateCandidateCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCandidateCommand command, CancellationToken ct)
    {
        if (await db.FindInvalidSource(command, ct) is { } error)
            return error;

        // Mükerrer kayıt engellenmez; İK uyarıyı görüp onaylarsa eklenir ve bu karar kayda geçer.
        var duplicates = await db.FindDuplicates(command.Email, command.Phone, excludeId: null, ct);
        if (duplicates.Count > 0 && !command.ConfirmDuplicate)
            return CandidateErrors.PossibleDuplicate(duplicates);

        // Kara liste eşleşmesi de engellemez; İK'nın onayı gerekir ve onay erişim kaydına yazılır.
        var blacklistMatches = await blacklist.FindMatches(
            command.Email, command.Phone, command.FirstName, command.LastName, excludeCandidateId: null, ct);
        if (blacklistMatches.Count > 0 && !command.ConfirmBlacklist)
            return CandidateErrors.BlacklistMatch(blacklistMatches);

        var candidate = Candidate.Create(
            command.ToProfile(), currentUser.UserId, despiteDuplicate: duplicates.Count > 0, clock);

        db.Candidates.Add(candidate);
        if (blacklistMatches.Count > 0)
        {
            db.AccessLogs.Add(AccessLog.Blacklist(
                AccessAction.BlacklistWarningAcknowledged, currentUser.UserId, candidate.Id, clock.GetUtcNow()));
        }
        await db.SaveChangesAsync(ct);
        return candidate.Id;
    }
}
