using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.CvReviews;

namespace Recruitment.Application.Features.CvReviews.GetCvReviews;

public enum CvReviewStatusFilter
{
    /// <summary>Karar bekleyenler ve bekletilenler.</summary>
    Waiting,

    /// <summary>Olumlu veya olumsuz sonuçlananlar.</summary>
    Decided,

    /// <summary>Karar bekleyen ve süresi dolmuş olanlar.</summary>
    Overdue,
}

/// <summary>
/// CV değerlendirmeleri. İK hepsini, takım lideri kendisine veya birimine önerilenleri görür.
/// </summary>
/// <param name="Mine">Yalnızca isteği yapan kullanıcıya önerilenler.</param>
/// <param name="CandidateId">Verilirse yalnızca bu adayın değerlendirmeleri (aday kartı).</param>
public sealed record GetCvReviewsQuery(
    CvReviewStatusFilter? Status,
    bool Mine,
    Guid? CandidateId,
    string? Search,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<CvReviewListItemDto>>;

internal sealed class GetCvReviewsQueryHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : IQueryHandler<GetCvReviewsQuery, PagedResult<CvReviewListItemDto>>
{
    public async Task<Result<PagedResult<CvReviewListItemDto>>> Handle(GetCvReviewsQuery query, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var reviews = db.VisibleCvReviews(currentUser);

        reviews = query.Status switch
        {
            CvReviewStatusFilter.Waiting => reviews.Where(
                r => r.Decision == CvReviewDecision.Pending || r.Decision == CvReviewDecision.OnHold),
            CvReviewStatusFilter.Decided => reviews.Where(
                r => r.Decision == CvReviewDecision.Positive || r.Decision == CvReviewDecision.Negative),
            CvReviewStatusFilter.Overdue => reviews.Where(r => r.Decision == CvReviewDecision.Pending && r.DueAt < now),
            _ => reviews,
        };

        if (query.Mine)
            reviews = reviews.Where(r => r.ReviewerUserId == currentUser.UserId);
        if (query.CandidateId is { } candidateId)
            reviews = reviews.Where(r => r.CandidateId == candidateId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var matching = db.Candidates.WhereAnyContains(query.Search, c => c.FirstName + " " + c.LastName);
            reviews = reviews.Where(r => matching.Any(c => c.Id == r.CandidateId));
        }

        // Önce karar bekleyenler (süresi en yakın olan üstte), sonra sonuçlananlar (en yeni üstte).
        return await reviews
            .OrderBy(r => r.Decision == CvReviewDecision.Positive || r.Decision == CvReviewDecision.Negative)
            .ThenByDescending(r =>
                r.Decision == CvReviewDecision.Positive || r.Decision == CvReviewDecision.Negative ? r.DecidedAt : null)
            .ThenBy(r => r.DueAt)
            .ToListItems(db, currentUser.UserId, now)
            .ToPagedResult(query.Page, query.PageSize, ct);
    }
}
