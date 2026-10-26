namespace Recruitment.Domain.Common;

/// <summary>İş günü hesabı: hafta sonları ve resmi tatiller sayılmaz.</summary>
public static class BusinessDays
{
    /// <summary>
    /// <paramref name="from"/> anına <paramref name="days"/> iş günü ekler; günün saati korunur.
    /// Örn. cuma 15:00 + 3 iş günü = çarşamba 15:00. Gün sınırları <paramref name="zone"/> saat dilimine göredir.
    /// </summary>
    public static DateTimeOffset Add(DateTimeOffset from, int days, TimeZoneInfo zone, Func<DateOnly, bool> isHoliday)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(days);

        var local = TimeZoneInfo.ConvertTime(from, zone);
        var date = DateOnly.FromDateTime(local.DateTime);

        for (var remaining = days; remaining > 0;)
        {
            date = date.AddDays(1);
            if (IsBusinessDay(date, isHoliday))
                remaining--;
        }

        var result = date.ToDateTime(TimeOnly.FromDateTime(local.DateTime));
        return new DateTimeOffset(result, zone.GetUtcOffset(result)).ToUniversalTime();
    }

    public static bool IsBusinessDay(DateOnly date, Func<DateOnly, bool> isHoliday) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !isHoliday(date);
}
