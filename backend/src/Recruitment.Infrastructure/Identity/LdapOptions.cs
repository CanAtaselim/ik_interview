using System.ComponentModel.DataAnnotations;

namespace Recruitment.Infrastructure.Identity;

public sealed class LdapOptions
{
    public const string SectionName = "Ldap";

    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 389;

    /// <summary>
    /// <see cref="LdapAuthMode.Basic"/> modunda true ise LDAPS; değilse bağlantı StartTLS ile şifrelenir.
    /// <see cref="LdapAuthMode.Negotiate"/> modunda kullanılmaz.
    /// </summary>
    public bool UseSsl { get; init; }

    /// <summary>Kimlik doğrulama yöntemi (aşağıdaki <see cref="LdapAuthMode"/>).</summary>
    public LdapAuthMode AuthMode { get; init; } = LdapAuthMode.Basic;

    /// <summary>Kullanıcı bind'inde UPN son eki (ör. "sirket.local" → "ad.soyad@sirket.local").</summary>
    public string Domain { get; init; } = string.Empty;

    public string BaseDn { get; init; } = string.Empty;

    [Required]
    public string UserSearchFilter { get; init; } = "(sAMAccountName={0})";

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>Sabit test kullanıcılarıyla çalışır. Yalnızca Development ortamında izinlidir.</summary>
    public bool UseFake { get; init; }
}

public enum LdapAuthMode
{
    /// <summary>Basic bind; şifre TLS (StartTLS veya LDAPS) ile korunur, sunucu sertifikasına güvenilmesi gerekir.</summary>
    Basic,

    /// <summary>
    /// Windows'un Negotiate (Kerberos/NTLM) kimlik doğrulaması; şifre ağa düz gitmez, bağlantı imzalanır ve
    /// şifrelenir (sealing), sertifika gerekmez. Yalnızca Windows sunucuda çalışır (IIS); Linux/Docker'da Basic kullanın.
    /// </summary>
    Negotiate,
}
