using Recruitment.Domain.Users;

namespace Recruitment.Application.Abstractions;

/// <summary>Access token'daki claim adları; token üreten ve okuyan taraf aynı sabitleri kullanır.</summary>
public static class AuthClaims
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string DisplayName = "display_name";
    public const string Email = "email";
    public const string Role = "role";
    public const string Department = "dept";
    public const string Scope = "scope";

    public const string FullScope = "full";

    public static string ToClaimValue(this AccessScope scope) => scope switch
    {
        AccessScope.Full => FullScope,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
    };
}
