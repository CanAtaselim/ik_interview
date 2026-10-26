using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.CvReviews;

namespace Recruitment.Application.Features.CvReviews.EventHandlers;

/// <summary>CV önerilerini ve kararlarını adayın zaman çizelgesine yazar. Gerekçe çizelgeye yazılmaz.</summary>
internal sealed class CvReviewTimelineWriter(IAppDbContext db) :
    IDomainEventHandler<CvReviewRequested>,
    IDomainEventHandler<CvReviewDecided>
{
    public async Task Handle(CvReviewRequested e, CancellationToken ct)
    {
        var position = await PositionTitle(e.ReviewId, ct);
        var summary = position is null
            ? $"CV, {await UserName(e.ReviewerUserId, ct)} adlı takım liderine önerildi."
            : $"CV, “{position}” pozisyonu için {await UserName(e.ReviewerUserId, ct)} adlı takım liderine önerildi.";

        await Write(e.CandidateId, TimelineEntryType.CvSuggested, summary, e.ActorUserId, e.OccurredAt, ct);
    }

    public async Task Handle(CvReviewDecided e, CancellationToken ct)
    {
        var decision = e.Decision switch
        {
            CvReviewDecision.Positive => "olumlu değerlendirdi",
            CvReviewDecision.Negative => "olumsuz değerlendirdi",
            _ => "beklemeye aldı",
        };
        var position = await PositionTitle(e.ReviewId, ct);
        var summary = position is null
            ? $"{await UserName(e.ReviewerUserId, ct)} CV'yi {decision}."
            : $"{await UserName(e.ReviewerUserId, ct)} CV'yi “{position}” pozisyonu için {decision}.";

        await Write(e.CandidateId, TimelineEntryType.CvReviewed, summary, e.ReviewerUserId, e.OccurredAt, ct);
    }

    private async Task<string> UserName(Guid userId, CancellationToken ct) =>
        await db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? string.Empty;

    private async Task<string?> PositionTitle(Guid reviewId, CancellationToken ct)
    {
        var applications = db.JobApplications;
        var positions = db.Positions;

        return await db.CvReviews
            .Where(r => r.Id == reviewId)
            .Select(r => applications
                .Where(a => a.Id == r.ApplicationId)
                .Select(a => positions.Where(p => p.Id == a.PositionId).Select(p => p.Title).FirstOrDefault())
                .FirstOrDefault())
            .FirstOrDefaultAsync(ct);
    }

    private async Task Write(
        Guid candidateId, TimelineEntryType type, string summary, Guid actorUserId, DateTimeOffset occurredAt, CancellationToken ct)
    {
        db.TimelineEntries.Add(TimelineEntry.Create(candidateId, type, summary, actorUserId, occurredAt));
        await db.SaveChangesAsync(ct);
    }
}
