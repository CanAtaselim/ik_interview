using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.HiringRequests;
using Recruitment.Domain.Positions;

namespace Recruitment.Application.Features.Dashboard;

public sealed record OpenPositionDto(Guid Id, string Title, string Department, int Headcount, int Filled);

/// <param name="PendingApproval">Şu an onay bekleyen talep sayısı (dönemden bağımsız).</param>
/// <param name="AverageApprovalDays">Dönemde karara bağlanan taleplerin gönderimden karara ortalama süresi; yoksa null.</param>
/// <param name="OpenByDepartment">Açık pozisyonlarda dolmamış kadro, birim bazında.</param>
public sealed record DashboardRequestsDto(
    IReadOnlyList<CountItem> ByStatus,
    IReadOnlyList<CountItem> ByReason,
    int PendingApproval,
    double? AverageApprovalDays,
    IReadOnlyList<CountItem> OpenByDepartment,
    IReadOnlyList<OpenPositionDto> OpenPositions);

/// <summary>Kadro talepleri ve pozisyonlar: durum, neden, onay süresi, açık kadro.</summary>
public sealed record GetDashboardRequestsQuery(DashboardFilter Filter) : IQuery<DashboardRequestsDto>;

internal sealed class GetDashboardRequestsQueryHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetDashboardRequestsQuery, DashboardRequestsDto>
{
    private const int MaxOpenPositions = 8;

    public async Task<Result<DashboardRequestsDto>> Handle(GetDashboardRequestsQuery query, CancellationToken ct)
    {
        var resolved = DashboardPeriod.Resolve(query.Filter, clock);
        if (resolved.IsFailure)
            return resolved.Error;

        var period = resolved.Value;
        var requests = db.ScopedRequests(query.Filter);

        var inPeriod = await requests
            .Where(r => r.CreatedAt >= period.Start && r.CreatedAt < period.End)
            .Select(r => new { r.Status, r.Reason })
            .ToListAsync(ct);

        var pending = await requests.CountAsync(r => r.Status == HiringRequestStatus.PendingApproval, ct);

        var decided = await requests
            .Where(r => (r.Status == HiringRequestStatus.Approved || r.Status == HiringRequestStatus.Rejected) &&
                        r.SubmittedAt != null && r.CompletedAt >= period.Start && r.CompletedAt < period.End)
            .Select(r => new { Submitted = r.SubmittedAt!.Value, Completed = r.CompletedAt!.Value })
            .ToListAsync(ct);

        var open = await db.ScopedPositions(query.Filter)
            .Where(p => p.Status == PositionStatus.Open)
            .Select(p => new { p.Id, p.Title, p.DepartmentId, p.Headcount, p.FilledCount })
            .ToListAsync(ct);
        var departments = await db.ItemNames(open.Select(p => p.DepartmentId).Distinct(), ct);

        string Department(Guid id) => departments.GetValueOrDefault(id, DashboardLabels.Unspecified);

        return new DashboardRequestsDto(
            Enum.GetValues<HiringRequestStatus>()
                .Select(status => new CountItem(DashboardLabels.Of(status), inPeriod.Count(r => r.Status == status)))
                .ToList(),
            Enum.GetValues<HiringReason>()
                .Select(reason => new CountItem(DashboardLabels.Of(reason), inPeriod.Count(r => r.Reason == reason)))
                .ToList(),
            pending,
            decided.Count == 0 ? null : Math.Round(decided.Average(r => (r.Completed - r.Submitted).TotalDays), 1),
            open.GroupBy(p => p.DepartmentId)
                .Select(g => new CountItem(Department(g.Key), g.Sum(p => Math.Max(0, p.Headcount - p.FilledCount))))
                .Where(item => item.Count > 0)
                .OrderByDescending(item => item.Count)
                .ToList(),
            open.OrderByDescending(p => p.Headcount - p.FilledCount)
                .ThenBy(p => p.Title)
                .Take(MaxOpenPositions)
                .Select(p => new OpenPositionDto(p.Id, p.Title, Department(p.DepartmentId), p.Headcount, p.FilledCount))
                .ToList());
    }
}
