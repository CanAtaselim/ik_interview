using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Interviews;

namespace Recruitment.Application.Features.Interviews.RescheduleInterview;

/// <summary>Planlı mülakatın zamanını, yerini, tipini veya mülakatçılarını değiştirir.</summary>
public sealed record RescheduleInterviewCommand(
    Guid Id,
    Guid TypeId,
    InterviewMode Mode,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    Guid? RoomId,
    string? MeetingUrl,
    string? Note,
    IReadOnlyCollection<Guid> InterviewerUserIds,
    bool ConfirmConflicts = false,
    Guid? TemplateId = null,
    bool NotifyCandidate = false) : ICommand<Unit>, IInterviewPlanFields;

internal sealed class RescheduleInterviewCommandValidator : InterviewPlanValidator<RescheduleInterviewCommand>;

internal sealed class RescheduleInterviewCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<RescheduleInterviewCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RescheduleInterviewCommand command, CancellationToken ct)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == command.Id, ct);
        if (interview is null)
            return InterviewErrors.NotFound;

        if (await db.FindInvalidPlan(command, ct) is { } invalid)
            return invalid;

        var plan = command.ToPlan();
        var conflicts = await db.FindConflicts(plan, interview.Id, ct);
        if (conflicts.Count > 0 && !command.ConfirmConflicts)
            return InterviewErrors.Conflicts(conflicts);

        interview.Reschedule(plan, currentUser.UserId, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
