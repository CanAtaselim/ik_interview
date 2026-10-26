using Recruitment.Domain.Common;

namespace Recruitment.Domain.Users;

/// <summary>
/// Şirket çalışanı. İlk LDAP girişinde oluşur, her girişte dizinden güncellenir;
/// profil ve roller uygulama içinden elle değiştirilmez.
/// </summary>
public sealed class User : AggregateRoot
{
    private readonly List<UserRole> _roles = [];

    private User() { }

    public string Username { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string? Email { get; private set; }
    public string? Department { get; private set; }

    /// <summary>Dizindeki birim adının sözlükteki karşılığı; veri bazlı yetkide kullanılır.</summary>
    public Guid? DepartmentId { get; private set; }
    public string? Title { get; private set; }
    public string? ManagerUsername { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public IReadOnlyCollection<UserRole> Roles => _roles;

    public static User Create(string username, string displayName)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new DomainException("Kullanıcı adı boş olamaz.");

        return new User
        {
            Username = username,
            DisplayName = NullIfBlank(displayName) ?? username,
            IsActive = true,
        };
    }

    public void SyncFromDirectory(
        string displayName,
        string? email,
        string? department,
        string? title,
        string? managerUsername,
        bool isEnabled)
    {
        DisplayName = NullIfBlank(displayName) ?? Username;
        Email = NullIfBlank(email)?.ToLowerInvariant();
        Department = NullIfBlank(department);
        Title = NullIfBlank(title);
        ManagerUsername = NullIfBlank(managerUsername);
        IsActive = isEnabled;
    }

    public void SyncRoles(IEnumerable<Role> roles)
    {
        var target = roles.ToHashSet();
        _roles.RemoveAll(r => !target.Contains(r.Role));
        foreach (var role in target.Where(role => !HasRole(role)))
            _roles.Add(new UserRole(role));
    }

    public void AssignDepartment(Guid? departmentId) => DepartmentId = departmentId;

    public bool HasRole(Role role) => _roles.Any(r => r.Role == role);

    public void RecordLogin(TimeProvider clock) => LastLoginAt = clock.GetUtcNow();

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
