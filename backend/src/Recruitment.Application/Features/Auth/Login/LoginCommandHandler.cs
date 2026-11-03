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

        var directoryUser = await ldap.Authenticate(username, command.Password, ct);
        if (directoryUser is null)
        {
            throttle.RecordFailure(username);
            logger.LogWarning("Başarısız giriş denemesi: {Username} ({IpAddress})", username, command.IpAddress);
            return AuthErrors.InvalidCredentials;
        }

        throttle.Reset(username);
        return await sessions.SignIn(directoryUser, command.IpAddress, ct);
    }
}
