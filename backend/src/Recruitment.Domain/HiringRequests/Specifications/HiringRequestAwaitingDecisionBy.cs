using System.Linq.Expressions;
using Recruitment.Domain.Common;
using Recruitment.Domain.Users;

namespace Recruitment.Domain.HiringRequests.Specifications;

/// <summary>
/// Sıradaki onayı bu kullanıcının verebileceği talepler.
/// <see cref="HiringRequest.CanBeDecidedBy"/> ile aynı kuralın sorgu karşılığıdır.
/// </summary>
public sealed class HiringRequestAwaitingDecisionBy(IReadOnlyCollection<Role> roles, Guid? departmentId)
    : Specification<HiringRequest>
{
    private readonly List<Role?> _roles = roles.Select(role => (Role?)role).ToList();

    public override Expression<Func<HiringRequest, bool>> ToExpression() =>
        r => r.Status == HiringRequestStatus.PendingApproval &&
             _roles.Contains(r.AwaitingRole) &&
             (r.AwaitingRole != Role.DepartmentManager || r.DepartmentId == departmentId);
}
