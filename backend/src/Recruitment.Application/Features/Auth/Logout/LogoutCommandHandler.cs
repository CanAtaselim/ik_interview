using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Auth.Logout;

internal sealed class LogoutCommandHandler(IAppDbContext db, ITokenService tokens, TimeProvider clock)
    : ICommandHandler<LogoutCommand, Unit>
{
    public async Task<Result<Unit>> Handle(LogoutCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            return Unit.Value;

        var hash = tokens.HashRefreshToken(command.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null)
            return Unit.Value;

        token.Revoke(clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
