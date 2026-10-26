using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Interviews;

namespace Recruitment.Application.Features.Interviews.RecordInterviewOutcome;

/// <summary>Mülakatın yapıldığını veya adayın gelmediğini kaydeder.</summary>
public sealed record RecordInterviewOutcomeCommand(Guid Id, InterviewStatus Outcome) : ICommand<Unit>;

internal sealed class RecordInterviewOutcomeCommandValidator : AbstractValidator<RecordInterviewOutcomeCommand>
{
    public RecordInterviewOutcomeCommandValidator() =>
        RuleFor(c => c.Outcome)
            .Must(o => o is InterviewStatus.Completed or InterviewStatus.NoShow)
            .WithMessage("Sonuç olarak yalnızca “yapıldı” veya “aday gelmedi” seçilebilir.");
}

internal sealed class RecordInterviewOutcomeCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<RecordInterviewOutcomeCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RecordInterviewOutcomeCommand command, CancellationToken ct)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == command.Id, ct);
        if (interview is null)
            return InterviewErrors.NotFound;

        interview.RecordOutcome(command.Outcome, currentUser.UserId, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
