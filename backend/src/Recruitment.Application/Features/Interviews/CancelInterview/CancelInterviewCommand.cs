using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Interviews;

namespace Recruitment.Application.Features.Interviews.CancelInterview;

public sealed record CancelInterviewCommand(Guid Id, string Reason) : ICommand<Unit>;

internal sealed class CancelInterviewCommandValidator : AbstractValidator<CancelInterviewCommand>
{
    public CancelInterviewCommandValidator() =>
        RuleFor(c => c.Reason)
            .NotEmpty().WithMessage("İptal nedenini yazmalısınız.")
            .MaximumLength(Interview.TextMaxLength)
            .WithMessage($"İptal nedeni en fazla {Interview.TextMaxLength} karakter olabilir.");
}

internal sealed class CancelInterviewCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<CancelInterviewCommand, Unit>
{
    public async Task<Result<Unit>> Handle(CancelInterviewCommand command, CancellationToken ct)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == command.Id, ct);
        if (interview is null)
            return InterviewErrors.NotFound;

        interview.Cancel(command.Reason, currentUser.UserId, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
