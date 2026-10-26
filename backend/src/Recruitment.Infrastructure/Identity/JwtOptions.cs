using System.ComponentModel.DataAnnotations;

namespace Recruitment.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = default!;

    [Required]
    public string Audience { get; init; } = default!;

    /// <summary>HS256 anahtarı; en az 32 byte. Repoya yazılmaz (user-secrets / ortam değişkeni).</summary>
    [Required, MinLength(32, ErrorMessage = "Jwt:SigningKey en az 32 karakter olmalı.")]
    public string SigningKey { get; init; } = default!;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; init; } = 7;
}
