using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Recruitment.Application.Abstractions;

namespace Recruitment.Infrastructure.Files;

/// <summary>
/// İndirme token'ını ASP.NET Data Protection ile şifreler ve süre sınırı koyar; içeriği değiştirilemez
/// ve okunamaz. Anahtarlar sunucuya özeldir: birden çok sunucuda çalışılacaksa anahtar halkası paylaşılmalıdır.
/// </summary>
internal sealed class DownloadTokenService(
    IDataProtectionProvider dataProtection, IOptions<FileStorageOptions> options, TimeProvider clock)
    : IDownloadTokenService
{
    private readonly ITimeLimitedDataProtector _protector =
        dataProtection.CreateProtector("Recruitment.DocumentDownload").ToTimeLimitedDataProtector();

    public (string Token, DateTimeOffset ExpiresAt) Create(Guid documentId)
    {
        var expiresAt = clock.GetUtcNow().AddMinutes(options.Value.SignedUrlMinutes);
        return (_protector.Protect(documentId.ToString("N"), expiresAt), expiresAt);
    }

    public Guid? Validate(string token)
    {
        try
        {
            return Guid.TryParseExact(_protector.Unprotect(token), "N", out var documentId) ? documentId : null;
        }
        catch (CryptographicException)
        {
            // Bozuk, başkasına ait veya süresi dolmuş token.
            return null;
        }
    }
}
