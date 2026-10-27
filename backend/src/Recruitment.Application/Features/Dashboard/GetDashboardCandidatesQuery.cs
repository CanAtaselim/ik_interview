using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Pipeline;

namespace Recruitment.Application.Features.Dashboard;

public sealed record SourceConversionDto(string Source, int Candidates, int Hired);

/// <param name="Total">Tüm aday sayısı.</param>
/// <param name="InTalentPool">Havuzda bekleyen aday sayısı.</param>
/// <param name="Added">Dönemde eklenen aday (bir önceki dönemle birlikte).</param>
/// <param name="BySource">Dönemde eklenen adayların kaynağı.</param>
/// <param name="SourceConversion">Kaynak başına tüm zamanların aday ve işe alım sayısı (hangi kaynak verimli).</param>
/// <param name="Timeline">Dönemde eklenen aday sayısı, <c>Granularity</c> adımıyla.</param>
/// <param name="TopTags">En çok kullanılan etiketler (CV'den okunan yetenekler dahil).</param>
public sealed record DashboardCandidatesDto(
    int Total,
    int InTalentPool,
    Kpi Added,
    IReadOnlyList<CountItem> BySource,
    IReadOnlyList<SourceConversionDto> SourceConversion,
    string Granularity,
    IReadOnlyList<TimePoint> Timeline,
    IReadOnlyList<CountItem> TopTags);

/// <summary>Aday havuzu: büyüklük, kaynak, kaynak verimi, zaman içinde eklenenler, etiketler.</summary>
public sealed record GetDashboardCandidatesQuery(DashboardFilter Filter) : IQuery<DashboardCandidatesDto>;

internal sealed class GetDashboardCandidatesQueryHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetDashboardCandidatesQuery, DashboardCandidatesDto>
{
    private const int MaxTags = 10;
    private const int MaxSources = 8;

    public async Task<Result<DashboardCandidatesDto>> Handle(GetDashboardCandidatesQuery query, CancellationToken ct)
    {
        var resolved = DashboardPeriod.Resolve(query.Filter, clock);
        if (resolved.IsFailure)
            return resolved.Error;

        var period = resolved.Value;

        // Aday sayısı iç kullanım ölçeğindedir; yalnızca gereken sütunlar çekilip bellekte gruplanır.
        var candidates = await db.Candidates.AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.CreatedAt,
                c.SourceId,
                c.InTalentPool,
                Tags = EF.Property<List<string>>(c, "_tags"),
            })
            .ToListAsync(ct);

        var hiredCandidateIds = (await db.JobApplications.AsNoTracking()
                .Where(a => a.Status == ApplicationStatus.Hired)
                .Select(a => a.CandidateId)
                .ToListAsync(ct))
            .ToHashSet();

        var sourceNames = await db.ItemNames(candidates.Select(c => c.SourceId).OfType<Guid>().Distinct(), ct);

        string Source(Guid? id) =>
            id is { } value ? sourceNames.GetValueOrDefault(value, DashboardLabels.Unspecified) : DashboardLabels.Unspecified;

        var added = candidates.Where(c => period.Contains(c.CreatedAt)).ToList();
        var timeline = added.GroupBy(c => period.Bucket(c.CreatedAt)).ToDictionary(g => g.Key, g => g.Count());

        return new DashboardCandidatesDto(
            candidates.Count,
            candidates.Count(c => c.InTalentPool),
            new Kpi(added.Count, candidates.Count(c => period.PreviousContains(c.CreatedAt))),
            Top(added.GroupBy(c => Source(c.SourceId)).Select(g => new CountItem(g.Key, g.Count())), MaxSources),
            candidates.GroupBy(c => Source(c.SourceId))
                .Select(g => new SourceConversionDto(g.Key, g.Count(), g.Count(c => hiredCandidateIds.Contains(c.Id))))
                .OrderByDescending(s => s.Candidates)
                .Take(MaxSources)
                .ToList(),
            period.Granularity,
            period.Buckets().Select(day => new TimePoint(day, timeline.GetValueOrDefault(day))).ToList(),
            Top(
                candidates.SelectMany(c => c.Tags ?? [])
                    .GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new CountItem(g.First(), g.Count())),
                MaxTags));
    }

    private static List<CountItem> Top(IEnumerable<CountItem> items, int take) =>
        [.. items.OrderByDescending(i => i.Count).ThenBy(i => i.Label).Take(take)];
}
