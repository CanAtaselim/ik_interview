using System.Linq.Expressions;
using Recruitment.Domain.Common;

namespace Recruitment.Domain.Positions.Specifications;

/// <summary>İK tüm pozisyonları, diğer kullanıcılar yalnızca kendi birimlerinin pozisyonlarını görür.</summary>
public sealed class PositionVisibleTo(bool seesAll, Guid? departmentId) : Specification<Position>
{
    public override Expression<Func<Position, bool>> ToExpression() =>
        p => seesAll || (departmentId != null && p.DepartmentId == departmentId);
}
