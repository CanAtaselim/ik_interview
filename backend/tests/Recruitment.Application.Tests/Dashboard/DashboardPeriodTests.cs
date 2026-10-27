using Recruitment.Application.Features.Dashboard;
using Shouldly;

namespace Recruitment.Application.Tests.Dashboard;

public sealed class DashboardPeriodTests
{
    // 27 Ekim 2026 12:00 UTC (Türkiye saati 15:00).
    private static readonly FakeClock Clock = new(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));

    private static DashboardPeriod Resolve(string? from, string? to)
    {
        var filter = new DashboardFilter(
            from is null ? null : DateOnly.Parse(from), to is null ? null : DateOnly.Parse(to), null, null);
        return DashboardPeriod.Resolve(filter, Clock).Value;
    }

    [Fact]
    public void Resolve_WithoutDates_UsesLast90DaysEndingToday()
    {
        var period = Resolve(null, null);

        period.Days.ShouldBe(90);
        period.Contains(Clock.GetUtcNow()).ShouldBeTrue();
        period.End.ShouldBe(new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.FromHours(3)));
    }

    [Fact]
    public void Resolve_PreviousPeriod_HasTheSameLengthAndIsAdjacent()
    {
        var period = Resolve("2026-10-01", "2026-10-10");

        period.Days.ShouldBe(10);
        period.PreviousStart.ShouldBe(new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.FromHours(3)));
        period.PreviousContains(period.Start.AddTicks(-1)).ShouldBeTrue();
        period.PreviousContains(period.Start).ShouldBeFalse();
        period.Contains(period.End).ShouldBeFalse();
    }

    [Fact]
    public void Resolve_InvalidRanges_ReturnValidationError()
    {
        DashboardPeriod.Resolve(new DashboardFilter(new DateOnly(2026, 12, 1), new DateOnly(2026, 1, 1), null, null), Clock)
            .IsFailure.ShouldBeTrue();
        DashboardPeriod.Resolve(new DashboardFilter(new DateOnly(2025, 1, 1), new DateOnly(2026, 10, 1), null, null), Clock)
            .IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("2026-10-01", "2026-10-20", "day")]
    [InlineData("2026-07-01", "2026-10-27", "week")]
    [InlineData("2026-01-01", "2026-10-27", "month")]
    public void Granularity_FollowsPeriodLength(string from, string to, string expected) =>
        Resolve(from, to).Granularity.ShouldBe(expected);

    [Fact]
    public void Buckets_Weekly_StartOnMondayAndCoverTheWholePeriod()
    {
        var period = Resolve("2026-07-01", "2026-10-27");

        var buckets = period.Buckets().ToList();

        buckets.ShouldAllBe(day => day.DayOfWeek == DayOfWeek.Monday);
        buckets.First().ShouldBe(new DateOnly(2026, 6, 29));
        buckets.Last().ShouldBe(new DateOnly(2026, 10, 26));
        period.Bucket(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero)).ShouldBe(new DateOnly(2026, 10, 26));
    }

    [Fact]
    public void Bucket_UsesTurkishLocalDay()
    {
        var period = Resolve("2026-10-01", "2026-10-10");

        // 20:30 UTC = ertesi gün 23:30 Türkiye saati (UTC+3).
        period.Bucket(new DateTimeOffset(2026, 10, 4, 21, 30, 0, TimeSpan.Zero)).ShouldBe(new DateOnly(2026, 10, 5));
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
