using System.Globalization;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Reports;

/// <summary>Rapor hücrelerinin okunur metne çevrilmesi (PDF ve sade metin için); Excel değerleri türüyle yazar.</summary>
public static class ReportFormatter
{
    public const string Empty = "—";
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

    public static string Format(object? value) => value switch
    {
        null => Empty,
        string { Length: 0 } => Empty,
        string text => text,
        bool flag => flag ? "Evet" : "Hayır",
        DateTimeOffset date => TimeZoneInfo.ConvertTime(date, TurkishTime.Zone).ToString("dd.MM.yyyy HH:mm", Culture),
        DateOnly date => date.ToString("dd.MM.yyyy", Culture),
        decimal number => number.ToString("#,0.##", Culture),
        double number => number.ToString("#,0.##", Culture),
        float number => number.ToString("#,0.##", Culture),
        IFormattable formattable => formattable.ToString(null, Culture),
        _ => value.ToString() ?? Empty,
    };

    public static string FileSafe(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string([.. name.Select(c => invalid.Contains(c) || c == ' ' ? '_' : c)]);
        return cleaned.Length == 0 ? "rapor" : cleaned;
    }
}
