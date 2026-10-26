using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.CvReviews;
using Recruitment.Application.Features.Scorecards;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Pipeline.MoveApplication;

/// <param name="BypassReason">
/// İK Yöneticisinin, gönderilmemiş mülakat değerlendirmelerine rağmen adayı geçirme gerekçesi.
/// </param>
public sealed record MoveApplicationCommand(Guid ApplicationId, Guid StageId, string? BypassReason = null) : ICommand<Unit>;

internal sealed class MoveApplicationCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<MoveApplicationCommand, Unit>
{
    public async Task<Result<Unit>> Handle(MoveApplicationCommand command, CancellationToken ct)
    {
        var application = await db.FindApplication(command.ApplicationId, ct);
        if (application is null)
            return PipelineErrors.NotFound;

        var template = await db.PipelineTemplateOfStage(application.StageId, ct);
        if (template is null)
            return PipelineErrors.TemplateMissing;

        var bypass = !string.IsNullOrWhiteSpace(command.BypassReason);
        if (bypass && !currentUser.IsInRole(Role.HrManager))
            return PipelineErrors.BypassNotAllowed;

        // Geçiş koşullarını aşamalar uygular (StageStates); burada yalnızca koşulların baktığı veri toplanır.
        var context = new StageTransitionContext(
            HasPositiveCvReview: await db.HasPositiveCvReview(application.CandidateId, application.Id, ct),
            PendingScorecardCount: await db.PendingScorecardCount(application.Id, clock.GetUtcNow(), ct),
            BypassedByHrManager: bypass);

        // Bekleyen değerlendirmeler yüzünden reddedilecek geçişte arayüz İK Yöneticisine gerekçeyle
        // geçirme seçeneği sunabilsin diye ayrı bir hata kodu döner.
        if (template.FindStage(application.StageId) is { } current &&
            template.FindStage(command.StageId) is { } target &&
            !StageStates.For(current.Kind).CanMoveTo(target, context).IsAllowed &&
            StageStates.For(current.Kind).CanMoveTo(target, context with { BypassedByHrManager = true }).IsAllowed)
        {
            return PipelineErrors.ScorecardsPending(context.PendingScorecardCount, currentUser.IsInRole(Role.HrManager));
        }

        application.MoveTo(template, command.StageId, context, currentUser.UserId, clock, command.BypassReason);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
