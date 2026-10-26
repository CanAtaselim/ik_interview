using Recruitment.Application.Abstractions;

namespace Recruitment.Application.Common.Behaviors;

/// <summary>
/// Command'ı tek transaction içinde çalıştırır. Yalnızca exception geri alır; handler
/// kaydettikten sonra bilerek hata döndürebilir (ör. çalınan refresh token'da tüm oturumları kapatmak).
/// </summary>
internal sealed class TransactionBehavior(IAppDbContext db) : IPipelineBehavior
{
    public Task<Result<TResponse>> Handle<TResponse>(
        RequestContext context,
        Func<Task<Result<TResponse>>> next,
        CancellationToken ct) =>
        context.IsCommand ? db.ExecuteInTransaction(next, ct) : next();
}
