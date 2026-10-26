using FluentValidation;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Pipeline;

namespace Recruitment.Application.Features.Pipeline.CloseApplication;

/// <summary>Süreci sonlandırır: <c>Rejected</c> (reddedildi) veya <c>Withdrawn</c> (aday çekildi).</summary>
public sealed record CloseApplicationCommand(
    Guid ApplicationId, ApplicationStatus Status, Guid RejectionReasonId, string? Note) : ICommand<Unit>;

internal sealed class CloseApplicationCommandValidator : AbstractValidator<CloseApplicationCommand>
{
    public CloseApplicationCommandValidator()
    {
        RuleFor(c => c.Status)
            .Must(status => status is ApplicationStatus.Rejected or ApplicationStatus.Withdrawn)
            .WithMessage("Süreç yalnızca red veya aday çekilmesiyle sonlandırılabilir.");
        RuleFor(c => c.RejectionReasonId).NotEmpty().WithMessage("Ret nedeni seçmelisiniz.");
        RuleFor(c => c.Note).MaximumLength(2000).WithMessage("Not en fazla 2000 karakter olabilir.");
    }
}

internal sealed class CloseApplicationCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<CloseApplicationCommand, Unit>
{
    public async Task<Result<Unit>> Handle(CloseApplicationCommand command, CancellationToken ct)
    {
        var application = await db.FindApplication(command.ApplicationId, ct);
        if (application is null)
            return PipelineErrors.NotFound;

        if (!await db.IsSelectable(command.RejectionReasonId, DictionaryType.RejectionReason, ct))
        {
            return Error.Validation(new Dictionary<string, string[]>
            {
                ["rejectionReasonId"] = ["Geçerli bir ret nedeni seçmelisiniz."],
            });
        }

        application.Close(command.Status, command.RejectionReasonId, command.Note, currentUser.UserId, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
