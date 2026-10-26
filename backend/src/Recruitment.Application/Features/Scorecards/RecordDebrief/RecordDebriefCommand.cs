using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Interviews;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Scorecards.RecordDebrief;

/// <summary>Tüm değerlendirmeler gönderildikten sonra İK'nın ortak kararı ve gerekçesi.</summary>
public sealed record RecordDebriefCommand(Guid InterviewId, DebriefDecision Decision, string Reason) : ICommand<Unit>;

internal sealed class RecordDebriefCommandValidator : AbstractValidator<RecordDebriefCommand>
{
    public RecordDebriefCommandValidator()
    {
        RuleFor(c => c.Decision).IsInEnum().WithMessage("Ortak kararı seçmelisiniz.");
        RuleFor(c => c.Reason)
            .NotEmpty().WithMessage("Ortak kararın gerekçesini yazmalısınız.")
            .MaximumLength(2000).WithMessage("Gerekçe en fazla 2000 karakter olabilir.");
    }
}

internal sealed class RecordDebriefCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<RecordDebriefCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RecordDebriefCommand command, CancellationToken ct)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == command.InterviewId, ct);
        if (interview is null)
            return InterviewErrors.NotFound;

        var submitted = await db.SubmittedPairs([interview.Id], ct);
        var allSubmitted = interview.InterviewerUserIds.All(userId => submitted.Contains((interview.Id, userId)));

        interview.RecordDebrief(command.Decision, command.Reason, allSubmitted, currentUser.UserId, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
