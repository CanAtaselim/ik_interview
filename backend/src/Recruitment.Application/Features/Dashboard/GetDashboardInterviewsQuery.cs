using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Interviews;

namespace Recruitment.Application.Features.Dashboard;

/// <param name="Day">0 = Pazartesi … 6 = Pazar.</param>
public sealed record HeatCell(int Day, int Hour, int Count);

/// <param name="Upcoming">Önümüzdeki 7 günde planlı mülakat.</param>
/// <param name="AwaitingOutcome">Saati geçmiş ama sonucu girilmemiş mülakat (dönemden bağımsız).</param>
/// <param name="ByStatus">Dönemdeki mülakatların durumu.</param>
/// <param name="ByType">Dönemdeki (iptaller hariç) mülakatların tipi.</param>
/// <param name="InterviewerLoad">Dönemde mülakatçı başına (iptaller hariç) mülakat sayısı.</param>
/// <param name="Heatmap">Gün ve saate göre yoğunluk (Türkiye saati, yalnızca dolu hücreler).</param>
public sealed record DashboardInterviewsDto(
    int InPeriod,
    int Upcoming,
    int AwaitingOutcome,
    IReadOnlyList<CountItem> ByStatus,
    IReadOnlyList<CountItem> ByType,
    IReadOnlyList<CountItem> InterviewerLoad,
    IReadOnlyList<HeatCell> Heatmap);

/// <summary>Mülakat: durum, tip, mülakatçı yükü, yoğunluk haritası.</summary>
public sealed record GetDashboardInterviewsQuery(DashboardFilter Filter) : IQuery<DashboardInterviewsDto>;

internal sealed class GetDashboardInterviewsQueryHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetDashboardInterviewsQuery, DashboardInterviewsDto>
{
    private const int MaxInterviewers = 8;
    private const int UpcomingDays = 7;

    public async Task<Result<DashboardInterviewsDto>> Handle(GetDashboardInterviewsQuery query, CancellationToken ct)
    {
        var resolved = DashboardPeriod.Resolve(query.Filter, clock);
        if (resolved.IsFailure)
            return resolved.Error;

        var period = resolved.Value;
        var now = clock.GetUtcNow();
        var interviews = db.ScopedInterviews(query.Filter);

        var inPeriod = await interviews
            .Where(i => i.StartsAt >= period.Start && i.StartsAt < period.End)
            .Select(i => new
            {
                i.StartsAt,
                i.Status,
                i.TypeId,
                Interviewers = i.Interviewers.Select(x => x.UserId).ToList(),
            })
            .ToListAsync(ct);

        var upcomingEnd = now.AddDays(UpcomingDays);
        var upcoming = await interviews
            .CountAsync(i => i.Status == InterviewStatus.Scheduled && i.EndsAt > now && i.StartsAt < upcomingEnd, ct);
        var awaiting = await interviews.CountAsync(i => i.Status == InterviewStatus.Scheduled && i.EndsAt <= now, ct);

        var held = inPeriod.Where(i => i.Status != InterviewStatus.Cancelled).ToList();
        var typeNames = await db.ItemNames(held.Select(i => i.TypeId).Distinct(), ct);
        var interviewerName = await db.UserNames(held.SelectMany(i => i.Interviewers), ct);

        var heatmap = held
            .Select(i => TimeZoneInfo.ConvertTime(i.StartsAt, TurkishTime.Zone))
            .GroupBy(local => (Day: ((int)local.DayOfWeek + 6) % 7, local.Hour))
            .Select(g => new HeatCell(g.Key.Day, g.Key.Hour, g.Count()))
            .OrderBy(cell => cell.Day).ThenBy(cell => cell.Hour)
            .ToList();

        return new DashboardInterviewsDto(
            inPeriod.Count,
            upcoming,
            awaiting,
            Enum.GetValues<InterviewStatus>()
                .Select(status => new CountItem(DashboardLabels.Of(status), inPeriod.Count(i => i.Status == status)))
                .ToList(),
            held.GroupBy(i => typeNames.GetValueOrDefault(i.TypeId, DashboardLabels.Unspecified))
                .Select(g => new CountItem(g.Key, g.Count()))
                .OrderByDescending(item => item.Count)
                .ToList(),
            held.SelectMany(i => i.Interviewers)
                .GroupBy(id => id)
                .Select(g => new CountItem(interviewerName(g.Key), g.Count()))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Label)
                .Take(MaxInterviewers)
                .ToList(),
            heatmap);
    }
}
