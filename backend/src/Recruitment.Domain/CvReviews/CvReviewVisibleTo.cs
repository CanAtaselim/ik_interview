using System.Linq.Expressions;
using Recruitment.Domain.Common;

namespace Recruitment.Domain.CvReviews;

/// <summary>
/// İK tüm değerlendirmeleri görür; takım lideri yalnızca kendisine veya birimine önerilenleri.
/// </summary>
public sealed class CvReviewVisibleTo(Guid userId, bool seesAll, Guid? departmentId) : Specification<CvReview>
{
    public override Expression<Func<CvReview, bool>> ToExpression() =>
        r => seesAll || r.ReviewerUserId == userId || (departmentId != null && r.DepartmentId == departmentId);
}
