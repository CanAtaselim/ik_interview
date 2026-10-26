using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Recruitment.Api.Middleware;

/// <summary>Beklenmeyen hatayı loglar ve kullanıcıya iç detay sızdırmayan genel bir mesaj döner.</summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Kullanıcı sayfadan ayrıldı veya isteği iptal etti: hata değil, yanıt bekleyen de yok.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation(
                "İstek istemci tarafından iptal edildi: {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        logger.LogError(
            exception, "Beklenmeyen hata: {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Beklenmeyen bir hata oluştu",
                Detail = "İşleminiz tamamlanamadı. Lütfen daha sonra tekrar deneyin.",
            },
        });
    }
}
