using Recruitment.Domain.Common;
using Shouldly;

namespace Recruitment.Domain.Tests.Common;

public sealed class BusinessDaysTests
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    private static DateTimeOffset Local(int month, int day, int hour) =>
        new(2026, month, day, hour, 0, 0, TimeSpan.FromHours(3));

    private static bool NoHoliday(DateOnly date) => false;

    [Fact]
    public void Add_SkipsWeekend_AndKeepsTimeOfDay()
    {
        // 9 Ekim 2026 cuma 15:00 + 3 iş günü = 14 Ekim çarşamba 15:00.
        var result = BusinessDays.Add(Local(10, 9, 15), 3, Istanbul, NoHoliday);

        result.ShouldBe(Local(10, 14, 15));
        result.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Add_SkipsHolidays()
    {
        // 28 Ekim çarşamba + 1 iş günü: 29 Ekim tatil olduğu için 30 Ekim cuma.
        var result = BusinessDays.Add(Local(10, 28, 10), 1, Istanbul, date => date is { Month: 10, Day: 29 });

        result.ShouldBe(Local(10, 30, 10));
    }

    [Fact]
    public void Add_FromWeekend_StartsCountingOnMonday()
    {
        BusinessDays.Add(Local(10, 10, 11), 1, Istanbul, NoHoliday).ShouldBe(Local(10, 12, 11));
    }

    [Fact]
    public void Add_UsesLocalDate_NotUtcDate()
    {
        // UTC'ye göre perşembe 22:30, Türkiye saatiyle cuma 01:30: +1 iş günü pazartesi 01:30 olmalı.
        var from = new DateTimeOffset(2026, 10, 8, 22, 30, 0, TimeSpan.Zero);

        BusinessDays.Add(from, 1, Istanbul, NoHoliday).ShouldBe(new DateTimeOffset(2026, 10, 12, 1, 30, 0, TimeSpan.FromHours(3)));
    }

    [Fact]
    public void Add_ZeroDays_ReturnsSameInstant_AndNegativeThrows()
    {
        BusinessDays.Add(Local(10, 10, 11), 0, Istanbul, NoHoliday).ShouldBe(Local(10, 10, 11));
        Should.Throw<ArgumentOutOfRangeException>(() => BusinessDays.Add(Local(10, 9, 11), -1, Istanbul, NoHoliday));
    }
}
