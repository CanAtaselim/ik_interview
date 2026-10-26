using Microsoft.AspNetCore.Mvc;
using Recruitment.Application.Common;

namespace Recruitment.Api.Controllers;

internal static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
    {
        if (result.IsFailure)
            return result.Error.ToProblemResult(controller);

        return result.Value is Unit ? controller.NoContent() : controller.Ok(result.Value);
    }

    /// <summary>Yeni kayıt oluşturan command'ın sonucunu 201 ve <c>{ id }</c> gövdesiyle döner.</summary>
    public static IActionResult ToCreatedResult(this Result<Guid> result, ControllerBase controller) =>
        result.IsFailure
            ? result.Error.ToProblemResult(controller)
            : controller.StatusCode(StatusCodes.Status201Created, new { id = result.Value });

    /// <summary>Hatayı RFC 7807 ProblemDetails'e çevirir; alan hataları <c>errors</c> içinde döner.</summary>
    public static IActionResult ToProblemResult(this Error error, ControllerBase controller)
    {
        var (status, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Bilgiler geçersiz"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Oturum açılamadı"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Yetkiniz yok"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Kayıt bulunamadı"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Kayıt çakışması"),
            ErrorType.BusinessRule => (StatusCodes.Status422UnprocessableEntity, "İşlem yapılamadı"),
            ErrorType.TooManyRequests => (StatusCodes.Status429TooManyRequests, "Çok fazla deneme"),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Type, null),
        };

        ProblemDetails problem = error.FieldErrors is { } fieldErrors
            ? new ValidationProblemDetails(fieldErrors.ToDictionary(e => e.Key, e => e.Value))
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = title;
        problem.Detail = error.Message;
        problem.Extensions["code"] = error.Code;
        foreach (var (key, value) in error.Extensions ?? new Dictionary<string, object?>())
            problem.Extensions[key] = value;
        problem.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;

        return new ObjectResult(problem) { StatusCode = status };
    }
}
