using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Recruitment.Application.Features.Reports;

namespace Recruitment.Infrastructure.Reports;

/// <summary>
/// Rapor belgesini A4 PDF'e çevirir. Az sütunlu tablolar ızgara, çok sütunlu olanlar (satır başına
/// ayrı kutu, "alan: değer" biçiminde) çizilir; böylece 10+ sütunlu kayıtlar da kâğıtta okunur kalır.
/// </summary>
internal static class PdfReportWriter
{
    private const int GridMaxColumns = 6;
    private const int BreakableRun = 28;
    private const char ZeroWidthSpace = '​';

    private static readonly Color Primary = Color.FromRgb(0x5B, 0x4F, 0xE0);
    private static readonly Color PrimarySoft = Color.FromRgb(0xEC, 0xE9, 0xFF);
    private static readonly Color Muted = Color.FromRgb(0x66, 0x62, 0x8A);
    private static readonly Color Line = Color.FromRgb(0xD4, 0xD1, 0xE8);
    private static readonly Unit ContentWidth = Unit.FromCentimeter(18);

    public static byte[] Write(ReportDocument report)
    {
        EmbeddedFontResolver.Install();

        var document = new Document();
        document.Info.Title = report.Title;
        document.Info.Author = report.GeneratedBy;
        ConfigureStyles(document);

        var page = document.AddSection();
        page.PageSetup.PageFormat = PageFormat.A4;
        page.PageSetup.Orientation = Orientation.Portrait;
        page.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);
        page.PageSetup.RightMargin = Unit.FromCentimeter(1.5);
        page.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
        page.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);
        page.PageSetup.FooterDistance = Unit.FromCentimeter(0.8);

        WriteFooter(page);
        WriteTitle(page, report);
        foreach (var section in report.Sections)
            WriteSection(page, section);

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    private static void ConfigureStyles(Document document)
    {
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = EmbeddedFontResolver.FamilyName;
        normal.Font.Size = 8.5;
        normal.ParagraphFormat.SpaceAfter = 0;

        var heading = document.Styles[StyleNames.Heading1]!;
        heading.Font.Name = EmbeddedFontResolver.FamilyName;
        heading.Font.Size = 11;
        heading.Font.Bold = true;
        heading.Font.Color = Colors.White;
        heading.ParagraphFormat.Shading.Color = Primary;
        heading.ParagraphFormat.SpaceBefore = Unit.FromPoint(14);
        heading.ParagraphFormat.SpaceAfter = Unit.FromPoint(5);
        heading.ParagraphFormat.LeftIndent = Unit.FromPoint(0);
        heading.ParagraphFormat.KeepWithNext = true;
    }

    private static void WriteFooter(Section page)
    {
        var footer = page.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 7.5;
        footer.Format.Font.Color = Muted;
        footer.Format.TabStops.AddTabStop(ContentWidth, TabAlignment.Right);
        footer.AddText("Gizli — yalnızca İK içi kullanım\tSayfa ");
        footer.AddPageField();
        footer.AddText(" / ");
        footer.AddNumPagesField();
    }

    private static void WriteTitle(Section page, ReportDocument report)
    {
        var title = page.AddParagraph(Breakable(report.Title));
        title.Format.Font.Size = 18;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Primary;
        title.Format.SpaceAfter = Unit.FromPoint(3);

        if (!string.IsNullOrWhiteSpace(report.Subtitle))
        {
            var subtitle = page.AddParagraph(Breakable(report.Subtitle));
            subtitle.Format.Font.Size = 10;
            subtitle.Format.SpaceAfter = Unit.FromPoint(2);
        }

        var meta = page.AddParagraph(
            $"Oluşturan: {report.GeneratedBy} · {ReportFormatter.Format(report.GeneratedAt)}");
        meta.Format.Font.Size = 8;
        meta.Format.Font.Color = Muted;
    }

    private static void WriteSection(Section page, ReportSection section)
    {
        page.AddParagraph(Breakable(section.Title), StyleNames.Heading1);

        // Doldurulmamış alanlar PDF'te yer kaplamaz (Excel'de listelenir); hiç dolu alan yoksa bölüm boş sayılır.
        var filled = section.Fields?.Where(f => f.Value is not (null or string { Length: 0 })).ToList() ?? [];
        if (filled.Count > 0)
            WriteFields(page, filled);

        if (section.Table is { Columns.Count: > 0, Rows.Count: > 0 } table)
        {
            if (table.Columns.Count <= GridMaxColumns)
                WriteGrid(page, table);
            else
                WriteRecords(page, table);
        }

        var isEmpty = filled.Count == 0 && section.Table is not { Rows.Count: > 0 };
        var text = string.IsNullOrWhiteSpace(section.Note) ? (isEmpty ? "Kayıt yok." : null) : section.Note;
        if (text is not null)
        {
            var note = page.AddParagraph(Breakable(text));
            note.Format.Font.Italic = true;
            note.Format.Font.Color = Muted;
            note.Format.SpaceBefore = Unit.FromPoint(3);
        }
    }

    private static void WriteFields(Section page, IReadOnlyList<ReportField> fields)
    {
        var table = NewTable(page, Unit.FromCentimeter(5), Unit.FromCentimeter(13));
        foreach (var field in fields)
        {
            var row = table.AddRow();
            row.Cells[0].Shading.Color = PrimarySoft;
            row.Cells[0].Format.Font.Bold = true;
            row.Cells[0].AddParagraph(Breakable(field.Label));
            row.Cells[1].AddParagraph(Breakable(ReportFormatter.Format(field.Value)));
        }
    }

    private static void WriteGrid(Section page, ReportTable data)
    {
        var weights = data.Columns
            .Select((header, index) => Math.Clamp(
                Math.Max(
                    (int)Math.Ceiling(header.Length * 1.2), // başlıklar kalın yazıldığı için geniş sayılır
                    data.Rows.Max(row => index < row.Count ? ReportFormatter.Format(row[index]).Length : 0)),
                9,
                40))
            .ToArray();
        var total = weights.Sum();
        var table = NewTable(page, [.. weights.Select(w => Unit.FromCentimeter(18.0 * w / total))]);

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = PrimarySoft;
        header.Format.Font.Bold = true;
        for (var c = 0; c < data.Columns.Count; c++)
            header.Cells[c].AddParagraph(Breakable(data.Columns[c]));

        foreach (var values in data.Rows)
        {
            var row = table.AddRow();
            for (var c = 0; c < data.Columns.Count; c++)
                row.Cells[c].AddParagraph(Breakable(ReportFormatter.Format(c < values.Count ? values[c] : null)));
        }
    }

    private static void WriteRecords(Section page, ReportTable data)
    {
        var number = 0;
        foreach (var values in data.Rows)
        {
            number++;
            var table = NewTable(page, Unit.FromCentimeter(5), Unit.FromCentimeter(13));
            table.Format.KeepTogether = false;

            var heading = table.AddRow();
            heading.Shading.Color = PrimarySoft;
            heading.Format.Font.Bold = true;
            heading.KeepWith = 1;
            heading.Cells[0].MergeRight = 1;
            heading.Cells[0].AddParagraph(Breakable($"{number}. {RecordTitle(values)}"));

            for (var c = 0; c < data.Columns.Count; c++)
            {
                var value = c < values.Count ? values[c] : null;
                if (value is null || value is string { Length: 0 })
                    continue;

                var row = table.AddRow();
                row.Cells[0].Format.Font.Color = Muted;
                row.Cells[0].AddParagraph(Breakable(data.Columns[c]));
                row.Cells[1].AddParagraph(Breakable(ReportFormatter.Format(value)));
            }

            page.AddParagraph().Format.SpaceAfter = Unit.FromPoint(4);
        }
    }

    private static string RecordTitle(IReadOnlyList<object?> values)
    {
        var parts = values.Take(3)
            .Select(ReportFormatter.Format)
            .Where(text => text != ReportFormatter.Empty)
            .Take(2);
        var title = string.Join(" · ", parts);
        return title.Length == 0 ? "Kayıt" : title;
    }

    private static Table NewTable(Section page, params Unit[] widths)
    {
        var table = page.AddTable();
        table.Borders.Color = Line;
        table.Borders.Width = 0.5;
        table.Rows.LeftIndent = 0;
        table.TopPadding = 2.5;
        table.BottomPadding = 2.5;
        table.LeftPadding = 4;
        table.RightPadding = 4;
        foreach (var width in widths)
            table.AddColumn(width);

        return table;
    }

    /// <summary>Gömülü yazı tipinde bulunmayan işaretleri (oklar vb.) karşılığıyla değiştirir; aksi halde kutucuk çıkar.</summary>
    private static string Printable(string text) =>
        text.Replace("→", "›").Replace("←", "‹").Replace("✓", "+").Replace("✗", "x");

    /// <summary>
    /// Boşluksuz uzun diziler (adres, dosya adı) hücreden taşmasın diye araya görünmez ayırıcı konur;
    /// metin kopyalandığında değişmez.
    /// </summary>
    private static string Breakable(string text)
    {
        text = Printable(text);
        if (text.Length < BreakableRun)
            return text;

        var result = new System.Text.StringBuilder(text.Length + text.Length / BreakableRun);
        var run = 0;
        foreach (var character in text)
        {
            run = char.IsWhiteSpace(character) ? 0 : run + 1;
            result.Append(character);
            if (run >= BreakableRun)
            {
                result.Append(ZeroWidthSpace);
                run = 0;
            }
        }

        return result.ToString();
    }
}
