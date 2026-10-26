using System.ComponentModel.DataAnnotations;

namespace Recruitment.Infrastructure.Identity;

public sealed class LdapOptions
{
    public const string SectionName = "Ldap";

    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 389;

    /// <summary>true ise LDAPS; değilse bağlantı StartTLS ile şifrelenir.</summary>
    public bool UseSsl { get; init; }

    /// <summary>Kullanıcı bind'inde UPN son eki (ör. "sirket.local" → "ad.soyad@sirket.local").</summary>
    public string Domain { get; init; } = string.Empty;

    public string BaseDn { get; init; } = string.Empty;

    /// <summary>Kullanıcı ve grup araması yapan servis hesabı.</summary>
    public string BindUser { get; init; } = string.Empty;

    public string BindPassword { get; init; } = string.Empty;

    [Required]
    public string UserSearchFilter { get; init; } = "(sAMAccountName={0})";

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>Sabit test kullanıcılarıyla çalışır. Yalnızca Development ortamında izinlidir.</summary>
    public bool UseFake { get; init; }
}
