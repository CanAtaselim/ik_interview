using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Application.Features.Positions;
using Recruitment.Domain.CvReviews;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Pipeline;

namespace Recruitment.Application.Features.Pipeline.GetPositionPipeline;

/// <summary>Pozisyonun süreç hattı: aşamalar ve her aşamadaki adaylar (kanban).</summary>
public sealed record GetPositionPipelineQuery(Guid PositionId) : IQuery<PositionPipelineDto>;

internal sealed class GetPositionPipelineQueryHandler(IAppDbContext db, TimeProvider clock)
    : IQueryHandler<GetPositionPipelineQuery, PositionPipelineDto>
{
    public async Task<Result<PositionPipelineDto>> Handle(GetPositionPipelineQuery query, CancellationToken ct)
    {
        var position = await db.Positions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == query.PositionId, ct);
        if (position is null)
            return PositionErrors.NotFound;

        var template = await db.DefaultPipelineTemplate(ct);
        if (template is null)
            return PipelineErrors.TemplateMissing;

        // Bir pozisyondaki süreç sayısı sınırlıdır; kanban hepsini tek seferde gösterir.
        var applications = await db.JobApplications.AsNoTracking()
            .Include(a => a.History)
            .Where(a => a.PositionId == position.Id)
            .OrderBy(a => a.StageEnteredAt)
            .ToListAsync(ct);

        var candidateIds = applications.Select(a => a.CandidateId).Distinct().ToList();
        var candidateNames = await db.Candidates.AsNoTracking()
            .Where(c => candidateIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.FirstName + " " + c.LastName, ct);
        var reasonNames = await db.ItemNames(applications.Select(a => a.RejectionReasonId).OfType<Guid>().Distinct(), ct);

        // Sürece bağlı ve adayın pozisyonsuz önerildiği değerlendirmeler (geçiş koşuluyla aynı kapsam).
        var reviews = await db.CvReviews.AsNoTracking()
            .Where(r => candidateIds.Contains(r.CandidateId))
            .Select(r => new { r.CandidateId, r.ApplicationId, r.Decision, r.SuggestedAt })
            .ToListAsync(ct);

        CvReviewDecision? CvDecisionOf(JobApplication application)
        {
            var decisions = reviews
                .Where(r => r.CandidateId == application.CandidateId &&
                            (r.ApplicationId == application.Id || r.ApplicationId is null))
                .OrderByDescending(r => r.SuggestedAt)
                .Select(r => r.Decision)
                .ToList();

            if (decisions.Count == 0)
                return null;
            if (decisions.Contains(CvReviewDecision.Positive))
                return CvReviewDecision.Positive;
            return decisions.Contains(CvReviewDecision.Pending) ? CvReviewDecision.Pending : decisions[0];
        }

        var now = clock.GetUtcNow();
        var applicationIds = applications.Select(a => a.Id).ToList();
        var nextInterviews = await db.Interviews.AsNoTracking()
            .Where(i => applicationIds.Contains(i.ApplicationId) && i.Status == InterviewStatus.Scheduled && i.EndsAt > now)
            .GroupBy(i => i.ApplicationId)
            .Select(g => new { ApplicationId = g.Key, StartsAt = g.Min(i => i.StartsAt) })
            .ToDictionaryAsync(x => x.ApplicationId, x => x.StartsAt, ct);

        var cards = applications
            .Select(a => new PipelineCardDto(
                a.Id,
                a.CandidateId,
                candidateNames.GetValueOrDefault(a.CandidateId, string.Empty),
                a.StageId,
                a.Status,
                a.StageEnteredAt,
                a.RejectionReasonId is { } reasonId ? reasonNames.GetValueOrDefault(reasonId) : null,
                a.CanUndo(clock),
                CvDecisionOf(a),
                nextInterviews.TryGetValue(a.Id, out var next) ? next : null))
            .ToList();

        return new PositionPipelineDto(
            position.Id,
            position.Title,
            position.Status,
            position.Headcount,
            position.FilledCount,
            [.. template.OrderedStages.Select(s => new PipelineStageDto(s.Id, s.Name, s.Kind, s.Order, s.IsOptional))],
            [.. cards.Where(c => c.Status is ApplicationStatus.Active or ApplicationStatus.Hired)],
            [.. cards.Where(c => c.Status is ApplicationStatus.Rejected or ApplicationStatus.Withdrawn)]);
    }
}
