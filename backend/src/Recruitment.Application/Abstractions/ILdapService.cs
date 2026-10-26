namespace Recruitment.Application.Abstractions;

/// <param name="Username">Normalize edilmiş sAMAccountName.</param>
/// <param name="Groups">
/// Kullanıcının doğrudan ve iç içe üye olduğu gruplar. Her grup hem adıyla (CN) hem tam DN'iyle
/// yer alır; küme büyük/küçük harf duyarsızdır.
/// </param>
public sealed record LdapUser(
    string Username,
    string DisplayName,
    string? Email,
    string? Department,
    string? Title,
    string? ManagerUsername,
    bool IsEnabled,
    IReadOnlySet<string> Groups);

public interface ILdapService
{
    /// <summary>Kullanıcının kendi kimliğiyle bind dener. Boş şifre her zaman reddedilir.</summary>
    Task<bool> Authenticate(string username, string password, CancellationToken ct);

    /// <summary>Servis hesabıyla kullanıcıyı arar; bulunamazsa <c>null</c>.</summary>
    Task<LdapUser?> FindUser(string username, CancellationToken ct);
}
