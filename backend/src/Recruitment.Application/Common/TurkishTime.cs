using System.Globalization;

namespace Recruitment.Application.Common;

/// <summary>Kullanıcıya gösterilen zamanlar (e-posta, bildirim) Türkiye saatiyle ve Türkçe yazılır.</summary>
public static class TurkishTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>"21 Ekim 2026 Çarşamba 14:00".</summary>
    public static string DateTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, Zone).ToString("d MMMM yyyy dddd HH:mm", Culture);

    /// <summary>"21 Ekim 2026 Çarşamba 14:00–15:00".</summary>
    public static string Range(DateTimeOffset startsAt, DateTimeOffset endsAt) =>
        $"{DateTime(startsAt)}–{TimeZoneInfo.ConvertTime(endsAt, Zone).ToString("HH:mm", Culture)}";

    /// <summary>"21.10.2026".</summary>
    public static string Date(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, Zone).ToString("dd.MM.yyyy", Culture);
}
