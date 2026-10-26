using Microsoft.Extensions.Logging;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Auth.Login;

internal sealed class LoginCommandHandler(
    ILdapService ldap,
    ILoginThrottle throttle,
    SessionIssuer sessions,
    ILogger<LoginCommandHandler> logger) : ICommandHandler<LoginCommand, AuthSession>
{
    public async Task<Result<AuthSession>> Handle(LoginCommand command, CancellationToken ct)
    {
        var username = UsernameNormalizer.Normalize(command.Username);

        if (throttle.IsLockedOut(username))
            return AuthErrors.LockedOut;

        if (!await ldap.Authenticate(username, command.Password, ct))
        {
            throttle.RecordFailure(username);
            logger.LogWarning("Başarısız giriş denemesi: {Username} ({IpAddress})", username, command.IpAddress);
            return AuthErrors.InvalidCredentials;
        }

        var directoryUser = await ldap.FindUser(username, ct);
        if (directoryUser is null)
        {
            logger.LogWarning("Kimliği doğrulanan kullanıcı dizinde bulunamadı: {Username}", username);
            return AuthErrors.InvalidCredentials;
        }

        throttle.Reset(username);
        return await sessions.Issue(directoryUser, command.IpAddress, isLogin: true, replaces: null, ct);
    }
}
