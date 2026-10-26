using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Recruitment.Application.Abstractions;

namespace Recruitment.Application.Common.Behaviors;

// İstek içeriği loglanmaz: kişisel veri ve şifre barındırabilir.
internal sealed class LoggingBehavior(ILogger<LoggingBehavior> logger) : IPipelineBehavior
{
    public async Task<Result<TResponse>> Handle<TResponse>(
        RequestContext context,
        Func<Task<Result<TResponse>>> next,
        CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await next();
        var elapsedMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        if (result.IsSuccess)
            logger.LogInformation("{Request} tamamlandı ({ElapsedMs} ms)", context.Name, elapsedMs);
        else
            logger.LogWarning(
                "{Request} başarısız: {ErrorCode} ({ElapsedMs} ms)", context.Name, result.Error.Code, elapsedMs);

        return result;
    }
}
