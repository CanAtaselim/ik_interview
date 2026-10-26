namespace Recruitment.Infrastructure.Calendar;

/// <summary>
/// Microsoft Graph (Exchange Online) ayarları (<c>Graph</c> bölümü). Alanlar boşsa Outlook bağlantısı
/// kapalıdır; uygulama yalnızca sistem içi müsaitlikle çalışır. İstemci sırrı repoya yazılmaz.
/// </summary>
public sealed class GraphOptions
{
    public const string SectionName = "Graph";

    public string TenantId { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// Takvim sorgularının üzerinden yapıldığı posta kutusu (ör. ik-ise-alim@sirket.com). Uygulamanın bu kutuya
    /// ve mülakatçıların takvimlerine <c>Calendars.Read</c> (uygulama izni) ile erişimi olmalıdır.
    /// </summary>
    public string OrganizerMailbox { get; init; } = string.Empty;

    /// <summary>Yalnızca Development: Outlook yerine sabit sahte meşgul saatler kullanılır.</summary>
    public bool UseFake { get; init; }

    /// <summary>Aynı kişi ve aralık için sonuç bu kadar saniye önbellekte tutulur.</summary>
    public int CacheSeconds { get; init; } = 120;

    public int TimeoutSeconds { get; init; } = 8;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TenantId) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(OrganizerMailbox);
}
