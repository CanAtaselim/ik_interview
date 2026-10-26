using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Identity;

internal sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private const int RefreshTokenBytes = 64;

    private static readonly JsonWebTokenHandler Handler = new();

    private readonly JwtOptions _options = options.Value;

    public AccessToken CreateAccessToken(User user, IReadOnlyCollection<Role> roles, AccessScope scope)
    {
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new Dictionary<string, object>
        {
            [AuthClaims.Subject] = user.Id.ToString(),
            [AuthClaims.Name] = user.Username,
            [AuthClaims.DisplayName] = user.DisplayName,
            [AuthClaims.Role] = roles.Select(r => r.ToString()).ToArray(),
            [AuthClaims.Scope] = scope.ToClaimValue(),
        };
        if (user.Email is not null)
            claims[AuthClaims.Email] = user.Email;
        if (user.DepartmentId is { } departmentId)
            claims[AuthClaims.Department] = departmentId.ToString();

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expiresAt);
    }

    public RefreshTokenValue CreateRefreshToken()
    {
        var value = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RefreshTokenBytes));
        var expiresAt = clock.GetUtcNow().AddDays(_options.RefreshTokenDays);

        return new RefreshTokenValue(value, HashRefreshToken(value), expiresAt);
    }

    // Token yüksek entropili rastgele bir değer; tuzsuz SHA-256 yeterli ve aranabilir.
    public string HashRefreshToken(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
