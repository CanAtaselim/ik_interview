using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Blacklist;
using Recruitment.Application.Features.Pipeline;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Pipeline;

namespace Recruitment.Application.Features.Interviews.ScheduleInterview;

/// <summary>Adayın bir pozisyondaki süreci için mülakat planlar.</summary>
public sealed record ScheduleInterviewCommand(
    Guid ApplicationId,
    Guid TypeId,
    InterviewMode Mode,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    Guid? RoomId,
    string? MeetingUrl,
    string? Note,
    IReadOnlyCollection<Guid> InterviewerUserIds,
    bool ConfirmConflicts = false,
    bool ConfirmBlacklist = false,
    Guid? TemplateId = null,
    bool NotifyCandidate = false) : ICommand<Guid>, IInterviewPlanFields;

internal sealed class ScheduleInterviewCommandValidator : InterviewPlanValidator<ScheduleInterviewCommand>;

internal sealed class ScheduleInterviewCommandHandler(
    IAppDbContext db, IBlacklistMatcher blacklist, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<ScheduleInterviewCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ScheduleInterviewCommand command, CancellationToken ct)
    {
        var application = await db.JobApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, ct);
        if (application is null)
            return PipelineErrors.NotFound;
        if (application.Status != ApplicationStatus.Active)
            return InterviewErrors.ApplicationNotActive;

        if (await db.FindInvalidPlan(command, ct) is { } invalid)
            return invalid;

        // Kara liste engellemez; İK'nın onayı gerekir ve onay erişim kaydına yazılır.
        var candidate = await db.Candidates.AsNoTracking().FirstAsync(c => c.Id == application.CandidateId, ct);
        var matches = await blacklist.FindForCandidate(candidate, ct);
        if (matches.Count > 0 && !command.ConfirmBlacklist)
            return InterviewErrors.BlacklistMatch(matches);

        var plan = command.ToPlan();
        if (plan.TemplateId is null)
            plan = plan with { TemplateId = await db.SuggestTemplate(application.PositionId, plan.TypeId, ct) };
        var conflicts = await db.FindConflicts(plan, excludeInterviewId: null, ct);
        if (conflicts.Count > 0 && !command.ConfirmConflicts)
            return InterviewErrors.Conflicts(conflicts);

        var interview = Interview.Schedule(
            application.Id, application.CandidateId, application.PositionId, plan, currentUser.UserId, clock);

        db.Interviews.Add(interview);
        if (matches.Count > 0)
        {
            db.AccessLogs.Add(AccessLog.Blacklist(
                AccessAction.BlacklistWarningAcknowledged, currentUser.UserId, candidate.Id, clock.GetUtcNow()));
        }
        await db.SaveChangesAsync(ct);
        return interview.Id;
    }
}
