using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Blacklist;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Pipeline;

namespace Recruitment.Application.Features.Candidates.GetCandidates;

internal sealed class GetCandidatesQueryHandler(IAppDbContext db, ICandidateTextSearch textSearch)
    : IQueryHandler<GetCandidatesQuery, PagedResult<CandidateListItemDto>>
{
    public async Task<Result<PagedResult<CandidateListItemDto>>> Handle(GetCandidatesQuery query, CancellationToken ct)
    {
        var candidates = textSearch.Apply(db.Candidates.AsNoTracking(), query.Search);

        if (query.SourceId is { } sourceId)
            candidates = candidates.Where(c => c.SourceId == sourceId);

        if (query.InTalentPool is { } inTalentPool)
            candidates = candidates.Where(c => c.InTalentPool == inTalentPool);

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = query.Tag.Trim();
            candidates = candidates.Where(c => EF.Property<List<string>>(c, "_tags").Contains(tag));
        }

        var applications = db.JobApplications;
        if (query.Process is { } process)
        {
            candidates = process switch
            {
                CandidateProcessFilter.Active =>
                    candidates.Where(c => applications.Any(a => a.CandidateId == c.Id && a.Status == ApplicationStatus.Active)),
                CandidateProcessFilter.None =>
                    candidates.Where(c => !applications.Any(a => a.CandidateId == c.Id)),
                CandidateProcessFilter.Hired =>
                    candidates.Where(c => applications.Any(a => a.CandidateId == c.Id && a.Status == ApplicationStatus.Hired)),
                _ => candidates.Where(c => applications.Any(a =>
                    a.CandidateId == c.Id && (a.Status == ApplicationStatus.Rejected || a.Status == ApplicationStatus.Withdrawn))),
            };
        }

        if (query.Blacklisted is { } blacklisted)
        {
            var entries = db.BlacklistEntries;
            candidates = candidates.Where(c =>
                entries.Any(e => e.CandidateId == c.Id && e.Status == BlacklistStatus.Active) == blacklisted);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim().ToLower();
            candidates = candidates.Where(c => c.City != null && c.City.ToLower().Contains(city));
        }

        var dictionary = db.DictionaryItems;
        var timeline = db.TimelineEntries;

        // Son işlem: zaman çizelgesindeki en son olay ile kartın en son güncellenmesinden yeni olanı.
        // Dosya, ön görüşme, süreç, mülakat ve karar hareketlerinin hepsi zaman çizelgesine düşer.
        var page = await candidates
            .Select(c => new
            {
                Candidate = c,
                LastEvent = timeline.Where(t => t.CandidateId == c.Id).Max(t => (DateTimeOffset?)t.OccurredAt),
            })
            .Select(x => new
            {
                x.Candidate,
                LastActivityAt =
                    (x.LastEvent ?? x.Candidate.CreatedAt) > (x.Candidate.UpdatedAt ?? x.Candidate.CreatedAt)
                        ? (x.LastEvent ?? x.Candidate.CreatedAt)
                        : (x.Candidate.UpdatedAt ?? x.Candidate.CreatedAt),
            })
            .OrderByDescending(x => x.LastActivityAt)
            .ThenByDescending(x => x.Candidate.CreatedAt)
            .Select(x => new Row(
                x.Candidate.Id,
                x.Candidate.FirstName,
                x.Candidate.LastName,
                x.Candidate.Email,
                x.Candidate.Phone,
                x.Candidate.City,
                dictionary.Where(d => d.Id == x.Candidate.SourceId).Select(d => d.Name).FirstOrDefault(),
                EF.Property<List<string>>(x.Candidate, "_tags"),
                x.Candidate.InTalentPool,
                x.Candidate.Documents.Count,
                x.Candidate.CreatedAt,
                x.LastActivityAt))
            .ToPagedResult(query.Page, query.PageSize, ct);

        var ids = page.Items.Select(r => r.Id).ToList();
        var cvs = await LoadCvs(ids, ct);
        var blacklist = await LoadBlacklist(ids, ct);
        var processes = await LoadProcesses(ids, ct);

        var items = page.Items
            .Select(r => new CandidateListItemDto(
                r.Id, r.FirstName, r.LastName, r.Email, r.Phone, r.City, r.SourceName, r.Tags, r.InTalentPool,
                r.DocumentCount, r.CreatedAt, r.LastActivityAt,
                cvs.GetValueOrDefault(r.Id),
                blacklist.TryGetValue(r.Id, out var status) ? status : null,
                processes.GetValueOrDefault(r.Id) ?? []))
            .ToList();

        return new PagedResult<CandidateListItemDto>(items, page.TotalCount, page.Page, page.PageSize);
    }

    // Listeden açılan CV: "CV" türündeki (veya türü belirtilmemiş eski) dosyaların en son yüklenen.
    private async Task<Dictionary<Guid, CandidateCvDto>> LoadCvs(List<Guid> candidateIds, CancellationToken ct)
    {
        var cvTypeIds = await db.DictionaryItems.AsNoTracking()
            .Where(d => d.Type == DictionaryType.DocumentType && d.Name.ToLower() == CandidateMappings.CvTypeName.ToLower())
            .Select(d => d.Id)
            .ToListAsync(ct);

        var documents = await db.Candidates.AsNoTracking()
            .Where(c => candidateIds.Contains(c.Id))
            .SelectMany(c => c.Documents, (c, d) => new { CandidateId = c.Id, d.Id, d.FileName, d.ContentType, d.TypeId, d.UploadedAt })
            .Where(d => d.TypeId == null || cvTypeIds.Contains(d.TypeId.Value))
            .ToListAsync(ct);

        return documents
            .GroupBy(d => d.CandidateId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(d => d.UploadedAt).Select(d => new CandidateCvDto(d.Id, d.FileName, d.ContentType)).First());
    }

    // Yürürlükteki veya onay bekleyen kayıt; neden ve gerekçe listede gösterilmez.
    private async Task<Dictionary<Guid, BlacklistStatus>> LoadBlacklist(List<Guid> candidateIds, CancellationToken ct)
    {
        var entries = await db.BlacklistEntries.AsNoTracking()
            .Where(e => candidateIds.Contains(e.CandidateId) &&
                        (e.Status == BlacklistStatus.PendingApproval || e.Status == BlacklistStatus.Active))
            .Select(e => new { e.CandidateId, e.Status })
            .ToListAsync(ct);

        // Hem onay bekleyen hem yürürlükte kayıt varsa yürürlükteki öne çıkar.
        return entries
            .GroupBy(e => e.CandidateId)
            .ToDictionary(g => g.Key, g => g.Any(e => e.Status == BlacklistStatus.Active) ? BlacklistStatus.Active : BlacklistStatus.PendingApproval);
    }

    // Açık süreçlerin hepsi; hiç açık süreç yoksa en son sonlanan tek süreç.
    private async Task<Dictionary<Guid, List<CandidateProcessDto>>> LoadProcesses(List<Guid> candidateIds, CancellationToken ct)
    {
        var applications = await db.JobApplications.AsNoTracking()
            .Where(a => candidateIds.Contains(a.CandidateId))
            .Select(a => new { a.Id, a.CandidateId, a.PositionId, a.StageId, a.Status, a.StartedAt, a.ClosedAt })
            .ToListAsync(ct);
        if (applications.Count == 0)
            return [];

        var chosen = applications
            .GroupBy(a => a.CandidateId)
            .SelectMany(g =>
            {
                var open = g.Where(a => a.Status == ApplicationStatus.Active).OrderBy(a => a.StartedAt).ToList();
                return open.Count > 0
                    ? open
                    : [g.OrderByDescending(a => a.ClosedAt ?? a.StartedAt).First()];
            })
            .ToList();

        var stageIds = chosen.Select(a => a.StageId).Distinct().ToList();
        var stages = await db.PipelineTemplates.AsNoTracking()
            .SelectMany(t => t.Stages)
            .Where(s => stageIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, s.Kind })
            .ToDictionaryAsync(s => s.Id, ct);

        var positionIds = chosen.Select(a => a.PositionId).Distinct().ToList();
        var titles = await db.Positions.AsNoTracking()
            .Where(p => positionIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Title, ct);

        return chosen
            .Where(a => stages.ContainsKey(a.StageId))
            .GroupBy(a => a.CandidateId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => new CandidateProcessDto(
                        a.Id,
                        a.PositionId,
                        titles.GetValueOrDefault(a.PositionId, "—"),
                        stages[a.StageId].Name,
                        stages[a.StageId].Kind,
                        a.Status))
                    .ToList());
    }

    private sealed record Row(
        Guid Id,
        string FirstName,
        string LastName,
        string? Email,
        string? Phone,
        string? City,
        string? SourceName,
        IReadOnlyList<string> Tags,
        bool InTalentPool,
        int DocumentCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset LastActivityAt);
}
