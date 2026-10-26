using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Auth;

public static class AuthErrors
{
    // Kullanıcı adının mı şifrenin mi hatalı olduğu bilerek söylenmez.
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "Kullanıcı adı veya şifre hatalı.");

    public static readonly Error LockedOut = Error.TooManyRequests(
        "Auth.LockedOut", "Çok fazla hatalı deneme yapıldı. Lütfen 5 dakika sonra tekrar deneyin.");

    public static readonly Error NoAccess =
        Error.Forbidden("Auth.NoAccess", "Şu anda aktif bir referans çağrısı bulunmuyor.");

    public static readonly Error SessionExpired =
        Error.Unauthorized("Auth.SessionExpired", "Oturumunuzun süresi doldu. Lütfen tekrar giriş yapın.");
}
