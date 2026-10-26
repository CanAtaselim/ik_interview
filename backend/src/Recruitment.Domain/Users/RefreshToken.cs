using Recruitment.Domain.Common;

namespace Recruitment.Domain.Users;

/// <summary>Refresh token'ın kendisi saklanmaz, yalnızca hash'i tutulur.</summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken() { }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public string? CreatedByIp { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedBy { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset expiresAt, string? createdByIp)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("Refresh token hash'i boş olamaz.");

        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedByIp = createdByIp,
        };
    }

    public bool IsExpired(TimeProvider clock) => clock.GetUtcNow() >= ExpiresAt;

    public bool IsActive(TimeProvider clock) => !IsRevoked && !IsExpired(clock);

    /// <summary>İptal eder. Zaten iptal edilmişse ilk iptal bilgisi korunur.</summary>
    public void Revoke(TimeProvider clock, Guid? replacedBy = null)
    {
        if (IsRevoked)
            return;

        RevokedAt = clock.GetUtcNow();
        ReplacedBy = replacedBy;
    }
}
