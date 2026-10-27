using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.CvReviews;

namespace Recruitment.Application.Features.Dashboard;

/// <param name="Decisions">Dönemde önerilen CV'lerin karar durumu.</param>
/// <param name="AverageDecisionDays">Dönemde karara bağlananların öneriden karara ortalama süresi (takvim günü).</param>
/// <param name="SlaCompliancePercent">Dönemde karara bağlananların son tarihine uyan yüzdesi; karar yoksa null.</param>
/// <param name="Overdue">Şu an süresi geçmiş, karar bekleyen değerlendirme.</param>
/// <param name="PendingByReviewer">Karar bekleyen değerlendirmeler, değerlendirici bazında.</param>
public sealed record DashboardCvReviewsDto(
    int Suggested,
    IReadOnlyList<CountItem> Decisions,
    double? AverageDecisionDays,
    double? PreviousAverageDecisionDays,
    int? SlaCompliancePercent,
    int Overdue,
    IReadOnlyList<CountItem> PendingByReviewer);

/// <summary>CV değerlendirme: karar dağılımı, süre, SLA uyumu, gecikenler.</summary>
public sealed record GetDashboardCvReviewsQuery(DashboardFilter Filter) : IQuery<DashboardCvReviewsDto>;

internal sealed class GetDashboardCvReviewsQueryHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetDashboardCvReviewsQuery, DashboardCvReviewsDto>
{
    private const int MaxReviewers = 8;

    public async Task<Result<DashboardCvReviewsDto>> Handle(GetDashboardCvReviewsQuery query, CancellationToken ct)
    {
        var resolved = DashboardPeriod.Resolve(query.Filter, clock);
        if (resolved.IsFailure)
            return resolved.Error;

        var period = resolved.Value;
        var now = clock.GetUtcNow();
        var reviews = db.ScopedCvReviews(query.Filter);

        var suggested = await reviews
            .Where(r => r.SuggestedAt >= period.Start && r.SuggestedAt < period.End)
            .Select(r => r.Decision)
            .ToListAsync(ct);

        var decided = await reviews
            .Where(r => r.DecidedAt != null && r.DecidedAt >= period.PreviousStart && r.DecidedAt < period.End)
            .Select(r => new { r.SuggestedAt, DecidedAt = r.DecidedAt!.Value, r.DueAt })
            .ToListAsync(ct);
        var decidedNow = decided.Where(r => period.Contains(r.DecidedAt)).ToList();
        var decidedBefore = decided.Where(r => period.PreviousContains(r.DecidedAt)).ToList();

        var pending = await reviews
            .Where(r => r.Decision == CvReviewDecision.Pending)
            .Select(r => new { r.ReviewerUserId, r.DueAt })
            .ToListAsync(ct);
        var reviewerName = await db.UserNames(pending.Select(r => r.ReviewerUserId), ct);

        return new DashboardCvReviewsDto(
            suggested.Count,
            Enum.GetValues<CvReviewDecision>()
                .Select(decision => new CountItem(DashboardLabels.Of(decision), suggested.Count(d => d == decision)))
                .ToList(),
            Average(decidedNow.Select(r => (r.DecidedAt - r.SuggestedAt).TotalDays).ToList()),
            Average(decidedBefore.Select(r => (r.DecidedAt - r.SuggestedAt).TotalDays).ToList()),
            decidedNow.Count == 0 ? null : (int)Math.Round(100.0 * decidedNow.Count(r => r.DecidedAt <= r.DueAt) / decidedNow.Count),
            pending.Count(r => r.DueAt < now),
            pending.GroupBy(r => r.ReviewerUserId)
                .Select(g => new CountItem(reviewerName(g.Key), g.Count()))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Label)
                .Take(MaxReviewers)
                .ToList());
    }

    private static double? Average(IReadOnlyList<double> days) => days.Count == 0 ? null : Math.Round(days.Average(), 1);
}
