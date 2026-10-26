using Recruitment.Domain.Candidates;

namespace Recruitment.Application.Abstractions;

/// <summary>Dosya yükleme kuralları ve depolama ayarları (<c>FileStorage</c> bölümü).</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string RootPath { get; init; } = "storage";
    public int MaxFileSizeMb { get; init; } = 10;
    public string[] AllowedExtensions { get; init; } = [".pdf", ".doc", ".docx", ".png", ".jpg", ".jpeg"];
    public int SignedUrlMinutes { get; init; } = 5;

    public long MaxFileSizeBytes => MaxFileSizeMb * 1024L * 1024L;
}

/// <summary>Dosyaların saklandığı yer. Bugün yerel disk; ileride MinIO/S3 olabilir.</summary>
public interface IFileStorage
{
    /// <returns>Dosyayı sonradan açmak için kullanılacak depolama anahtarı.</returns>
    Task<string> Save(Stream content, string extension, CancellationToken ct);

    Task<Stream> Open(string storageKey, CancellationToken ct);

    Task Delete(string storageKey, CancellationToken ct);
}

/// <summary>
/// Kısa süreli indirme bağlantısı. Token'ı bilen, süresi dolana kadar dosyayı oturum açmadan
/// indirebilir; bu yüzden yalnızca yetki kontrolünden geçmiş kullanıcıya üretilir.
/// </summary>
public interface IDownloadTokenService
{
    (string Token, DateTimeOffset ExpiresAt) Create(Guid documentId);

    /// <returns>Token geçerliyse belge kimliği; bozuk veya süresi dolmuşsa <c>null</c>.</returns>
    Guid? Validate(string token);
}

public interface ITextExtractor
{
    /// <summary>Dosyadan arama için düz metin çıkarır; desteklenmeyen veya okunamayan dosyada <c>null</c>.</summary>
    Task<string?> Extract(Stream content, string extension, CancellationToken ct);

    /// <summary>
    /// Dosyayı satır satır okur (CV'den bilgi çıkarmak için). PDF'te aynı hizadaki ve aralarında
    /// geniş boşluk olan metin parçaları ayrı satır sayılır (iki sütunlu CV'ler karışmasın). Eski
    /// .doc biçiminden yalnızca okunabilen metin parçaları gelir. Okunamayan dosyada boş liste.
    /// </summary>
    Task<IReadOnlyList<string>> ExtractLines(Stream content, string extension, CancellationToken ct);
}

public interface ICandidateTextSearch
{
    /// <summary>
    /// Adayları ad, iletişim, etiket, ön görüşme metni ve CV içeriği üzerinden filtreler.
    /// Her sözcük, sözcük başı eşleşmesiyle aranır; <paramref name="term"/> boşsa filtre uygulanmaz.
    /// </summary>
    IQueryable<Candidate> Apply(IQueryable<Candidate> candidates, string? term);
}
