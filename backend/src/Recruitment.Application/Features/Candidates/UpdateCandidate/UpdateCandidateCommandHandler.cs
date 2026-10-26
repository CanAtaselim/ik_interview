using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Candidates;

namespace Recruitment.Application.Features.Candidates.UpdateCandidate;

internal sealed class UpdateCandidateCommandHandler(
    IAppDbContext db, IBlacklistMatcher blacklist, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<UpdateCandidateCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateCandidateCommand command, CancellationToken ct)
    {
        var candidate = await db.FindCandidate(command.Id, ct);
        if (candidate is null)
            return CandidateErrors.NotFound;

        // Kaynak yalnızca değiştiyse doğrulanır; sonradan pasifleştirilmiş kaynak kayıtta kalabilir.
        if (command.SourceId != candidate.SourceId && await db.FindInvalidSource(command, ct) is { } error)
            return error;

        // Uyarı yalnızca iletişim bilgisi değiştiyse gösterilir; aynı uyarı her kayıtta tekrarlanmaz.
        var contactChanged =
            ContactNormalizer.NormalizeEmail(command.Email) != candidate.NormalizedEmail ||
            ContactNormalizer.NormalizePhone(command.Phone) != candidate.NormalizedPhone;
        var duplicates = contactChanged
            ? await db.FindDuplicates(command.Email, command.Phone, excludeId: candidate.Id, ct)
            : [];
        if (duplicates.Count > 0 && !command.ConfirmDuplicate)
            return CandidateErrors.PossibleDuplicate(duplicates);

        // Kara liste de yalnızca kimlik veya iletişim bilgisi değiştiyse yeniden kontrol edilir.
        var identityChanged = contactChanged ||
            ContactNormalizer.NormalizeName(command.FirstName, command.LastName) != candidate.NormalizedName;
        var blacklistMatches = identityChanged
            ? await blacklist.FindMatches(
                command.Email, command.Phone, command.FirstName, command.LastName, candidate.Id, ct)
            : [];
        if (blacklistMatches.Count > 0 && !command.ConfirmBlacklist)
            return CandidateErrors.BlacklistMatch(blacklistMatches);

        if (blacklistMatches.Count > 0)
        {
            db.AccessLogs.Add(AccessLog.Blacklist(
                AccessAction.BlacklistWarningAcknowledged, currentUser.UserId, candidate.Id, clock.GetUtcNow()));
        }

        candidate.UpdateProfile(command.ToProfile(), despiteDuplicate: duplicates.Count > 0);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
