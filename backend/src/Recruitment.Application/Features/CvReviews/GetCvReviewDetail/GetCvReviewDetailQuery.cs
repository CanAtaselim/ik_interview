using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Candidates;

namespace Recruitment.Application.Features.CvReviews.GetCvReviewDetail;

/// <summary>
/// Değerlendirme ve adayın kısıtlı görünümü (CV, ön görüşme özeti). İK yorumu, ücret, iletişim ve
/// kara liste bilgisi bu yanıtta yer almaz.
/// </summary>
public sealed record GetCvReviewDetailQuery(Guid Id) : IQuery<CvReviewDetailDto>;

internal sealed class GetCvReviewDetailQueryHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : IQueryHandler<GetCvReviewDetailQuery, CvReviewDetailDto>
{
    public async Task<Result<CvReviewDetailDto>> Handle(GetCvReviewDetailQuery query, CancellationToken ct)
    {
        var review = await db.VisibleCvReviews(currentUser)
            .Where(r => r.Id == query.Id)
            .ToListItems(db, currentUser.UserId, clock.GetUtcNow())
            .FirstOrDefaultAsync(ct);
        if (review is null)
            return CvReviewErrors.NotFound;

        var candidate = await db.LoadRestrictedCandidate(review.CandidateId, ct);
        if (candidate is null)
            return CandidateErrors.NotFound;

        return new CvReviewDetailDto(review, candidate);
    }
}
