using System.Collections.Concurrent;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Users;

namespace Recruitment.Api.IntegrationTests.Infrastructure;

/// <summary>Testlerin kullanıcıları ve grup üyeliklerini çalışma anında değiştirebildiği sahte dizin.</summary>
public sealed class TestLdapService : ILdapService
{
    public const string Password = "dogru-sifre";

    private readonly ConcurrentDictionary<string, LdapUser> _users = new();

    public const string DefaultDepartment = "Test Birimi";

    /// <summary>Verilen rollerin varsayılan AD gruplarına üye yeni bir kullanıcı ekler.</summary>
    public string AddUser(params Role[] roles) => AddUserIn(DefaultDepartment, roles);

    public string AddUserIn(string department, params Role[] roles) =>
        Add("Test Kullanıcısı", department, roles);

    public string AddUserNamed(string displayName, params Role[] roles) =>
        Add(displayName, DefaultDepartment, roles);

    private string Add(string displayName, string department, Role[] roles)
    {
        var username = $"kullanici-{Guid.NewGuid():N}";
        _users[username] = new LdapUser(
            username,
            displayName,
            $"{username}@test.local",
            department,
            "Test Unvanı",
            ManagerUsername: null,
            IsEnabled: true,
            GroupsFor(roles));

        return username;
    }

    public void SetRoles(string username, params Role[] roles) =>
        _users[username] = _users[username] with { Groups = GroupsFor(roles) };

    public void Disable(string username) => _users[username] = _users[username] with { IsEnabled = false };

    public Task<bool> Authenticate(string username, string password, CancellationToken ct) =>
        Task.FromResult(_users.ContainsKey(username) && password == Password);

    public Task<LdapUser?> FindUser(string username, CancellationToken ct) =>
        Task.FromResult(_users.GetValueOrDefault(username));

    private static HashSet<string> GroupsFor(Role[] roles) =>
        roles.Select(role => $"Recruitment-{role}").ToHashSet(StringComparer.OrdinalIgnoreCase);
}
