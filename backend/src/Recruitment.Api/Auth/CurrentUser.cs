using System.Security.Claims;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Users;

namespace Recruitment.Api.Auth;

/// <summary>Access token claim'lerinden okunan kullanıcı. İstek dışında (seed, job) oturumsuzdur.</summary>
internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private IReadOnlySet<Role>? _roles;

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId => Guid.TryParse(Principal?.FindFirstValue(AuthClaims.Subject), out var id) ? id : Guid.Empty;

    public string Username => Principal?.FindFirstValue(AuthClaims.Name) ?? string.Empty;

    public IReadOnlySet<Role> Roles => _roles ??= ReadRoles();

    public Guid? DepartmentId =>
        Guid.TryParse(Principal?.FindFirstValue(AuthClaims.Department), out var id) ? id : null;

    // Şimdilik tek scope var; Faz 3'te claim değerinden okunacak.
    public AccessScope Scope => AccessScope.Full;

    public bool IsInRole(Role role) => Roles.Contains(role);

    private HashSet<Role> ReadRoles() =>
        (Principal?.FindAll(AuthClaims.Role) ?? [])
        .Select(claim => Enum.TryParse<Role>(claim.Value, out var role) ? role : (Role?)null)
        .OfType<Role>()
        .ToHashSet();
}
