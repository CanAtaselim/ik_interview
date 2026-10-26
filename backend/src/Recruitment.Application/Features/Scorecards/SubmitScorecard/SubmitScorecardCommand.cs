using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Interviews;

namespace Recruitment.Application.Features.Scorecards.SubmitScorecard;

/// <summary>Değerlendirmeyi gönderir; gönderilen değerlendirme kilitlenir.</summary>
public sealed record SubmitScorecardCommand(Guid Id) : ICommand<Unit>;

internal sealed class SubmitScorecardCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<SubmitScorecardCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SubmitScorecardCommand command, CancellationToken ct)
    {
        var scorecard = await db.Scorecards.FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (scorecard is null)
            return ScorecardErrors.NotFound;
        if (scorecard.InterviewerUserId != currentUser.UserId)
            return ScorecardErrors.NotOwner;

        var interview = await db.Interviews.AsNoTracking()
            .Where(i => i.Id == scorecard.InterviewId)
            .Select(i => new { i.Status, i.StartsAt })
            .FirstAsync(ct);
        if (interview.Status is InterviewStatus.Cancelled or InterviewStatus.NoShow)
            return ScorecardErrors.InterviewClosed;
        if (clock.GetUtcNow() < interview.StartsAt)
            return ScorecardErrors.NotStartedYet;

        scorecard.Submit(clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
