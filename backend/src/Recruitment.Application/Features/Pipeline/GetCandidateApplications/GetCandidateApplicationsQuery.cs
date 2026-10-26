using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Candidates;
using Recruitment.Application.Features.Dictionaries;

namespace Recruitment.Application.Features.Pipeline.GetCandidateApplications;

/// <summary>Adayın tüm süreçleri (devam eden ve sonlanan), en yenisi başta.</summary>
public sealed record GetCandidateApplicationsQuery(Guid CandidateId) : IQuery<IReadOnlyList<CandidateApplicationDto>>;

internal sealed class GetCandidateApplicationsQueryHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetCandidateApplicationsQuery, IReadOnlyList<CandidateApplicationDto>>
{
    // Bir adayın süreç sayısı azdır; liste sayfalanmaz.
    public async Task<Result<IReadOnlyList<CandidateApplicationDto>>> Handle(
        GetCandidateApplicationsQuery query, CancellationToken ct)
    {
        if (!await db.Candidates.AnyAsync(c => c.Id == query.CandidateId, ct))
            return CandidateErrors.NotFound;

        var applications = await db.JobApplications.AsNoTracking()
            .Include(a => a.History)
            .Where(a => a.CandidateId == query.CandidateId)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(ct);

        var positionIds = applications.Select(a => a.PositionId).Distinct().ToList();
        var positionTitles = await db.Positions.AsNoTracking()
            .Where(p => positionIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Title, ct);

        var stageIds = applications.Select(a => a.StageId).Distinct().ToList();
        var stageNames = await db.PipelineTemplates.AsNoTracking()
            .SelectMany(t => t.Stages)
            .Where(s => stageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var reasonNames = await db.ItemNames(applications.Select(a => a.RejectionReasonId).OfType<Guid>().Distinct(), ct);

        return applications
            .Select(a => new CandidateApplicationDto(
                a.Id,
                a.PositionId,
                positionTitles.GetValueOrDefault(a.PositionId, string.Empty),
                stageNames.GetValueOrDefault(a.StageId, string.Empty),
                a.Status,
                a.StartedAt,
                a.StageEnteredAt,
                a.RejectionReasonId is { } reasonId ? reasonNames.GetValueOrDefault(reasonId) : null,
                a.ClosingNote,
                a.ClosedAt,
                a.CanUndo(clock)))
            .ToList();
    }
}
