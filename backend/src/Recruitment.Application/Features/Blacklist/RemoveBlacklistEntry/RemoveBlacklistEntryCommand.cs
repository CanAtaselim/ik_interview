using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Blacklist.RemoveBlacklistEntry;

/// <summary>Yürürlükteki kaydı kara listeden çıkarır veya onay bekleyen talebi reddeder; gerekçe zorunludur.</summary>
public sealed record RemoveBlacklistEntryCommand(Guid Id, string Reason) : ICommand<Unit>;

internal sealed class RemoveBlacklistEntryCommandValidator : AbstractValidator<RemoveBlacklistEntryCommand>
{
    public RemoveBlacklistEntryCommandValidator() =>
        RuleFor(c => c.Reason)
            .NotEmpty().WithMessage("Kara listeden çıkarma gerekçesini yazmalısınız.")
            .MaximumLength(2000).WithMessage("Gerekçe en fazla 2000 karakter olabilir.");
}

internal sealed class RemoveBlacklistEntryCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<RemoveBlacklistEntryCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RemoveBlacklistEntryCommand command, CancellationToken ct)
    {
        var entry = await db.BlacklistEntries.FirstOrDefaultAsync(e => e.Id == command.Id, ct);
        if (entry is null)
            return BlacklistErrors.NotFound;

        entry.Remove(currentUser.UserId, command.Reason, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
