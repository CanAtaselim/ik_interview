using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Application.Features.Pipeline;
using Recruitment.Domain.Pipeline;

namespace Recruitment.Application.Features.Dashboard;

/// <param name="Count">Aşamaya hiç ulaşmış süreç sayısı (atlanan isteğe bağlı aşamalar hariç).</param>
public sealed record FunnelStageDto(string Stage, int Count);

/// <param name="AverageDays">Dönemde bu aşamadan çıkan süreçlerin aşamada ortalama kalma süresi.</param>
/// <param name="Waiting">Şu an bu aşamada bekleyen aktif süreç.</param>
/// <param name="AverageWaitingDays">Şu an bekleyenlerin bu aşamada geçirdiği ortalama süre.</param>
public sealed record StageDurationDto(string Stage, double? AverageDays, int Waiting, double? AverageWaitingDays);

/// <param name="Reached">Aşamaya ulaşan süreç.</param>
/// <param name="Closed">Süreci o aşamada red veya çekilmeyle sonlanan.</param>
public sealed record StageRejectionDto(string Stage, int Reached, int Closed);

public sealed record DurationStatDto(double? AverageDays, double? MedianDays, int Count, double? PreviousAverageDays);

public sealed record DepartmentDurationDto(string Label, double AverageDays, int Count);

public sealed record DashboardPipelineDto(
    IReadOnlyList<FunnelStageDto> Funnel,
    IReadOnlyList<StageDurationDto> StageDurations,
    IReadOnlyList<StageRejectionDto> RejectionByStage,
    IReadOnlyList<CountItem> RejectionReasons,
    DurationStatDto TimeToHire,
    IReadOnlyList<DepartmentDurationDto> TimeToHireByDepartment,
    DurationStatDto TimeToFill);

/// <summary>Süreç hattı: işe alım hunisi, aşamada bekleme, ret nedenleri, işe alım ve kadro doldurma süresi.</summary>
public sealed record GetDashboardPipelineQuery(DashboardFilter Filter) : IQuery<DashboardPipelineDto>;

internal sealed class GetDashboardPipelineQueryHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetDashboardPipelineQuery, DashboardPipelineDto>
{
    private const int MaxReasons = 8;

