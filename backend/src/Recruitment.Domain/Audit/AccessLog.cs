namespace Recruitment.Domain.Audit;

public enum AccessAction
{
    CvDownloaded,
    BlacklistListViewed,
    BlacklistEntryViewed,

    /// <summary>İK, kara liste eşleşmesi uyarısını görüp işleme (aday kaydı, CV önerme) yine de devam etti.</summary>
    BlacklistWarningAcknowledged,

    /// <summary>Aday dosyası ya da liste raporu PDF/Excel olarak dışa aktarıldı.</summary>
    ReportExported,
}

/// <summary>Hassas veriye (CV, kara liste) kimin ne zaman eriştiğinin değiştirilemez kaydı.</summary>
public sealed class AccessLog
{
    public const int DetailMaxLength = 200;

    private AccessLog() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UserId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public AccessAction Action { get; private set; }
    public Guid? CandidateId { get; private set; }
    public Guid? DocumentId { get; private set; }

    /// <summary>Dışa aktarılan raporun adı (ör. "Aday dosyası (PDF)"); diğer erişimlerde boştur.</summary>
    public string? Detail { get; private set; }

    public static AccessLog CvDownloaded(Guid userId, Guid candidateId, Guid documentId, DateTimeOffset occurredAt) =>
        new()
        {
            UserId = userId,
            OccurredAt = occurredAt,
            Action = AccessAction.CvDownloaded,
            CandidateId = candidateId,
            DocumentId = documentId,
        };

    /// <summary>Kara liste erişimi; liste görüntülemede <paramref name="candidateId"/> boştur.</summary>
    public static AccessLog Blacklist(AccessAction action, Guid userId, Guid? candidateId, DateTimeOffset occurredAt) =>
        new() { UserId = userId, OccurredAt = occurredAt, Action = action, CandidateId = candidateId };

    /// <summary>Rapor dışa aktarma; aday dosyasında <paramref name="candidateId"/> dolu, liste raporlarında boştur.</summary>
    public static AccessLog ReportExported(Guid userId, Guid? candidateId, string detail, DateTimeOffset occurredAt) =>
        new()
        {
            UserId = userId,
            OccurredAt = occurredAt,
            Action = AccessAction.ReportExported,
            CandidateId = candidateId,
            Detail = detail.Length <= DetailMaxLength ? detail : detail[..DetailMaxLength],
        };
}
