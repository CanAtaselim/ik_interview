using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.CvReviews;
using Recruitment.Domain.Notifications;

namespace Recruitment.Application.Features.Notifications.EventHandlers;

/// <summary>CV önerisi, süre aşımı ve kararlarla ilgili bildirimler.</summary>
internal sealed class CvReviewNotifications(IAppDbContext db, Notifier notifier) :
    IDomainEventHandler<CvReviewRequested>,
    IDomainEventHandler<CvReviewOverdue>,
    IDomainEventHandler<CvReviewEscalated>,
    IDomainEventHandler<CvReviewDecided>
{
    public async Task Handle(CvReviewRequested e, CancellationToken ct)
    {
        var info = await Load(e.ReviewId, ct);
        await notifier.Notify(
            [e.ReviewerUserId],
            NotificationType.CvReviewRequested,
            $"Size bir CV önerildi: {info.Candidate}",
            $"{info.SuggestedBy} önerdi · {info.Position} · Son tarih: {TurkishTime.Date(info.DueAt)}",
            $"/cv-reviews/{e.ReviewId}",
            $"cv-req:{e.ReviewId}",
            ct);
    }

    public async Task Handle(CvReviewOverdue e, CancellationToken ct)
    {
        var info = await Load(e.ReviewId, ct);
        await notifier.Notify(
            [e.ReviewerUserId],
            NotificationType.CvReviewReminder,
            $"CV değerlendirmesinin süresi doldu: {info.Candidate}",
            $"Son tarih {TurkishTime.Date(info.DueAt)} idi. Kararınızı İK ekibi bekliyor.",
            $"/cv-reviews/{e.ReviewId}",
            $"cv-overdue:{e.ReviewId}",
            ct);
    }

    public async Task Handle(CvReviewEscalated e, CancellationToken ct)
    {
        var info = await Load(e.ReviewId, ct);
        await notifier.Notify(
            [e.SuggestedByUserId],
            NotificationType.CvReviewEscalated,
            $"Geciken CV değerlendirmesi: {info.Candidate}",
            $"{info.Reviewer} hatırlatmaya rağmen karar vermedi (son tarih {TurkishTime.Date(info.DueAt)}).",
            $"/cv-reviews/{e.ReviewId}",
            $"cv-escalated:{e.ReviewId}",
            ct);
    }

    public async Task Handle(CvReviewDecided e, CancellationToken ct)
    {
        var info = await Load(e.ReviewId, ct);
        var (title, link) = e.Decision switch
        {
            // Olumlu kararda İK'yı doğrudan mülakat planlayacağı yere götürür.
            CvReviewDecision.Positive => (
                $"Olumlu CV kararı: {info.Candidate} · mülakat planlayın",
                $"/candidates/{e.CandidateId}?tab=mulakatlar"),
            CvReviewDecision.Negative => (
                $"{info.Reviewer} CV'yi olumsuz değerlendirdi: {info.Candidate}",
                $"/candidates/{e.CandidateId}?tab=cv-degerlendirme"),
            _ => (
                $"{info.Reviewer} CV'yi beklemeye aldı: {info.Candidate}",
                $"/candidates/{e.CandidateId}?tab=cv-degerlendirme"),
        };

        await notifier.Notify(
            [info.SuggestedByUserId],
            NotificationType.CvReviewDecided,
            title,
            $"{info.Position} · Gerekçe: {info.Reason}",
            link,
            $"cv-decided:{e.ReviewId}:{e.OccurredAt.UtcTicks}",
            ct);
    }

    private sealed record ReviewInfo(
        string Candidate, string Position, string Reviewer, string SuggestedBy, Guid SuggestedByUserId, DateTimeOffset DueAt, string? Reason);

    private async Task<ReviewInfo> Load(Guid reviewId, CancellationToken ct)
    {
        var candidates = db.Candidates;
        var users = db.Users;
        var applications = db.JobApplications;
        var positions = db.Positions;

        return await db.CvReviews.AsNoTracking()
            .Where(r => r.Id == reviewId)
            .Select(r => new ReviewInfo(
                candidates.Where(c => c.Id == r.CandidateId).Select(c => c.FirstName + " " + c.LastName).FirstOrDefault() ?? string.Empty,
                applications.Where(a => a.Id == r.ApplicationId)
                    .Select(a => positions.Where(p => p.Id == a.PositionId).Select(p => p.Title).FirstOrDefault())
                    .FirstOrDefault() ?? "Pozisyon belirtilmeden",
                users.Where(u => u.Id == r.ReviewerUserId).Select(u => u.DisplayName).FirstOrDefault() ?? string.Empty,
                users.Where(u => u.Id == r.SuggestedByUserId).Select(u => u.DisplayName).FirstOrDefault() ?? string.Empty,
                r.SuggestedByUserId,
                r.DueAt,
                r.DecisionReason))
            .FirstAsync(ct);
    }
}
