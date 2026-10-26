namespace Recruitment.Domain.Users;

public sealed class UserRole
{
    private UserRole() { }

    internal UserRole(Role role) => Role = role;

    public Role Role { get; private set; }
}
