using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Application.Features.Positions;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Scorecards.GetPositionComparison;

/// <summary>
/// Pozisyondaki adaylar, gönderilmiş değerlendirmelerden hesaplanan puanlarıyla yan yana. Bir
/// mülakatçı, kendi değerlendirmesini göndermediği adayın puanlarını göremez (bağımsız değerlendirme).
/// </summary>
public sealed record GetPositionComparisonQuery(Guid PositionId) : IQuery<PositionComparisonDto>;

internal sealed class GetPositionComparisonQueryHandler(
    IAppDbContext db, IScoreCalculator calculator, ICurrentUser currentUser, TimeProvider clock)
    : IQueryHandler<GetPositionComparisonQuery, PositionComparisonDto>
{
    public async Task<Result<PositionComparisonDto>> Handle(GetPositionComparisonQuery query, CancellationToken ct)
    {
        var position = await db.Positions.AsNoTracking()
            .Where(PositionChecks.VisibleTo(currentUser).ToExpression())
            .FirstOrDefaultAsync(p => p.Id == query.PositionId, ct);
        if (position is null)
            return PositionErrors.NotFound;

        var now = clock.GetUtcNow();
        var applications = await db.JobApplications.AsNoTracking()
            .Where(a => a.PositionId == position.Id && (a.Status == ApplicationStatus.Active || a.Status == ApplicationStatus.Hired))
            .ToListAsync(ct);
        var applicationIds = applications.Select(a => a.Id).ToList();
        var candidateIds = applications.Select(a => a.CandidateId).ToList();

        var interviews = await db.Interviews.AsNoTracking()
            .Where(i => applicationIds.Contains(i.ApplicationId))
            .DueForScoring(now)
            .ToListAsync(ct);
        var interviewIds = interviews.Select(i => i.Id).ToList();
        var scorecards = await db.Scorecards.AsNoTracking()
            .Where(s => interviewIds.Contains(s.InterviewId) && s.Status == ScorecardStatus.Submitted)
            .ToListAsync(ct);

        var candidateNames = await db.Candidates.AsNoTracking()
            .Where(c => candidateIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.FirstName + " " + c.LastName, ct);
        var stageIds = applications.Select(a => a.StageId).Distinct().ToList();
        var stageNames = await db.PipelineTemplates.AsNoTracking()
            .SelectMany(t => t.Stages)
            .Where(s => stageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var competencyNames = await db.ItemNames(
            scorecards.SelectMany(s => s.Items).Select(i => i.CompetencyId).OfType<Guid>().Distinct(), ct);

        var rows = applications
            .Select(application =>
            {
                var own = interviews.Where(i => i.ApplicationId == application.Id).ToList();
                var ownIds = own.Select(i => i.Id).ToHashSet();
                var submitted = scorecards.Where(s => ownIds.Contains(s.InterviewId)).ToList();
                // Mülakatçının bu adayda göndermediği değerlendirmesi varsa puanlar ona gösterilmez.
                var hidden = !currentUser.IsHr && own.Any(i =>
                    i.IsAssignedTo(currentUser.UserId) &&
                    !submitted.Any(s => s.InterviewId == i.Id && s.InterviewerUserId == currentUser.UserId));
                var scores = submitted.Select(s => s.WeightedScore(calculator)).OfType<decimal>().ToList();
                var lastDebrief = own.Where(i => i.DebriefDecision is not null).MaxBy(i => i.DebriefAt)?.DebriefDecision;

                return new ComparisonRowDto(
                    application.Id,
                    application.CandidateId,
                    candidateNames.GetValueOrDefault(application.CandidateId, string.Empty),
                    stageNames.GetValueOrDefault(application.StageId, string.Empty),
                    application.Status,
                    submitted.Count,
                    own.Sum(i => i.InterviewerUserIds.Count),
                    hidden || scores.Count == 0 ? null : Math.Round(scores.Average(), 2, MidpointRounding.AwayFromZero),
                    hidden ? [] : ScorecardQueries.CompetencyScores(submitted.SelectMany(s => s.Items), calculator, competencyNames),
                    hidden
                        ? new Dictionary<Recommendation, int>()
                        : submitted.Where(s => s.Recommendation is not null)
                            .GroupBy(s => s.Recommendation!.Value)
                            .ToDictionary(g => g.Key, g => g.Count()),
                    hidden ? null : lastDebrief,
                    hidden);
            })
            .OrderByDescending(r => r.AverageScore ?? -1)
            .ThenBy(r => r.CandidateName)
            .ToList();

        var competencies = rows.SelectMany(r => r.CompetencyScores)
            .Select(c => new CompetencyScoreDto(c.CompetencyId, c.Name, 0))
            .DistinctBy(c => c.CompetencyId)
            .OrderBy(c => c.Name)
            .ToList();

        return new PositionComparisonDto(position.Id, position.Title, competencies, rows);
    }
}
