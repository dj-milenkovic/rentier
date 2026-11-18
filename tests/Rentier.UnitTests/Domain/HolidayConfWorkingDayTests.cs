using FluentAssertions;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests.Domain;

public class HolidayConfWorkingDayTests
{
    private static readonly DateOnly Holiday = new(2024, 5, 1); // Wednesday

    private static HolidayConf Calendar() => new([Holiday]);

    [Theory]
    [InlineData(2024, 5, 4)] // Saturday
    [InlineData(2024, 5, 5)] // Sunday
    [InlineData(2024, 5, 1)] // configured holiday
    public void IsWorkingDay_WeekendOrHoliday_ReturnsFalse(int year, int month, int day)
    {
        Calendar().IsWorkingDay(new DateOnly(year, month, day)).Should().BeFalse();
    }

    [Fact]
    public void IsWorkingDay_PlainWeekday_ReturnsTrue()
    {
        Calendar().IsWorkingDay(new DateOnly(2024, 5, 2)).Should().BeTrue();
    }

    [Fact]
    public void CalculateDeadline_SaturdayThenMondayHoliday_LandsOnTuesday()
    {
        // income 2024-05-02 → raw deadline 2024-06-01 (Saturday) → Monday 2024-06-03 is a
        // holiday → Tuesday 2024-06-04.
        var calendar = new HolidayConf([new DateOnly(2024, 6, 3)]);

        var deadline = FilingDeadlineCalculator.CalculateDeadline(new DateOnly(2024, 5, 2), calendar);

        deadline.Should().Be(new DateOnly(2024, 6, 4));
    }

    [Fact]
    public void IsBusinessDay_AgreesWithIsWorkingDay()
    {
        var calendar = Calendar();
        var days = Enumerable.Range(0, 14).Select(i => new DateOnly(2024, 4, 28).AddDays(i));

        days.Should().OnlyContain(d => BusinessDayResolver.IsBusinessDay(d, calendar) == calendar.IsWorkingDay(d));
    }
}
