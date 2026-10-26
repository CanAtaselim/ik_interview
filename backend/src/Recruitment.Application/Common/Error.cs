namespace Recruitment.Application.Common;

public enum ErrorType
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    BusinessRule,
    TooManyRequests,
}

/// <summary>Beklenen bir hata. <see cref="Message"/> kullanıcıya gösterilir, bu yüzden Türkçe yazılır.</summary>
public sealed record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    IReadOnlyDictionary<string, object?>? Extensions = null)
{
    public static Error Validation(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new("Validation.Failed", "Girdiğiniz bilgileri kontrol edin.", ErrorType.Validation, fieldErrors);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    /// <summary>İstemcinin kararı için ek veri taşıyan çakışma (ör. olası mükerrer kayıtlar).</summary>
    public static Error Conflict(string code, string message, string dataKey, object data) =>
        new(code, message, ErrorType.Conflict, Extensions: new Dictionary<string, object?> { [dataKey] = data });

    public static Error BusinessRule(string code, string message) => new(code, message, ErrorType.BusinessRule);

    public static Error TooManyRequests(string code, string message) => new(code, message, ErrorType.TooManyRequests);
}
