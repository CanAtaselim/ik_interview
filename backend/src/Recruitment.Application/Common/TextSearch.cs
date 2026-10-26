using System.Linq.Expressions;
using System.Reflection;
using Recruitment.Domain.Common;

namespace Recruitment.Application.Common;

/// <summary>
/// Listelerdeki metin araması. Büyük/küçük harfe ve Türkçedeki noktalı/noktasız i ayrımına
/// duyarsızdır: "DEMİR", "demir" ve "DEMIR" aynı kayıtları bulur. Hem aranan metin hem kolon
/// aynı biçimde sadeleştirilir, böylece sonuç veritabanının dil ayarına bağlı kalmaz.
/// </summary>
public static class TextSearch
{
    private static readonly MethodInfo ToLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly MethodInfo Replace =
        typeof(string).GetMethod(nameof(string.Replace), [typeof(string), typeof(string)])!;
    private static readonly MethodInfo Contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    /// <summary>
    /// <paramref name="term"/> boş değilse, verilen alanlardan en az birinde geçen kayıtları bırakır.
    /// </summary>
    public static IQueryable<T> WhereAnyContains<T>(
        this IQueryable<T> query, string? term, params Expression<Func<T, string>>[] fields)
    {
        if (string.IsNullOrWhiteSpace(term))
            return query;

        // Sabit yerine nesne üzerinden okunur ki EF değeri sorguya parametre olarak geçirsin.
        var needle = Expression.Property(Expression.Constant(new SearchTerm(Fold(term))), nameof(SearchTerm.Value));
        var parameter = Expression.Parameter(typeof(T), "x");

        var matches = fields
            .Select(field => new ParameterReplacer(field.Parameters[0], parameter).Visit(field.Body))
            .Select(field => (Expression)Expression.Call(FoldColumn(field), Contains, needle))
            .Aggregate(Expression.OrElse);

        return query.Where(Expression.Lambda<Func<T, bool>>(matches, parameter));
    }

    /// <summary>Aranan metni, aday aramasıyla aynı kuralla sadeleştirir.</summary>
    public static string Fold(string text) => SearchText.Fold(text);

    // Kolon tarafında aynı sadeleştirme: replace(replace(lower(kolon), 'ı', 'i'), 'i̇', 'i').
    // İkinci değişim, bazı dil ayarlarında lower('İ') sonucunda oluşan "i + birleşen nokta" içindir.
    private static Expression FoldColumn(Expression column)
    {
        var lowered = Expression.Call(column, ToLower);
        var dotless = Expression.Call(lowered, Replace, Expression.Constant("ı"), Expression.Constant("i"));
        return Expression.Call(dotless, Replace, Expression.Constant("i̇"), Expression.Constant("i"));
    }

    private sealed record SearchTerm(string Value);

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
