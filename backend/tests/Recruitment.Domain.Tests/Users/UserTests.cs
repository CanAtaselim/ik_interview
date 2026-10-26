using Microsoft.Extensions.Time.Testing;
using Recruitment.Domain.Common;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Domain.Tests.Users;

public sealed class UserTests
{
    [Fact]
    public void Create_WithBlankUsername_Throws()
    {
        Should.Throw<DomainException>(() => User.Create("  ", "Ayşe Yılmaz"));
    }

    [Fact]
    public void Create_WithBlankDisplayName_FallsBackToUsername()
    {
        var user = User.Create("ayse.yilmaz", " ");

        user.DisplayName.ShouldBe("ayse.yilmaz");
        user.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void SyncRoles_WithDifferentSet_AddsMissingAndRemovesExtra()
    {
        var user = User.Create("ayse.yilmaz", "Ayşe Yılmaz");
        user.SyncRoles([Role.HrSpecialist, Role.TeamLead]);

        user.SyncRoles([Role.TeamLead, Role.HrManager]);

        user.Roles.Select(r => r.Role).ShouldBe([Role.TeamLead, Role.HrManager], ignoreOrder: true);
        user.HasRole(Role.HrSpecialist).ShouldBeFalse();
    }

    [Fact]
    public void SyncRoles_WithDuplicates_KeepsEachRoleOnce()
    {
        var user = User.Create("ayse.yilmaz", "Ayşe Yılmaz");

        user.SyncRoles([Role.TeamLead, Role.TeamLead]);
        user.SyncRoles([Role.TeamLead]);

        user.Roles.Count.ShouldBe(1);
    }

    [Fact]
    public void SyncRoles_WithEmptySet_RemovesAllRoles()
    {
        var user = User.Create("ayse.yilmaz", "Ayşe Yılmaz");
        user.SyncRoles([Role.HrManager]);

        user.SyncRoles([]);

        user.Roles.ShouldBeEmpty();
    }

    [Fact]
    public void SyncFromDirectory_WithBlankValues_StoresNullAndLowercasesEmail()
    {
        var user = User.Create("ayse.yilmaz", "Ayşe Yılmaz");

        user.SyncFromDirectory("Ayşe Yılmaz Kaya", " Ayse.Yilmaz@Ornek.Local ", "", "  ", null, isEnabled: false);

        user.DisplayName.ShouldBe("Ayşe Yılmaz Kaya");
        user.Email.ShouldBe("ayse.yilmaz@ornek.local");
        user.Department.ShouldBeNull();
        user.Title.ShouldBeNull();
        user.ManagerUsername.ShouldBeNull();
        user.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void RecordLogin_SetsLastLoginAtToCurrentTime()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 9, 8, 30, 0, TimeSpan.Zero));
        var user = User.Create("ayse.yilmaz", "Ayşe Yılmaz");

        user.RecordLogin(clock);

        user.LastLoginAt.ShouldBe(clock.GetUtcNow());
    }
}
