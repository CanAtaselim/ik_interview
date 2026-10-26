using System.Linq.Expressions;

namespace Recruitment.Domain.Common;

/// <summary>
/// Bir kayıt kümesini tanımlayan, adı olan filtre kuralı. Sorgularda
/// <c>query.Where(spec.ToExpression())</c> ile uygulanır; birden çok kural art arda eklenebilir.
/// </summary>
public abstract class Specification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();

    public bool IsSatisfiedBy(T candidate) => ToExpression().Compile()(candidate);
}
