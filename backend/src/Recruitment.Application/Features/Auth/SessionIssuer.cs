using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Auth;

/// <summary>
/// Giriş ve yenilemenin ortak adımı: kullanıcıyı dizinden gelen bilgiyle eşitler, rollerini
/// AD gruplarından yeniden çözer ve yeni token çifti üretir.
/// </summary>
internal sealed class SessionIssuer(IAppDbContext db, ITokenService tokens, TimeProvider clock)
{
    /// <param name="replaces">Yenilemede kullanılan eski refresh token; her durumda iptal edilir.</param>
    public async Task<Result<AuthSession>> Issue(
        LdapUser directoryUser,
        string? ipAddress,
        bool isLogin,
        RefreshToken? replaces,
        CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == directoryUser.Username, ct);
        if (user is null)
        {
            user = User.Create(directoryUser.Username, directoryUser.DisplayName);
            db.Users.Add(user);
        }

        user.SyncFromDirectory(
            directoryUser.DisplayName,
            directoryUser.Email,
            directoryUser.Department,
            directoryUser.Title,
            directoryUser.ManagerUsername,
            directoryUser.IsEnabled);

        user.AssignDepartment(await ResolveDepartmentId(directoryUser.Department, ct));

        var roles = await ResolveRoles(directoryUser, ct);
        user.SyncRoles(roles);

        if (isLogin)
            user.RecordLogin(clock);

        // Faz 3: rol yoksa burada referans çağrısı kuralı devreye girecek (domain.md §2).
        if (roles.Count == 0)
        {
            replaces?.Revoke(clock);
            await db.SaveChangesAsync(ct);
            return AuthErrors.NoAccess;
        }

        const AccessScope scope = AccessScope.Full;
        var accessToken = tokens.CreateAccessToken(user, roles, scope);
        var refreshValue = tokens.CreateRefreshToken();
        var refreshToken = RefreshToken.Issue(user.Id, refreshValue.Hash, refreshValue.ExpiresAt, ipAddress);

        db.RefreshTokens.Add(refreshToken);
        replaces?.Revoke(clock, refreshToken.Id);
        await db.SaveChangesAsync(ct);

        return new AuthSession(
            accessToken.Value,
            accessToken.ExpiresAt,
            refreshValue.Value,
            refreshValue.ExpiresAt,
            user.ToCurrentUserDto(roles, scope));
    }

    private async Task<List<Role>> ResolveRoles(LdapUser directoryUser, CancellationToken ct)
    {
        var mappings = await db.RoleGroupMappings.AsNoTracking().ToListAsync(ct);

        return mappings
            .Where(m => directoryUser.Groups.Contains(m.GroupName))
            .Select(m => m.Role)
            .Distinct()
            .Order()
            .ToList();
    }

    // Dizinde ilk kez görülen birim sözlüğe eklenir; birim listesi böylece AD'den kendiliğinden dolar.
    private async Task<Guid?> ResolveDepartmentId(string? departmentName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
            return null;

        var name = departmentName.Trim();
        var existing = await db.DictionaryItems
            .FirstOrDefaultAsync(d => d.Type == DictionaryType.Department && d.Name == name, ct);
        if (existing is not null)
            return existing.Id;

        var lastOrder = await db.DictionaryItems
            .Where(d => d.Type == DictionaryType.Department)
            .MaxAsync(d => (int?)d.SortOrder, ct) ?? 0;

        var department = DictionaryItem.Create(DictionaryType.Department, name, lastOrder + 1);
        db.DictionaryItems.Add(department);
        return department.Id;
    }
}
