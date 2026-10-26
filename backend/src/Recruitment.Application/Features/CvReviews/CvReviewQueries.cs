using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.CvReviews;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.CvReviews;

internal static class CvReviewQueries
{
    /// <summary>
    /// İsteği yapan kullanıcının görebildiği değerlendirmeler: İK hepsini, takım lideri yalnızca
    /// kendisine veya birimine önerilenleri görür.
    /// </summary>
    public static IQueryable<CvReview> VisibleCvReviews(this IAppDbContext db, ICurrentUser currentUser) =>
        db.CvReviews.Where(
            new CvReviewVisibleTo(currentUser.UserId, currentUser.IsHr, currentUser.DepartmentId).ToExpression());

    /// <summary>Değerlendirmeleri liste satırına çevirir; adlar aynı sorguda okunur.</summary>
    public static IQueryable<CvReviewListItemDto> ToListItems(
        this IQueryable<CvReview> reviews, IAppDbContext db, Guid currentUserId, DateTimeOffset now)
    {
        var candidates = db.Candidates;
        var users = db.Users;
        var dictionary = db.DictionaryItems;
        var applications = db.JobApplications;
        var positions = db.Positions;

        return reviews.Select(r => new CvReviewListItemDto(
            r.Id,
            r.CandidateId,
            candidates.Where(c => c.Id == r.CandidateId).Select(c => c.FirstName + " " + c.LastName).FirstOrDefault() ?? string.Empty,
            r.ApplicationId,
            applications.Where(a => a.Id == r.ApplicationId).Select(a => (Guid?)a.PositionId).FirstOrDefault(),
            applications.Where(a => a.Id == r.ApplicationId)
                .Select(a => positions.Where(p => p.Id == a.PositionId).Select(p => p.Title).FirstOrDefault())
                .FirstOrDefault(),
            dictionary.Where(d => d.Id == r.DepartmentId).Select(d => d.Name).FirstOrDefault(),
            users.Where(u => u.Id == r.ReviewerUserId).Select(u => u.DisplayName).FirstOrDefault() ?? string.Empty,
            users.Where(u => u.Id == r.SuggestedByUserId).Select(u => u.DisplayName).FirstOrDefault() ?? string.Empty,
            r.SuggestionNote,
            r.Decision,
            r.DecisionReason,
            dictionary.Where(d => d.Id == r.RejectionReasonId).Select(d => d.Name).FirstOrDefault(),
            r.SuggestedAt,
            r.DueAt,
            r.DecidedAt,
            r.Decision == CvReviewDecision.Pending && r.DueAt < now,
            r.ReviewerUserId == currentUserId &&
            (r.Decision == CvReviewDecision.Pending || r.Decision == CvReviewDecision.OnHold)));
    }

    /// <summary>
    /// Adayın bu süreçten çıkışına izin veren olumlu değerlendirmesi var mı? Sürece bağlı kararlar ve
    /// adayın pozisyonsuz önerildiği kararlar sayılır.
    /// </summary>
    public static Task<bool> HasPositiveCvReview(
        this IAppDbContext db, Guid candidateId, Guid applicationId, CancellationToken ct) =>
        db.CvReviews.AnyAsync(
            r => r.CandidateId == candidateId &&
                 r.Decision == CvReviewDecision.Positive &&
                 (r.ApplicationId == applicationId || r.ApplicationId == null),
            ct);

    public static bool IsTeamLead(this User user) => user.Roles.Any(r => r.Role == Role.TeamLead);
}
