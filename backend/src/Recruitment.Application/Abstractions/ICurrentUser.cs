using Recruitment.Domain.Users;

namespace Recruitment.Application.Abstractions;

/// <summary>İsteği yapan kullanıcı. Veri bazlı yetki kontrolleri handler'da bunun üzerinden yapılır.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string Username { get; }
    IReadOnlySet<Role> Roles { get; }
    Guid? DepartmentId { get; }
    AccessScope Scope { get; }

    bool IsInRole(Role role);

    bool IsHr => IsInRole(Role.HrSpecialist) || IsInRole(Role.HrManager);
}
