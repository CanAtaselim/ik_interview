using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Blacklist;

namespace Recruitment.Application.Features.Dashboard;

/// <param name="Active">Aktif kara liste kaydı.</param>
/// <param name="PendingApproval">Onay bekleyen kayıt.</param>
/// <param name="Added">Dönemde açılan kayıt (bir önceki dönemle birlikte).</param>
/// <param name="ByReason">Aktif kayıtların nedeni.</param>
public sealed record DashboardBlacklistDto(int Active, int PendingApproval, Kpi Added, IReadOnlyList<CountItem> ByReason);

/// <summary>Kara liste özeti. Birim/pozisyon filtresi uygulanmaz; kayıtlar adaya bağlıdır.</summary>
public sealed record GetDashboardBlacklistQuery(DashboardFilter Filter) : IQuery<DashboardBlacklistDto>;

internal sealed class GetDashboardBlacklistQueryHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetDashboardBlacklistQuery, DashboardBlacklistDto>
{
    public async Task<Result<DashboardBlacklistDto>> Handle(GetDashboardBlacklistQuery query, CancellationToken ct)
    {
        var resolved = DashboardPeriod.Resolve(query.Filter, clock);
        if (resolved.IsFailure)
            return resolved.Error;

        var period = resolved.Value;
        var entries = await db.BlacklistEntries.AsNoTracking()
            .Select(e => new { e.Status, e.ReasonId, e.CreatedAt })
            .ToListAsync(ct);

        var active = entries.Where(e => e.Status == BlacklistStatus.Active).ToList();
        var reasonNames = await db.ItemNames(active.Select(e => e.ReasonId).Distinct(), ct);

        return new DashboardBlacklistDto(
            active.Count,
            entries.Count(e => e.Status == BlacklistStatus.PendingApproval),
            new Kpi(entries.Count(e => period.Contains(e.CreatedAt)), entries.Count(e => period.PreviousContains(e.CreatedAt))),
            active.GroupBy(e => reasonNames.GetValueOrDefault(e.ReasonId, DashboardLabels.Unspecified))
                .Select(g => new CountItem(g.Key, g.Count()))
                .OrderByDescending(item => item.Count)
                .ToList());
    }
}
