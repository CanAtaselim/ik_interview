using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Auth;

public sealed record CurrentUserDto(
    Guid Id,
    string Username,
    string DisplayName,
    string? Email,
    string? Department,
    Guid? DepartmentId,
    string? Title,
    IReadOnlyCollection<Role> Roles,
    AccessScope Scope);

/// <summary>
/// Giriş veya yenileme sonucu. <see cref="RefreshToken"/> yanıt gövdesine konmaz;
/// Api katmanı onu HttpOnly cookie olarak verir.
/// </summary>
public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    CurrentUserDto User);

internal static class AuthMappings
{
    public static CurrentUserDto ToCurrentUserDto(this User user, IEnumerable<Role> roles, AccessScope scope) =>
        new(user.Id, user.Username, user.DisplayName, user.Email, user.Department, user.DepartmentId, user.Title, [.. roles], scope);
}
