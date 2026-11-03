using Microsoft.Extensions.Logging;
using Recruitment.Application.Abstractions;

namespace Recruitment.Infrastructure.Identity;

/// <summary>
/// Test dönemi için: sabit test hesaplarıyla (<see cref="FakeLdapService"/>, ortak şifre) girişe izin verir,
/// diğer herkes gerçek dizine yönlendirilir. <c>Ldap:EnableTestAccounts=true</c> iken kullanılır.
/// Test hesaplarının şifresi herkesçe bilinir; bu yüzden gerçek kullanıma geçilirken kapatılmalıdır.
/// </summary>
internal sealed class TestAccountLdapService(
    ILdapService directory,
    ILdapService testAccounts,
    ILogger<TestAccountLdapService> logger) : ILdapService
{
    public async Task<LdapUser?> Authenticate(string username, string password, CancellationToken ct)
    {
        if (await testAccounts.Authenticate(username, password, ct) is { } testUser)
        {
            logger.LogWarning("Test hesabıyla giriş yapıldı: {Username} (Ldap:EnableTestAccounts açık)", username);
            return testUser;
        }

        return await directory.Authenticate(username, password, ct);
    }
}
