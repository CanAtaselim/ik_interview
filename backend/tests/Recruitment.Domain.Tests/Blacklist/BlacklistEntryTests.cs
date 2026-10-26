using Microsoft.Extensions.Time.Testing;
using Recruitment.Domain.Blacklist;
using Recruitment.Domain.Common;
using Shouldly;

namespace Recruitment.Domain.Tests.Blacklist;

public sealed class BlacklistEntryTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero));
    private readonly Guid _specialist = Guid.CreateVersion7();
    private readonly Guid _manager = Guid.CreateVersion7();

    private BlacklistEntry Requested(DateTimeOffset? expiresAt = null, bool approveImmediately = false) =>
        BlacklistEntry.Request(
            Guid.CreateVersion7(), Guid.CreateVersion7(), " Mülakata haber vermeden gelmedi. ", expiresAt,
            approveImmediately ? _manager : _specialist, approveImmediately, _clock);

    [Fact]
    public void Request_BySpecialist_WaitsForApprovalAndIsNotEffective()
    {
        var entry = Requested();

        entry.Status.ShouldBe(BlacklistStatus.PendingApproval);
        entry.Description.ShouldBe("Mülakata haber vermeden gelmedi.");
        entry.IsEffective(_clock).ShouldBeFalse();
        entry.ApprovedByUserId.ShouldBeNull();
    }

    [Fact]
    public void Request_ByManager_IsEffectiveImmediately()
    {
        var entry = Requested(approveImmediately: true);

        entry.Status.ShouldBe(BlacklistStatus.Active);
        entry.ApprovedByUserId.ShouldBe(_manager);
        entry.ApprovedAt.ShouldBe(_clock.GetUtcNow());
        entry.IsEffective(_clock).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_WithoutDescription_Throws(string description)
    {
        Should.Throw<DomainException>(() => BlacklistEntry.Request(
            Guid.CreateVersion7(), Guid.CreateVersion7(), description, null, _specialist, false, _clock));
    }

    [Fact]
    public void Request_WithoutReasonOrWithPastExpiry_Throws()
    {
        Should.Throw<DomainException>(() => BlacklistEntry.Request(
            Guid.CreateVersion7(), Guid.Empty, "Gerekçe", null, _specialist, false, _clock));
        Should.Throw<DomainException>(() => Requested(expiresAt: _clock.GetUtcNow()));
    }

    [Fact]
    public void Approve_WhenPending_ActivatesEntry()
    {
        var entry = Requested();

        entry.Approve(_manager, _clock);

        entry.Status.ShouldBe(BlacklistStatus.Active);
        entry.ApprovedByUserId.ShouldBe(_manager);
    }

    [Fact]
    public void Approve_WhenNotPending_Throws()
    {
        Should.Throw<DomainException>(() => Requested(approveImmediately: true).Approve(_manager, _clock));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Remove_PendingOrActive_WithReason_RemovesEntry(bool active)
    {
        var entry = Requested(approveImmediately: active);

        entry.Remove(_manager, " Yanlış kişi ", _clock);

        entry.Status.ShouldBe(BlacklistStatus.Removed);
        entry.RemovedByUserId.ShouldBe(_manager);
        entry.RemovalReason.ShouldBe("Yanlış kişi");
        entry.IsEffective(_clock).ShouldBeFalse();
    }

    [Fact]
    public void Remove_WithoutReasonOrWhenAlreadyRemoved_Throws()
    {
        var entry = Requested(approveImmediately: true);

        Should.Throw<DomainException>(() => entry.Remove(_manager, " ", _clock));
        entry.Remove(_manager, "Gerekçe", _clock);
        Should.Throw<DomainException>(() => entry.Remove(_manager, "Gerekçe", _clock));
    }

    [Fact]
    public void ExpireIfDue_OnlyExpiresActiveEntriesPastTheirDate()
    {
        var permanent = Requested(approveImmediately: true);
        var temporary = Requested(expiresAt: _clock.GetUtcNow().AddDays(30), approveImmediately: true);
        var pending = Requested(expiresAt: _clock.GetUtcNow().AddDays(30));

        _clock.Advance(TimeSpan.FromDays(29));
        temporary.ExpireIfDue(_clock).ShouldBeFalse();
        temporary.IsEffective(_clock).ShouldBeTrue();

        _clock.Advance(TimeSpan.FromDays(1));
        temporary.IsEffective(_clock).ShouldBeFalse();
        temporary.ExpireIfDue(_clock).ShouldBeTrue();
        permanent.ExpireIfDue(_clock).ShouldBeFalse();
        pending.ExpireIfDue(_clock).ShouldBeFalse();

        temporary.Status.ShouldBe(BlacklistStatus.Expired);
        permanent.Status.ShouldBe(BlacklistStatus.Active);
        pending.Status.ShouldBe(BlacklistStatus.PendingApproval);
    }
}
