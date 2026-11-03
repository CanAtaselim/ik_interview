using Recruitment.Domain.Users;

namespace Recruitment.Application.Abstractions;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <param name="Value">İstemciye verilen ham değer; saklanmaz.</param>
/// <param name="Hash">Veritabanında saklanan hash.</param>
public sealed record RefreshTokenValue(string Value, string Hash, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user, IReadOnlyCollection<Role> roles, AccessScope scope);

    /// <param name="expiresAt">Verilirse süre uzatılmaz, bu bitiş kullanılır (yenilemede ilk girişin bitişi).</param>
    RefreshTokenValue CreateRefreshToken(DateTimeOffset? expiresAt = null);

    string HashRefreshToken(string value);
}
