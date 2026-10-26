using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Recruitment.Application.Abstractions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Recruitment.Infrastructure.Files;

/// <summary>
/// PDF ve DOCX dosyalarından arama için düz metin çıkarır. Eski .doc biçimi desteklenmez (metin
/// çıkarılmaz, dosya yine de saklanır). Okunamayan dosya hata sayılmaz; yalnızca aranamaz.
/// </summary>
internal sealed class TextExtractor(ILogger<TextExtractor> logger) : ITextExtractor
{
    private static readonly XNamespace Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public async Task<string?> Extract(Stream content, string extension, CancellationToken ct)
    {
        try
        {
            // İki kitaplık da baştan sona okunabilir (seekable) akış ister.
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            return extension.ToLowerInvariant() switch
            {
                ".pdf" => ExtractPdf(buffer),
                ".docx" => ExtractDocx(buffer),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Dosyadan metin çıkarılamadı ({Extension}); dosya aranamayacak", extension);
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> ExtractLines(Stream content, string extension, CancellationToken ct)
    {
        try
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            return extension.ToLowerInvariant() switch
            {
                ".pdf" => PdfLines(buffer),
                ".docx" => DocxLines(buffer),
                ".doc" => LegacyDocLines(buffer.ToArray()),
                _ => [],
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Dosya satırlara ayrılamadı ({Extension})", extension);
            return [];
        }
    }

    // Adın bulunduğu ilk sayfalar yeter. Aynı hizadaki sözcükler satır, aralarında geniş boşluk olan
    // sözcük grupları ayrı parça olur: iki sütunlu bir CV'de iki sütun tek satıra karışmaz.
    private static List<string> PdfLines(Stream content)
    {
        const int MaxPages = 2;
        var lines = new List<string>();
        var links = new List<string>();
        using var document = PdfDocument.Open(content);

        foreach (var page in document.GetPages().Take(MaxPages))
        {
            // Görünen yazı "LinkedIn" olsa bile bağlantının adresi sayfa açıklamalarında durur.
            links.AddRange(page.GetHyperlinks().Select(link => link.Uri).OfType<string>());

            var words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
            var rows = new List<List<Word>>();
            foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom))
            {
                var row = rows.LastOrDefault();
                var tolerance = Math.Max(word.BoundingBox.Height, 1) * 0.5;
                if (row is not null && Math.Abs(row[0].BoundingBox.Bottom - word.BoundingBox.Bottom) <= tolerance)
                    row.Add(word);
                else
                    rows.Add([word]);
            }

            foreach (var row in rows)
            {
                var segment = new List<string>();
                Word? previous = null;
                foreach (var word in row.OrderBy(w => w.BoundingBox.Left))
                {
                    var gap = previous is null ? 0 : word.BoundingBox.Left - previous.BoundingBox.Right;
                    if (previous is not null && gap > Math.Max(word.BoundingBox.Height, 6) * 1.5)
                    {
                        lines.Add(string.Join(' ', segment));
                        segment.Clear();
                    }

                    segment.Add(word.Text);
                    previous = word;
                }

                lines.Add(string.Join(' ', segment));
            }
        }

        // Bağlantı adresleri sona eklenir; ad aranan ilk satırlar metinle dolu kalsın.
        lines.AddRange(links);
        return lines;
    }

    // Paragraflar satır olur; "LinkedIn" gibi görünen yazının arkasındaki bağlantı adresleri de eklenir.
    private static List<string> DocxLines(Stream content)
    {
        var lines = (ExtractDocx(content) ?? string.Empty)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        content.Position = 0;
        using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.GetEntry("word/_rels/document.xml.rels") is { } relations)
        {
            using var xml = relations.Open();
            lines.AddRange(XDocument.Load(xml).Descendants()
                .Where(e => e.Name.LocalName == "Relationship" &&
                            (string?)e.Attribute("Type") is { } type && type.EndsWith("/hyperlink", StringComparison.Ordinal))
                .Select(e => (string?)e.Attribute("Target"))
                .OfType<string>());
        }

        return lines;
    }

    // Eski .doc ikili bir biçimdir; metin, dosya içindeki okunabilir parçalardan (8 bit veya UTF-16)
    // devşirilir. Yalnızca e-posta, telefon ve bağlantı aramak için yeterlidir.
    private static List<string> LegacyDocLines(byte[] bytes)
    {
        var lines = new List<string>();
        foreach (var text in new[]
                 {
                     Encoding.Latin1.GetString(bytes),
                     Encoding.Unicode.GetString(bytes),
                     Encoding.Unicode.GetString(bytes, 1, bytes.Length - 1),
                 })
        {
            lines.AddRange(Regex.Split(text, @"[^ -~ -ɏ]+")
                .Where(run => run.Trim().Length >= 5)
                .Select(run => run.Trim()));
        }

        return lines;
    }

    private static string ExtractPdf(Stream content)
    {
        using var document = PdfDocument.Open(content);
        var text = new StringBuilder();

        foreach (var page in document.GetPages())
            text.AppendJoin(' ', page.GetWords().Select(word => word.Text)).Append('\n');

        return text.ToString();
    }

    // DOCX bir zip arşividir; metin word/document.xml içindeki <w:t> öğelerindedir.
    private static string? ExtractDocx(Stream content)
    {
        using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.GetEntry("word/document.xml");
        if (entry is null)
            return null;

        using var xml = entry.Open();
        var paragraphs = XDocument.Load(xml)
            .Descendants(Word + "p")
            .Select(paragraph => string.Concat(paragraph.Descendants(Word + "t").Select(run => run.Value)));

        return string.Join('\n', paragraphs);
    }
}
