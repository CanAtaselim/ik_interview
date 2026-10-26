using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Scorecards.ReopenScorecard;

/// <summary>
/// Gönderilmiş değerlendirmeyi mülakatçının düzeltmesi için yeniden açar. Yalnızca İK Yöneticisi;
/// gerekçe zorunludur ve kayda geçer.
/// </summary>
public sealed record ReopenScorecardCommand(Guid Id, string Reason) : ICommand<Unit>;

internal sealed class ReopenScorecardCommandValidator : AbstractValidator<ReopenScorecardCommand>
{
    public ReopenScorecardCommandValidator() =>
        RuleFor(c => c.Reason)
            .NotEmpty().WithMessage("Yeniden açma gerekçesini yazmalısınız.")
            .MaximumLength(2000).WithMessage("Gerekçe en fazla 2000 karakter olabilir.");
}

internal sealed class ReopenScorecardCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<ReopenScorecardCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ReopenScorecardCommand command, CancellationToken ct)
    {
        var scorecard = await db.Scorecards.FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (scorecard is null)
            return ScorecardErrors.NotFound;

        scorecard.Reopen(command.Reason, currentUser.UserId, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
