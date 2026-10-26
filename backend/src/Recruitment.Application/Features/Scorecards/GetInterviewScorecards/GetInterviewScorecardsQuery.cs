using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Application.Features.Interviews;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Scorecards.GetInterviewScorecards;

/// <summary>
/// Mülakatın değerlendirmeleri: her mülakatçının durumu, görülebilenlerin puanları, ortalama ve
/// ortak karar. Bağımsız değerlendirme kuralı burada uygulanır.
/// </summary>
public sealed record GetInterviewScorecardsQuery(Guid InterviewId) : IQuery<InterviewScorecardsDto>;

internal sealed class GetInterviewScorecardsQueryHandler(
    IAppDbContext db, IScoreCalculator calculator, ICurrentUser currentUser)
    : IQueryHandler<GetInterviewScorecardsQuery, InterviewScorecardsDto>
{
    public async Task<Result<InterviewScorecardsDto>> Handle(GetInterviewScorecardsQuery query, CancellationToken ct)
    {
        var interview = await db.VisibleInterviews(currentUser).AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == query.InterviewId, ct);
        if (interview is null)
            return InterviewErrors.NotFound;

        var scorecards = await db.Scorecards.AsNoTracking().Where(s => s.InterviewId == interview.Id).ToListAsync(ct);
        var mine = scorecards.FirstOrDefault(s => s.InterviewerUserId == currentUser.UserId);
        var ownSubmitted = mine?.IsSubmitted == true;
        var visible = scorecards.Where(s => s.CanView(currentUser, ownSubmitted)).ToList();
        var visibleSubmitted = visible.Where(s => s.IsSubmitted).ToList();

        var userIds = interview.InterviewerUserIds.Concat(interview.DebriefByUserId is { } by ? [by] : []).ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var competencyNames = await db.ItemNames(
            visibleSubmitted.SelectMany(s => s.Items).Select(i => i.CompetencyId).OfType<Guid>().Distinct(), ct);

        var interviewers = interview.InterviewerUserIds
            .Select(userId =>
            {
                var scorecard = scorecards.FirstOrDefault(s => s.InterviewerUserId == userId);
                var canView = scorecard is not null && visible.Contains(scorecard);
                var progress = scorecard is null ? ScorecardProgress.NotStarted
                    : scorecard.IsSubmitted ? ScorecardProgress.Submitted
                    : ScorecardProgress.Draft;
                return new InterviewerScorecardDto(
                    userId,
                    users.GetValueOrDefault(userId, string.Empty),
                    progress,
                    canView ? scorecard!.Id : null,
                    canView && scorecard!.IsSubmitted ? scorecard.Recommendation : null,
                    canView && scorecard!.IsSubmitted ? scorecard.WeightedScore(calculator) : null,
                    canView);
            })
            .ToList();

        var scores = visibleSubmitted.Select(s => s.WeightedScore(calculator)).OfType<decimal>().ToList();
        var allSubmitted = interview.InterviewerUserIds.All(id => scorecards.Any(s => s.InterviewerUserId == id && s.IsSubmitted));

        return new InterviewScorecardsDto(
            interview.Id,
            interviewers,
            mine?.Id,
            OthersHidden: !currentUser.IsHr && !ownSubmitted && interview.InterviewerUserIds.Count > 1,
            scores.Count == 0 ? null : Math.Round(scores.Average(), 2, MidpointRounding.AwayFromZero),
            ScorecardQueries.CompetencyScores(visibleSubmitted.SelectMany(s => s.Items), calculator, competencyNames),
            interview.DebriefDecision is { } decision
                ? new DebriefDto(decision, interview.DebriefReason!, interview.DebriefByUserId is { } byId ? users.GetValueOrDefault(byId) : null, interview.DebriefAt!.Value)
                : null,
            CanDebrief: currentUser.IsHr && interview.Status == InterviewStatus.Completed && allSubmitted);
    }
}
