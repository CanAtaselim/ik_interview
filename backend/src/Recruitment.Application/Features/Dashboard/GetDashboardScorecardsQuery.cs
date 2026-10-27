using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Dashboard;

public sealed record ScoreItemDto(string Label, double Average, int Count);

/// <param name="Pending">Mülakat saati geçmiş ama henüz gönderilmemiş değerlendirme (dönemden bağımsız).</param>
/// <param name="PendingByInterviewer">Gönderilmemiş değerlendirmeler, mülakatçı bazında.</param>
/// <param name="Submitted">Dönemde gönderilen değerlendirme.</param>
/// <param name="Recommendations">Gönderilenlerin genel önerisi.</param>
/// <param name="CompetencyAverages">Yetkinlik başına ağırlıklı puan ortalaması (1-5).</param>
/// <param name="InterviewerScoring">Mülakatçı başına verdiği değerlendirmelerin ortalama puanı; kimin sert/yumuşak puanladığını gösterir.</param>
public sealed record DashboardScorecardsDto(
    int Pending,
    IReadOnlyList<CountItem> PendingByInterviewer,
    int Submitted,
    IReadOnlyList<CountItem> Recommendations,
    IReadOnlyList<ScoreItemDto> CompetencyAverages,
    IReadOnlyList<ScoreItemDto> InterviewerScoring);

/// <summary>Değerlendirme ve puanlama: bekleyenler, öneri dağılımı, yetkinlik ve mülakatçı puanları.</summary>
public sealed record GetDashboardScorecardsQuery(DashboardFilter Filter) : IQuery<DashboardScorecardsDto>;

internal sealed class GetDashboardScorecardsQueryHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetDashboardScorecardsQuery, DashboardScorecardsDto>
{
    private const int MaxInterviewers = 8;

    public async Task<Result<DashboardScorecardsDto>> Handle(GetDashboardScorecardsQuery query, CancellationToken ct)
    {
        var resolved = DashboardPeriod.Resolve(query.Filter, clock);
        if (resolved.IsFailure)
            return resolved.Error;

        var period = resolved.Value;
        var now = clock.GetUtcNow();
        var scopedInterviewIds = db.ScopedInterviews(query.Filter).Select(i => i.Id);
        var passedInterviewIds = db.ScopedInterviews(query.Filter)
            .Where(i => i.Status != InterviewStatus.Cancelled && i.EndsAt <= now)
            .Select(i => i.Id);

        var pending = await db.Scorecards.AsNoTracking()
            .Where(s => s.Status == ScorecardStatus.Draft && passedInterviewIds.Contains(s.InterviewId))
            .Select(s => s.InterviewerUserId)
            .ToListAsync(ct);

        // Gönderilen değerlendirme sayısı iç kullanım ölçeğindedir; maddeleriyle çekilip bellekte hesaplanır.
        var submitted = await db.Scorecards.AsNoTracking()
            .Where(s => s.Status == ScorecardStatus.Submitted && s.SubmittedAt >= period.Start && s.SubmittedAt < period.End &&
                        scopedInterviewIds.Contains(s.InterviewId))
            .ToListAsync(ct);

        var competencyNames = await db.ItemNames(
            submitted.SelectMany(s => s.Items).Select(i => i.CompetencyId).OfType<Guid>().Distinct(), ct);
        var userName = await db.UserNames(pending.Concat(submitted.Select(s => s.InterviewerUserId)), ct);
        var calculator = new WeightedAverageCalculator();

        var competencies = submitted
            .SelectMany(s => s.Items)
            .Where(i => i.Score is not null && i.CompetencyId is not null)
            .GroupBy(i => i.CompetencyId!.Value)
            .Select(g => new ScoreItemDto(
                competencyNames.GetValueOrDefault(g.Key, DashboardLabels.Unspecified),
                Math.Round((double)(calculator.Calculate(g.Select(i => new WeightedScore(i.Score!.Value, i.Weight))) ?? 0), 2),
                g.Count()))
            .OrderByDescending(item => item.Average)
            .ToList();

        var interviewers = submitted
            .Select(s => (s.InterviewerUserId, Score: calculator.Calculate(
                s.Items.Where(i => i.Score is not null).Select(i => new WeightedScore(i.Score!.Value, i.Weight)))))
            .Where(x => x.Score is not null)
            .GroupBy(x => x.InterviewerUserId)
            .Select(g => new ScoreItemDto(userName(g.Key), Math.Round((double)g.Average(x => x.Score!.Value), 2), g.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label)
            .Take(MaxInterviewers)
            .ToList();

        return new DashboardScorecardsDto(
            pending.Count,
            pending.GroupBy(id => id)
                .Select(g => new CountItem(userName(g.Key), g.Count()))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Label)
                .Take(MaxInterviewers)
                .ToList(),
            submitted.Count,
            Enum.GetValues<Recommendation>()
                .Select(r => new CountItem(DashboardLabels.Of(r), submitted.Count(s => s.Recommendation == r)))
                .ToList(),
            competencies,
            interviewers);
    }
}
