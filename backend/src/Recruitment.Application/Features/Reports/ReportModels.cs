namespace Recruitment.Application.Features.Reports;

public enum ReportFormat
{
    Pdf,
    Xlsx,
}

/// <summary>"Ad: değer" satırı. <see cref="Value"/> metin, sayı, tarih, evet/hayır olabilir; çıktı biçimi bunu kendi diline çevirir.</summary>
public sealed record ReportField(string Label, object? Value);

/// <summary>Düz tablo. Hücreler metin, sayı, tarih ya da evet/hayır olabilir; boş hücre <c>null</c>'dır.</summary>
public sealed record ReportTable(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows);

/// <summary>Raporun bir bölümü: alan listesi ve/veya tablo. Excel'de her bölüm ayrı sayfa olur.</summary>
public sealed record ReportSection(
    string Title,
    IReadOnlyList<ReportField>? Fields = null,
    ReportTable? Table = null,
    string? Note = null);

/// <summary>Çıktı biçiminden bağımsız rapor içeriği; PDF ve Excel oluşturucuları aynı belgeyi çizer.</summary>
public sealed record ReportDocument(
    string Title,
    string? Subtitle,
    IReadOnlyList<ReportSection> Sections,
    DateTimeOffset GeneratedAt,
    string GeneratedBy);

public sealed record ReportFile(string FileName, string ContentType, byte[] Content);

/// <summary>Rapor belgesini PDF ya da Excel dosyasına çevirir (Infrastructure'da uygulanır).</summary>
public interface IReportRenderer
{
    ReportFile Render(ReportDocument document, ReportFormat format, string fileName);
}
