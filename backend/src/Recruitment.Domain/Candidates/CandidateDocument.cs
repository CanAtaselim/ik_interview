using Recruitment.Domain.Common;

namespace Recruitment.Domain.Candidates;

/// <summary>Adaya ait yüklenmiş dosya (CV, sertifika, case çalışması…). Dosyanın kendisi depolamada, burada yalnızca bilgisi durur.</summary>
public sealed class CandidateDocument : Entity
{
    public const int ExtractedTextMaxLength = 100_000;

    private CandidateDocument() { }

    internal CandidateDocument(
        string fileName, string contentType, long sizeBytes, string storageKey, Guid? typeId, Guid uploadedByUserId, DateTimeOffset uploadedAt)
    {
        TypeId = typeId;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        UploadedByUserId = uploadedByUserId;
        UploadedAt = uploadedAt;
    }

    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = default!;
    /// <summary>
    /// Dosya türü (sözlükte DocumentType: CV, sertifika, case…). <c>null</c>, türün belirtilmediği
    /// eski dosyalardır ve CV sayılır.
    /// </summary>
    public Guid? TypeId { get; private set; }

    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }

    /// <summary>Dosyadan çıkarılan metin; yalnızca arama içindir.</summary>
    public string? ExtractedText { get; private set; }

    internal void SetExtractedText(string? text)
    {
        var trimmed = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        ExtractedText = trimmed is { Length: > ExtractedTextMaxLength } ? trimmed[..ExtractedTextMaxLength] : trimmed;
    }
}
