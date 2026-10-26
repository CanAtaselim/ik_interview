namespace Recruitment.Application.Abstractions;

/// <summary>İşe alım sürecinin süre ayarları (<c>Recruitment</c> bölümü).</summary>
public sealed class RecruitmentOptions
{
    public const string SectionName = "Recruitment";

    /// <summary>Takım liderinin önerilen CV için karar vermesi beklenen süre (iş günü).</summary>
    public int CvReviewSlaBusinessDays { get; init; } = 3;

    /// <summary>Süre dolduktan kaç iş günü sonra gecikme İK'ya bildirilir.</summary>
    public int CvReviewEscalationBusinessDays { get; init; } = 2;

    /// <summary>E-postalardaki bağlantıların açacağı uygulama adresi.</summary>
    public string FrontendBaseUrl { get; init; } = "http://localhost:8080";
}

/// <summary>İş günü hesabı: hafta sonları ve resmi tatiller sayılmaz.</summary>
public interface IBusinessCalendar
{
    Task<DateTimeOffset> AddBusinessDays(DateTimeOffset from, int days, CancellationToken ct);
}
