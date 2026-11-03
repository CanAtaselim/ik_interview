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
    /// <summary>
    /// Kullanıcının kendi kimliğiyle bind dener; başarılıysa profilini ve gruplarını aynı bağlantıdan, yine
    /// kendi yetkisiyle okuyup döner (servis hesabı gerekmez). Kimlik bilgileri hatalıysa <c>null</c>.
    /// Boş şifre her zaman reddedilir. Bilgi yalnızca girişte okunur; oturum yenilemede dizine gidilmez.
    /// </summary>
    Task<LdapUser?> Authenticate(string username, string password, CancellationToken ct);
}
