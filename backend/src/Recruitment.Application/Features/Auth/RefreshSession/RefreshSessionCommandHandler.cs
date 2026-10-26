using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Auth.RefreshSession;

internal sealed class RefreshSessionCommandHandler(
    IAppDbContext db,
    ILdapService ldap,
    ITokenService tokens,
    SessionIssuer sessions,
    TimeProvider clock,
    ILogger<RefreshSessionCommandHandler> logger) : ICommandHandler<RefreshSessionCommand, AuthSession>
{
    // Aynı anda açılan sekmeler aynı token'la yenileme isteyebilir; ikinci gelen, az önce
    // değiştirilmiş token'ı sunar. Bu kısa pencerede çalınma sayılmaz, yalnızca reddedilir.
    private static readonly TimeSpan RotationGracePeriod = TimeSpan.FromSeconds(30);

    public async Task<Result<AuthSession>> Handle(RefreshSessionCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            return AuthErrors.SessionExpired;

        var hash = tokens.HashRefreshToken(command.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null)
            return AuthErrors.SessionExpired;

        if (token.IsRevoked)
        {
            var justRotated = token.ReplacedBy is not null &&
                              clock.GetUtcNow() - token.RevokedAt < RotationGracePeriod;
            if (!justRotated)
                await RevokeAllSessions(token.UserId, ct);

            return AuthErrors.SessionExpired;
        }

        if (token.IsExpired(clock))
            return AuthErrors.SessionExpired;

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == token.UserId, ct);
        var directoryUser = user is null ? null : await ldap.FindUser(user.Username, ct);

        if (directoryUser is null || !directoryUser.IsEnabled)
        {
            token.Revoke(clock);
            await db.SaveChangesAsync(ct);
            return AuthErrors.SessionExpired;
        }

        return await sessions.Issue(directoryUser, command.IpAddress, isLogin: false, replaces: token, ct);
    }

    // İptal edilmiş token'ın yeniden kullanılması çalınma işaretidir: kullanıcının tüm oturumları kapatılır.
    private async Task RevokeAllSessions(Guid userId, CancellationToken ct)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var session in active)
            session.Revoke(clock);

        await db.SaveChangesAsync(ct);

        logger.LogWarning(
            "İptal edilmiş refresh token yeniden kullanıldı; kullanıcının {SessionCount} oturumu kapatıldı: {UserId}",
            active.Count, userId);
    }
}