    public async Task<Result<DashboardPipelineDto>> Handle(GetDashboardPipelineQuery query, CancellationToken ct)
    {
        var resolved = DashboardPeriod.Resolve(query.Filter, clock);
        if (resolved.IsFailure)
            return resolved.Error;

        var period = resolved.Value;
        var template = await db.DefaultPipelineTemplate(ct);
        if (template is null)
            return PipelineErrors.TemplateMissing;

        var stages = template.OrderedStages;
        var now = clock.GetUtcNow();

        // Süreç sayısı iç kullanım ölçeğindedir; geçmişiyle birlikte çekilip bellekte hesaplanır.
        var applications = await db.ScopedApplications(query.Filter).Include(a => a.History).ToListAsync(ct);
        var positions = await db.ScopedPositions(query.Filter)
            .Select(p => new { p.Id, p.DepartmentId, p.Headcount, p.CreatedAt })
            .ToListAsync(ct);
        var departments = await db.ItemNames(positions.Select(p => p.DepartmentId).Distinct(), ct);

        // Dönemde başlayan süreçler: huni ve aşama bazlı ret oranı bu kümeden hesaplanır.
        var cohort = applications.Where(a => period.Contains(a.StartedAt)).ToList();
        var reachedByApplication = cohort.ToDictionary(a => a.Id, a => StagesReached(a));

        var funnel = stages
            .Select(stage => new FunnelStageDto(stage.Name, cohort.Count(a => reachedByApplication[a.Id].Contains(stage.Id))))
            .ToList();

        var rejection = stages
            .Where(stage => stage.Kind != StageKind.Hired)
            .Select(stage => new StageRejectionDto(
                stage.Name,
                cohort.Count(a => reachedByApplication[a.Id].Contains(stage.Id)),
                cohort.Count(a => a.Status is ApplicationStatus.Rejected or ApplicationStatus.Withdrawn && a.StageId == stage.Id)))
            .ToList();

        // Aşamadan çıkış: bir sonraki aşamaya geçiş veya aşamada sonlanma. Geri alınanlar sayılmaz.
        var exits = applications
            .SelectMany(a => a.History)
            .Where(h => h.UndoneAt is null && h.FromStatus == ApplicationStatus.Active &&
                        (h.ToStageId != h.FromStageId || h.ToStatus != ApplicationStatus.Active) &&
                        period.Contains(h.ChangedAt))
            .ToList();
        var active = applications.Where(a => a.Status == ApplicationStatus.Active).ToList();

        var durations = stages
            .Where(stage => stage.Kind != StageKind.Hired)
            .Select(stage =>
            {
                var left = exits.Where(h => h.FromStageId == stage.Id).Select(h => (h.ChangedAt - h.PreviousStageEnteredAt).TotalDays).ToList();
                var waiting = active.Where(a => a.StageId == stage.Id).Select(a => (now - a.StageEnteredAt).TotalDays).ToList();
                return new StageDurationDto(
                    stage.Name,
                    left.Count == 0 ? null : Round(left.Average()),
                    waiting.Count,
                    waiting.Count == 0 ? null : Round(waiting.Average()));
            })
            .ToList();

        var closedInPeriod = applications
            .Where(a => a.Status is ApplicationStatus.Rejected or ApplicationStatus.Withdrawn &&
                        a.ClosedAt is { } closedAt && period.Contains(closedAt))
            .ToList();
        var reasonNames = await db.ItemNames(closedInPeriod.Select(a => a.RejectionReasonId).OfType<Guid>().Distinct(), ct);
        var reasons = closedInPeriod
            .GroupBy(a => a.RejectionReasonId is { } id ? reasonNames.GetValueOrDefault(id, DashboardLabels.Unspecified) : DashboardLabels.Unspecified)
            .Select(g => new CountItem(g.Key, g.Count()))
            .OrderByDescending(item => item.Count)
            .Take(MaxReasons)
            .ToList();

        var hired = applications.Where(a => a.Status == ApplicationStatus.Hired && a.ClosedAt is not null).ToList();
        double HireDays(JobApplication a) => (a.ClosedAt!.Value - a.StartedAt).TotalDays;
        var hiredInPeriod = hired.Where(a => period.Contains(a.ClosedAt!.Value)).ToList();
        var hiredPrevious = hired.Where(a => period.PreviousContains(a.ClosedAt!.Value)).ToList();

        var departmentOf = positions.ToDictionary(p => p.Id, p => p.DepartmentId);
        var hireByDepartment = hiredInPeriod
            .Where(a => departmentOf.ContainsKey(a.PositionId))
            .GroupBy(a => departments.GetValueOrDefault(departmentOf[a.PositionId], DashboardLabels.Unspecified))
            .Select(g => new DepartmentDurationDto(g.Key, Round(g.Average(HireDays)), g.Count()))
            .OrderBy(item => item.AverageDays)
            .ToList();

        return new DashboardPipelineDto(
            funnel,
            durations,
            rejection,
            reasons,
            Stat(hiredInPeriod.Select(HireDays).ToList(), hiredPrevious.Select(HireDays).ToList()),
            hireByDepartment,
            TimeToFill(positions.Select(p => (p.Id, p.Headcount, p.CreatedAt)).ToList(), hired, period));
    }

    // Süreç, başladığı aşamadan şimdiki aşamasına kadar uğradığı tüm aşamalara ulaşmış sayılır.
    private static HashSet<Guid> StagesReached(JobApplication application)
    {
        var reached = new HashSet<Guid> { application.StageId };
        foreach (var change in application.History.Where(h => h.UndoneAt is null))
        {
            reached.Add(change.FromStageId);
            reached.Add(change.ToStageId);
        }

        return reached;
    }

    // Pozisyon, işe alınan sayısı kadroya ulaşınca dolmuş sayılır; süre pozisyonun açılışından son işe alıma kadardır.
    private static DurationStatDto TimeToFill(
        IReadOnlyList<(Guid Id, int Headcount, DateTimeOffset CreatedAt)> positions,
        IReadOnlyList<JobApplication> hired,
        DashboardPeriod period)
    {
        var filled = positions
            .Select(p =>
            {
                var hires = hired.Where(a => a.PositionId == p.Id).OrderBy(a => a.ClosedAt).ToList();
                return hires.Count >= p.Headcount && p.Headcount > 0
                    ? (FilledAt: hires[p.Headcount - 1].ClosedAt!.Value, p.CreatedAt)
                    : ((DateTimeOffset FilledAt, DateTimeOffset CreatedAt)?)null;
            })
            .OfType<(DateTimeOffset FilledAt, DateTimeOffset CreatedAt)>()
            .ToList();

        return Stat(
            filled.Where(f => period.Contains(f.FilledAt)).Select(f => (f.FilledAt - f.CreatedAt).TotalDays).ToList(),
            filled.Where(f => period.PreviousContains(f.FilledAt)).Select(f => (f.FilledAt - f.CreatedAt).TotalDays).ToList());
    }

    private static DurationStatDto Stat(IReadOnlyList<double> current, IReadOnlyList<double> previous) =>
        new(
            current.Count == 0 ? null : Round(current.Average()),
            current.Count == 0 ? null : Round(Median(current)),
            current.Count,
            previous.Count == 0 ? null : Round(previous.Average()));

    private static double Median(IReadOnlyList<double> values)
    {
        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private static double Round(double value) => Math.Round(value, 1);
}
