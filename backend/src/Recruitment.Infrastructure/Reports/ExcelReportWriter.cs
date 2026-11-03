using ClosedXML.Excel;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Reports;

namespace Recruitment.Infrastructure.Reports;

/// <summary>
/// Rapor belgesini Excel'e çevirir: her bölüm ayrı sayfa, tablolar filtrelenebilir ve başlık satırı sabit,
/// tarih ve sayılar metin değil gerçek değer olarak yazılır (Excel'de sıralanıp süzülebilsin).
/// </summary>
internal static class ExcelReportWriter
{
    private const string DateTimeFormat = "dd.MM.yyyy HH:mm";
    private const string DateFormat = "dd.MM.yyyy";
    private const int MaxColumnWidth = 60;
    private const int MinColumnWidth = 10;
    private const int MaxCellLength = 32_000;
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#5B4FE0");
    private static readonly XLColor LabelFill = XLColor.FromHtml("#ECE9FF");

    public static byte[] Write(ReportDocument document)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Title = document.Title;
        workbook.Properties.Author = document.GeneratedBy;

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in document.Sections)
        {
            var sheet = workbook.Worksheets.Add(SheetName(section.Title, usedNames));
            WriteSection(sheet, document, section);
        }

        if (workbook.Worksheets.Count == 0)
            workbook.Worksheets.Add("Rapor");

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteSection(IXLWorksheet sheet, ReportDocument document, ReportSection section)
    {
        sheet.Style.Font.FontName = "Calibri";
        sheet.Style.Font.FontSize = 10;

        sheet.Cell(1, 1).Value = document.Title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = $"{section.Title} · {document.Subtitle ?? "—"}";
        sheet.Cell(3, 1).Value =
            $"Oluşturan: {document.GeneratedBy} · {ReportFormatter.Format(document.GeneratedAt)} · Gizli: yalnızca İK içi kullanım";
        sheet.Range(2, 1, 3, 1).Style.Font.FontColor = XLColor.FromHtml("#66628A");

        var row = 5;
        var columns = 1;

        if (section.Fields is { Count: > 0 })
        {
            foreach (var field in section.Fields)
            {
                var label = sheet.Cell(row, 1);
                label.Value = field.Label;
                label.Style.Font.Bold = true;
                label.Style.Fill.BackgroundColor = LabelFill;
                label.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                SetValue(sheet.Cell(row, 2), field.Value);
                sheet.Cell(row, 2).Style.Alignment.WrapText = true;
                sheet.Cell(row, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                sheet.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                row++;
            }

            columns = 2;
            row++;
        }

        if (section.Table is { } table && table.Columns.Count > 0)
        {
            var headerRow = row;
            for (var c = 0; c < table.Columns.Count; c++)
            {
                var header = sheet.Cell(row, c + 1);
                header.Value = table.Columns[c];
                header.Style.Font.Bold = true;
                header.Style.Font.FontColor = XLColor.White;
                header.Style.Fill.BackgroundColor = HeaderFill;
                header.Style.Alignment.WrapText = true;
                header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            row++;
            foreach (var values in table.Rows)
            {
                for (var c = 0; c < table.Columns.Count; c++)
                {
                    var cell = sheet.Cell(row, c + 1);
                    SetValue(cell, c < values.Count ? values[c] : null);
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                }

                row++;
            }

            var tableRange = sheet.Range(headerRow, 1, Math.Max(row - 1, headerRow), table.Columns.Count);
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#E8E6F4");
            tableRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E8E6F4");
            if (table.Rows.Count > 0)
                sheet.Range(headerRow, 1, row - 1, table.Columns.Count).SetAutoFilter();

            sheet.SheetView.FreezeRows(headerRow);
            columns = Math.Max(columns, table.Columns.Count);
            row++;
        }

        if (!string.IsNullOrWhiteSpace(section.Note))
        {
            sheet.Cell(row, 1).Value = section.Note;
            sheet.Cell(row, 1).Style.Font.Italic = true;
            sheet.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#66628A");
        }

        // Başlık satırları ilk sütunda taşar; genişlik yalnızca tablo/alan içeriğine göre ayarlanır.
        for (var c = 1; c <= columns; c++)
        {
            var column = sheet.Column(c);
            var longest = column.CellsUsed()
                .Where(cell => cell.Address.RowNumber >= 5)
                .Select(cell => cell.GetFormattedString().Length)
                .DefaultIfEmpty(MinColumnWidth)
                .Max();
            column.Width = Math.Clamp(longest + 2, MinColumnWidth, MaxColumnWidth);
            column.Style.Alignment.WrapText = true;
        }

        sheet.Cell(1, 1).Style.Alignment.WrapText = false;
        sheet.Range(2, 1, 3, 1).Style.Alignment.WrapText = false;
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
    }

    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case string text:
                cell.Value = Clean(text);
                break;
            case bool flag:
                cell.Value = flag ? "Evet" : "Hayır";
                break;
            case DateTimeOffset date:
                cell.Value = TimeZoneInfo.ConvertTime(date, TurkishTime.Zone).DateTime;
                cell.Style.DateFormat.Format = DateTimeFormat;
                break;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = DateFormat;
                break;
            case int number:
                cell.Value = number;
                break;
            case long number:
                cell.Value = number;
                break;
            case decimal number:
                cell.Value = number;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case double number:
                cell.Value = number;
                cell.Style.NumberFormat.Format = "#,##0.0#";
                break;
            default:
                cell.Value = Clean(ReportFormatter.Format(value));
                break;
        }
    }

    /// <summary>Excel'in kabul etmediği kontrol karakterlerini atar ve hücre sınırını aşan metni keser.</summary>
    private static string Clean(string text)
    {
        var cleaned = new string([.. text.Where(c => !char.IsControl(c) || c is '\n' or '\r' or '\t')]);
        return cleaned.Length <= MaxCellLength ? cleaned : cleaned[..MaxCellLength];
    }

    private static string SheetName(string title, HashSet<string> used)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var name = new string([.. title.Select(c => invalid.Contains(c) ? ' ' : c)]).Trim();
        if (name.Length == 0)
            name = "Sayfa";
        if (name.Length > 31)
            name = name[..31].TrimEnd();

        var candidate = name;
        for (var n = 2; !used.Add(candidate); n++)
        {
            var suffix = $" ({n})";
            candidate = name[..Math.Min(name.Length, 31 - suffix.Length)] + suffix;
        }

        return candidate;
    }
}
