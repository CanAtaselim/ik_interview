using System.Linq.Expressions;
using Recruitment.Domain.Common;

namespace Recruitment.Domain.HiringRequests.Specifications;

/// <summary>
/// Kullanıcının görebildiği talepler: kendi talepleri (taslak dahil) ve, onaya gönderilmiş olanlar
/// arasından, İK ise hepsi, departman yöneticisi ise kendi biriminin talepleri.
/// </summary>
public sealed class HiringRequestVisibleTo(Guid userId, bool seesAllSubmitted, Guid? managedDepartmentId)
    : Specification<HiringRequest>
{
    public override Expression<Func<HiringRequest, bool>> ToExpression() =>
        r => r.RequestedByUserId == userId ||
             (r.Status != HiringRequestStatus.Draft &&
              (seesAllSubmitted || (managedDepartmentId != null && r.DepartmentId == managedDepartmentId)));
}
