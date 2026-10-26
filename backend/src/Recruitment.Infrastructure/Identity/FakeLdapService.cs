using Recruitment.Application.Abstractions;
using Recruitment.Domain.Users;
using Recruitment.Infrastructure.Persistence.Seed;

namespace Recruitment.Infrastructure.Identity;

/// <summary>
/// AD olmadan geliştirme yapabilmek için sabit test kullanıcıları. Yalnızca Development
/// ortamında ve <c>Ldap:UseFake=true</c> iken kullanılır; hepsinin şifresi aynıdır.
/// </summary>
internal sealed class FakeLdapService : ILdapService
{
    public const string Password = "Test123!";

    private static readonly Dictionary<string, LdapUser> Users = new LdapUser[]
    {
        Create("admin", "Sistem Admin", "Bilgi Teknolojileri", "Sistem Yöneticisi", null, Role.SystemAdmin),
        Create("ikyonetici", "Ayşe Yılmaz", "İnsan Kaynakları", "İK Yöneticisi", null, Role.HrManager),
        Create("ikuzman", "Mehmet Demir", "İnsan Kaynakları", "İK Uzmanı", "ikyonetici", Role.HrSpecialist),
        Create("mudur", "Ali Çelik", "Yazılım Geliştirme", "Yazılım Müdürü", null, Role.DepartmentManager),
        Create("takimlideri", "Zeynep Kaya", "Yazılım Geliştirme", "Takım Lideri", "mudur", Role.TeamLead),
        Create("yigit", "Yiğit Enes Tunçay", "Yazılım Geliştirme", "Takım Lideri", "mudur", Role.TeamLead),
        Create("yonetim", "Fatma Şahin", "Genel Müdürlük", "Genel Müdür Yardımcısı", null, Role.Executive),
        Create("calisan", "Can Öztürk", "Yazılım Geliştirme", "Yazılım Geliştirici", "takimlideri"),
    }.ToDictionary(u => u.Username);

    public Task<bool> Authenticate(string username, string password, CancellationToken ct) =>
        Task.FromResult(Users.ContainsKey(username) && password == Password);

    public Task<LdapUser?> FindUser(string username, CancellationToken ct) =>
        Task.FromResult(Users.GetValueOrDefault(username));

    private static LdapUser Create(
        string username, string displayName, string department, string title, string? manager, params Role[] roles) =>
        new(
            username,
            displayName,
            $"{username}@ornek.local",
            department,
            title,
            manager,
            IsEnabled: true,
            roles.Select(DatabaseSeeder.DefaultGroupName).ToHashSet(StringComparer.OrdinalIgnoreCase));
}
