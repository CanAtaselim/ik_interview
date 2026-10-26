using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Users;

public sealed class UserSearchEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/v1/users";

    [Fact]
    public async Task Search_ByRoles_ReturnsOnlyUsersHavingAnyOfThem()
    {
        var hrSpecialist = await factory.LoginAs(Role.HrSpecialist);
        var hrManager = await factory.LoginAs(Role.HrManager, Role.TeamLead);
        var teamLead = await factory.LoginAs(Role.TeamLead);

        var hrUsers = (await teamLead.GetJson($"{Url}?roles=HrSpecialist&roles=HrManager&pageSize=100")).Ids().ToList();

        hrUsers.ShouldContain(hrSpecialist.Id);
        hrUsers.ShouldContain(hrManager.Id);
        hrUsers.ShouldNotContain(teamLead.Id);
    }

    [Theory]
    [InlineData("IŞIL DEMİR")]
    [InlineData("ışıl demir")]
    [InlineData("işil demır")]
    [InlineData("Işıl Demir")]
    public async Task Search_ByName_IgnoresCaseAndDottedOrDotlessI(string typed)
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        var username = factory.Ldap.AddUserNamed($"Işıl Demir {marker}", Role.TeamLead);
        var user = await factory.LoginExisting(username);

        var found = (await user.GetJson($"{Url}?search={Uri.EscapeDataString($"{typed} {marker}")}")).Ids();

        found.ShouldBe([user.Id]);
    }
}
