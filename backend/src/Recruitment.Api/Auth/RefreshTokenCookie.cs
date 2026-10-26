namespace Recruitment.Api.Auth;

/// <summary>
/// Refresh token yalnızca HttpOnly cookie ile taşınır; JavaScript okuyamaz ve yalnızca
/// auth endpoint'lerine gönderilir.
/// </summary>
internal static class RefreshTokenCookie
{
    private const string Name = "recruitment_refresh";
    private const string Path = "/api/v1/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Write(HttpResponse response, string value, DateTimeOffset expiresAt, bool secure) =>
        response.Cookies.Append(Name, value, Options(secure, expiresAt));

    public static void Delete(HttpResponse response, bool secure) =>
        response.Cookies.Delete(Name, Options(secure, expiresAt: null));

    private static CookieOptions Options(bool secure, DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,
    };
}
