using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.CvReviews;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Positions;

namespace Recruitment.Application.Features.Dashboard;

/// <param name="NewCandidates">Dönemde eklenen aday (havuz tüm adaylardır, birim/pozisyon süzmez).</param>
/// <param name="Hires">Dönemde işe alınan.</param>
/// <param name="InterviewsHeld">Dönemde tamamlanan mülakat.</param>
/// <param name="OpenHeadcount">Açık pozisyonlarda henüz dolmamış kadro.</param>
/// <param name="AwaitingOutcome">Saati geçmiş ama sonucu girilmemiş mülakat.</param>
public sealed record DashboardSummaryDto(
    Kpi NewCandidates,
    Kpi Hires,
    Kpi InterviewsHeld,
    int OpenPositions,
    int OpenHeadcount,
    int ActiveApplications,
    int PendingCvReviews,
    int OverdueCvReviews,
    int UpcomingInterviews,
    int AwaitingOutcome);

/// <summary>Dashboard'un üst şeridi: anlık sayılar ve dönem karşılaştırmalı göstergeler.</summary>
public sealed record GetDashboardSummaryQuery(DashboardFilter Filter) : IQuery<DashboardSummaryDto>;

internal sealed class GetDashboardSummaryQueryHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public async Task<Result<DashboardSummaryDto>> Handle(GetDashboardSummaryQuery query, CancellationToken ct)
    {
        var resolved = DashboardPeriod.Resolve(query.Filter, clock);
        if (resolved.IsFailure)
            return resolved.Error;

        var period = resolved.Value;
        var filter = query.Filter;
        var now = clock.GetUtcNow();

        var candidateDates = await db.Candidates.AsNoTracking()
            .Where(c => c.CreatedAt >= period.PreviousStart && c.CreatedAt < period.End)
            .Select(c => c.CreatedAt)
            .ToListAsync(ct);

        var hireDates = await db.ScopedApplications(filter)
            .Where(a => a.Status == ApplicationStatus.Hired && a.ClosedAt >= period.PreviousStart && a.ClosedAt < period.End)
            .Select(a => a.ClosedAt!.Value)
            .ToListAsync(ct);

        var interviewDates = await db.ScopedInterviews(filter)
            .Where(i => i.Status == InterviewStatus.Completed && i.StartsAt >= period.PreviousStart && i.StartsAt < period.End)
            .Select(i => i.StartsAt)
            .ToListAsync(ct);

        var openPositions = await db.ScopedPositions(filter)
            .Where(p => p.Status == PositionStatus.Open)
            .Select(p => new { p.Headcount, p.FilledCount })
            .ToListAsync(ct);

        var activeApplications = await db.ScopedApplications(filter).CountAsync(a => a.Status == ApplicationStatus.Active, ct);

        var pendingReviews = db.ScopedCvReviews(filter).Where(r => r.Decision == CvReviewDecision.Pending);
        var pendingCount = await pendingReviews.CountAsync(ct);
        var overdueCount = await pendingReviews.CountAsync(r => r.DueAt < now, ct);

        var upcoming = await db.ScopedInterviews(filter)
            .CountAsync(i => i.Status == InterviewStatus.Scheduled && i.EndsAt > now, ct);
        var awaiting = await db.ScopedInterviews(filter)
            .CountAsync(i => i.Status == InterviewStatus.Scheduled && i.EndsAt <= now, ct);

        return new DashboardSummaryDto(
            ToKpi(candidateDates, period),
            ToKpi(hireDates, period),
            ToKpi(interviewDates, period),
            openPositions.Count,
            openPositions.Sum(p => Math.Max(0, p.Headcount - p.FilledCount)),
            activeApplications,
            pendingCount,
            overdueCount,
            upcoming,
            awaiting);
    }

    private static Kpi ToKpi(IReadOnlyCollection<DateTimeOffset> dates, DashboardPeriod period) =>
        new(dates.Count(period.Contains), dates.Count(period.PreviousContains));
}
