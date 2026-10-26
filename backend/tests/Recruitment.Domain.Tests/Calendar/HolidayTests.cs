using Recruitment.Domain.Calendar;
using Recruitment.Domain.Common;
using Shouldly;

namespace Recruitment.Domain.Tests.Calendar;

public sealed class HolidayTests
{
    [Fact]
    public void RecurringHoliday_OccursEveryYear()
    {
        var holiday = Holiday.Create(" Cumhuriyet Bayramı ", 10, 29);

        holiday.Name.ShouldBe("Cumhuriyet Bayramı");
        holiday.OccursOn(new DateOnly(2026, 10, 29)).ShouldBeTrue();
        holiday.OccursOn(new DateOnly(2031, 10, 29)).ShouldBeTrue();
        holiday.OccursOn(new DateOnly(2026, 10, 28)).ShouldBeFalse();
    }

    [Fact]
    public void DatedHoliday_OccursOnlyInItsYear()
    {
        var holiday = Holiday.Create("Ramazan Bayramı 1. gün", 3, 20, 2026);

        holiday.OccursOn(new DateOnly(2026, 3, 20)).ShouldBeTrue();
        holiday.OccursOn(new DateOnly(2027, 3, 20)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("", 1, 1, null)]
    [InlineData("Tatil", 13, 1, null)]
    [InlineData("Tatil", 2, 30, null)]
    [InlineData("Tatil", 2, 29, 2027)]
    public void Create_WithInvalidValues_Throws(string name, int month, int day, int? year)
    {
        Should.Throw<DomainException>(() => Holiday.Create(name, month, day, year));
    }
}
