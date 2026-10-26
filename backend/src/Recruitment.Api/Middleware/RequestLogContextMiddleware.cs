using System.Security.Claims;
using Recruitment.Application.Abstractions;
using Serilog;
using Serilog.Context;

namespace Recruitment.Api.Middleware;

/// <summary>İstek boyunca yazılan her loga CorrelationId ve kullanıcı adını ekler.</summary>
internal sealed class RequestLogContextMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context, IDiagnosticContext diagnostics)
    {
        var correlationId = ReadCorrelationId(context);
        var username = context.User.FindFirstValue(AuthClaims.Name) ?? "-";

        context.Response.Headers[HeaderName] = correlationId;
        diagnostics.Set("CorrelationId", correlationId);
        diagnostics.Set("Username", username);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("Username", username))
        {
            await next(context);
        }
    }

    // İstemciden gelen değer loga yazılacağı için yalnızca güvenli karakterler kabul edilir.
    private static string ReadCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var isUsable = incoming.Length is > 0 and <= MaxLength &&
                       incoming.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

        return isUsable ? incoming : context.TraceIdentifier;
    }
}
