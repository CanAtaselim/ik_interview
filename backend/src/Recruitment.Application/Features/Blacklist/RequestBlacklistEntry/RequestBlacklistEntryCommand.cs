using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Candidates;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Blacklist;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Blacklist.RequestBlacklistEntry;

/// <summary>
/// Adayı kara listeye alır. İK Uzmanının talebi İK Yöneticisinin onayını bekler; İK Yöneticisinin
/// eklediği kayıt hemen yürürlüğe girer.
/// </summary>
/// <param name="ExpiresAt">Boşsa kayıt kalıcıdır.</param>
public sealed record RequestBlacklistEntryCommand(
    Guid CandidateId, Guid ReasonId, string Description, DateTimeOffset? ExpiresAt) : ICommand<Guid>;

internal sealed class RequestBlacklistEntryCommandValidator : AbstractValidator<RequestBlacklistEntryCommand>
{
    public RequestBlacklistEntryCommandValidator()
    {
        RuleFor(c => c.ReasonId).NotEmpty().WithMessage("Kara liste nedeni seçmelisiniz.");
        RuleFor(c => c.Description)
            .NotEmpty().WithMessage("Kara listeye alma gerekçesini yazmalısınız.")
            .MaximumLength(BlacklistEntry.DescriptionMaxLength)
            .WithMessage($"Gerekçe en fazla {BlacklistEntry.DescriptionMaxLength} karakter olabilir.");
    }
}

internal sealed class RequestBlacklistEntryCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<RequestBlacklistEntryCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RequestBlacklistEntryCommand command, CancellationToken ct)
    {
        if (!await db.Candidates.AnyAsync(c => c.Id == command.CandidateId, ct))
            return CandidateErrors.NotFound;

        if (!await db.IsSelectable(command.ReasonId, DictionaryType.BlacklistReason, ct))
            return Error.Validation(new Dictionary<string, string[]> { ["reasonId"] = ["Geçerli bir neden seçmelisiniz."] });

        var alreadyOpen = await db.BlacklistEntries.AnyAsync(
            e => e.CandidateId == command.CandidateId &&
                 (e.Status == BlacklistStatus.PendingApproval || e.Status == BlacklistStatus.Active),
            ct);
        if (alreadyOpen)
            return BlacklistErrors.AlreadyOpen;

        var entry = BlacklistEntry.Request(
            command.CandidateId,
            command.ReasonId,
            command.Description,
            command.ExpiresAt,
            currentUser.UserId,
            approveImmediately: currentUser.IsInRole(Role.HrManager),
            clock);

        db.BlacklistEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry.Id;
    }
}
