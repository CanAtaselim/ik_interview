using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Interviews;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Scorecards.GetScorecard;

public sealed record GetScorecardQuery(Guid Id) : IQuery<ScorecardDto>;

internal sealed class GetScorecardQueryHandler(
    IAppDbContext db, IScoreCalculator calculator, ICurrentUser currentUser, TimeProvider clock)
    : IQueryHandler<GetScorecardQuery, ScorecardDto>
{
    public async Task<Result<ScorecardDto>> Handle(GetScorecardQuery query, CancellationToken ct)
    {
        var scorecard = await db.Scorecards.AsNoTracking().FirstOrDefaultAsync(s => s.Id == query.Id, ct);
        if (scorecard is null)
            return ScorecardErrors.NotFound;

        var interview = await db.VisibleInterviews(currentUser).AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == scorecard.InterviewId, ct);
        if (interview is null)
            return ScorecardErrors.NotFound;

        var ownSubmitted = await db.Scorecards.AnyAsync(
            s => s.InterviewId == interview.Id && s.InterviewerUserId == currentUser.UserId && s.Status == ScorecardStatus.Submitted, ct);
        // Görülemeyen değerlendirmenin varlığı da açığa çıkmaz.
        if (!scorecard.CanView(currentUser, ownSubmitted))
            return ScorecardErrors.NotFound;

        return await scorecard.ToDto(db, calculator, currentUser, interview.StartsAt, clock.GetUtcNow(), ct);
    }
}
