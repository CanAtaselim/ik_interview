using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Pipeline;

namespace Recruitment.Application.Features.Pipeline.SearchActiveApplications;

public sealed record ActiveApplicationDto(Guid ApplicationId, Guid CandidateId, string CandidateName, string PositionTitle);

/// <summary>Devam eden süreçleri aday adıyla arar (takvimden mülakat planlarken süreç seçimi).</summary>
public sealed record SearchActiveApplicationsQuery(string? Search) : IQuery<IReadOnlyList<ActiveApplicationDto>>
{
    public const int MaxResults = 20;
}

internal sealed class SearchActiveApplicationsQueryHandler(IAppDbContext db)
    : IQueryHandler<SearchActiveApplicationsQuery, IReadOnlyList<ActiveApplicationDto>>
{
    public async Task<Result<IReadOnlyList<ActiveApplicationDto>>> Handle(
        SearchActiveApplicationsQuery query, CancellationToken ct)
    {
        var candidates = db.Candidates.WhereAnyContains(query.Search, c => c.FirstName + " " + c.LastName);
        var positions = db.Positions;

        return await db.JobApplications.AsNoTracking()
            .Where(a => a.Status == ApplicationStatus.Active)
            .Join(candidates, a => a.CandidateId, c => c.Id, (a, c) => new { Application = a, Candidate = c })
            .OrderBy(x => x.Candidate.FirstName).ThenBy(x => x.Candidate.LastName)
            .Take(SearchActiveApplicationsQuery.MaxResults)
            .Select(x => new ActiveApplicationDto(
                x.Application.Id,
                x.Candidate.Id,
                x.Candidate.FirstName + " " + x.Candidate.LastName,
                positions.Where(p => p.Id == x.Application.PositionId).Select(p => p.Title).FirstOrDefault() ?? string.Empty))
            .ToListAsync(ct);
    }
}
