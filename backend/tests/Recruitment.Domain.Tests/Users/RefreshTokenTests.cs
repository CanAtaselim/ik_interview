using Microsoft.Extensions.Time.Testing;
using Recruitment.Domain.Common;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Domain.Tests.Users;

public sealed class RefreshTokenTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 9, 8, 0, 0, TimeSpan.Zero));

    private RefreshToken IssueToken() =>
        RefreshToken.Issue(Guid.CreateVersion7(), "hash", _clock.GetUtcNow().AddDays(7), "10.0.0.1");

    [Fact]
    public void Issue_WithBlankHash_Throws()
    {
        Should.Throw<DomainException>(() =>
            RefreshToken.Issue(Guid.CreateVersion7(), " ", _clock.GetUtcNow().AddDays(7), null));
    }

    [Fact]
    public void IsActive_WhenFresh_ReturnsTrue()
    {
        IssueToken().IsActive(_clock).ShouldBeTrue();
    }

    [Fact]
    public void IsActive_WhenExpired_ReturnsFalse()
    {
        var token = IssueToken();

        _clock.Advance(TimeSpan.FromDays(7));

        token.IsExpired(_clock).ShouldBeTrue();
        token.IsActive(_clock).ShouldBeFalse();
    }

    [Fact]
    public void Revoke_WhenActive_MarksRevokedWithReplacement()
    {
        var token = IssueToken();
        var replacement = Guid.CreateVersion7();

        token.Revoke(_clock, replacement);

        token.IsRevoked.ShouldBeTrue();
        token.IsActive(_clock).ShouldBeFalse();
        token.RevokedAt.ShouldBe(_clock.GetUtcNow());
        token.ReplacedBy.ShouldBe(replacement);
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_KeepsFirstRevocation()
    {
        var token = IssueToken();
        var replacement = Guid.CreateVersion7();
        token.Revoke(_clock, replacement);
        var firstRevokedAt = token.RevokedAt;

        _clock.Advance(TimeSpan.FromMinutes(5));
        token.Revoke(_clock);

        token.RevokedAt.ShouldBe(firstRevokedAt);
        token.ReplacedBy.ShouldBe(replacement);
    }
}
