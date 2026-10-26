using Recruitment.Domain.Users;

namespace Recruitment.Application.Abstractions;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <param name="Value">İstemciye verilen ham değer; saklanmaz.</param>
/// <param name="Hash">Veritabanında saklanan hash.</param>
public sealed record RefreshTokenValue(string Value, string Hash, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user, IReadOnlyCollection<Role> roles, AccessScope scope);

    RefreshTokenValue CreateRefreshToken();

    string HashRefreshToken(string value);
}
