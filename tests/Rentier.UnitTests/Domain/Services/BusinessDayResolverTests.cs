using FluentAssertions;
using Rentier.Domain.Exceptions;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

public class BusinessDayResolverTests
{
    private static HolidayConf NoHolidays() => new HolidayConf(new List<DateOnly>());

    [Fact]
    public void IsBusinessDay_Monday_ReturnsTrue()
    {
        var monday = new DateOnly(2024, 1, 8);
        BusinessDayResolver.IsBusinessDay(monday, NoHolidays()).Should().BeTrue();
    }

    [Fact]
    public void IsBusinessDay_Saturday_ReturnsFalse()
    {
        var saturday = new DateOnly(2024, 1, 6);
        BusinessDayResolver.IsBusinessDay(saturday, NoHolidays()).Should().BeFalse();
    }

    [Fact]
    public void IsBusinessDay_Sunday_ReturnsFalse()
    {
        var sunday = new DateOnly(2024, 1, 7);
        BusinessDayResolver.IsBusinessDay(sunday, NoHolidays()).Should().BeFalse();
    }

    [Fact]
    public void IsBusinessDay_WeekdayInHolidays_ReturnsFalse()
    {
        var friday = new DateOnly(2024, 1, 5);
        var holidays = new HolidayConf(new List<DateOnly> { friday });
        BusinessDayResolver.IsBusinessDay(friday, holidays).Should().BeFalse();
    }

    [Fact]
    public void IsBusinessDay_WeekdayNotInHolidays_ReturnsTrue()
    {
        var friday = new DateOnly(2024, 1, 5);
        BusinessDayResolver.IsBusinessDay(friday, NoHolidays()).Should().BeTrue();
    }

    [Fact]
    public void WalkBackward_FromMonday_YieldsFriday()
    {
        var monday = new DateOnly(2024, 1, 8);
        var first = BusinessDayResolver.WalkBackward(monday, NoHolidays()).First();
        first.Should().Be(new DateOnly(2024, 1, 5));
    }

    [Fact]
    public void WalkBackward_FromSunday_YieldsFriday()
    {
        var sunday = new DateOnly(2024, 1, 7);
        var first = BusinessDayResolver.WalkBackward(sunday, NoHolidays()).First();
        first.Should().Be(new DateOnly(2024, 1, 5));
    }

    [Fact]
    public void WalkBackward_FromSaturday_YieldsFriday()
    {
        var saturday = new DateOnly(2024, 1, 6);
        var first = BusinessDayResolver.WalkBackward(saturday, NoHolidays()).First();
        first.Should().Be(new DateOnly(2024, 1, 5));
    }

    [Fact]
    public void WalkBackward_FromWeekdayAfterHoliday_YieldsBusinessDayBeforeHoliday()
    {
        var tuesday = new DateOnly(2024, 1, 9);
        var monday = new DateOnly(2024, 1, 8);
        var holidays = new HolidayConf(new List<DateOnly> { monday });
        var first = BusinessDayResolver.WalkBackward(tuesday, holidays).First();
        first.Should().Be(new DateOnly(2024, 1, 5));
    }

    [Fact]
    public void WalkBackward_ConsecutiveHolidays_SkipsBoth()
    {
        var monday = new DateOnly(2024, 1, 8);
        var friday = new DateOnly(2024, 1, 5);
        var holidays = new HolidayConf(new List<DateOnly> { friday });
        var first = BusinessDayResolver.WalkBackward(monday, holidays).First();
        first.Should().Be(new DateOnly(2024, 1, 4));
    }

    [Fact]
    public void WalkBackward_MaxLookbackExhausted_ReturnsEmpty()
    {
        var from = new DateOnly(2024, 1, 8);
        var holidays = new HolidayConf(new List<DateOnly>
        {
            new(2024, 1, 5), // Friday - holiday
            new(2024, 1, 4), // Thursday - holiday
            new(2024, 1, 3), // Wednesday - holiday
        });
        var results = BusinessDayResolver.WalkBackward(from, holidays, maxLookbackDays: 3).ToList();
        results.Should().BeEmpty();
    }

    [Fact]
    public void WalkBackward_MaxLookbackDays1_LimitsToOneDayBack()
    {
        var wednesday = new DateOnly(2024, 1, 10);
        var results = BusinessDayResolver.WalkBackward(wednesday, NoHolidays(), maxLookbackDays: 1).ToList();
        results.Should().ContainSingle().Which.Should().Be(new DateOnly(2024, 1, 9));
    }

    [Fact]
    public void IsBusinessDay_WhenHolidaysIsNull_ThrowsDomainException()
    {
        var act = () => BusinessDayResolver.IsBusinessDay(new DateOnly(2024, 1, 8), null!);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void WalkBackward_WhenHolidaysIsNull_ThrowsDomainException()
    {
        // Enumerate to trigger the guard (lazy method)
        var act = () => BusinessDayResolver.WalkBackward(new DateOnly(2024, 1, 8), null!).ToList();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void FindPreviousBusinessDay_WhenHolidaysIsNull_ThrowsDomainException()
    {
        var act = () => BusinessDayResolver.FindPreviousBusinessDay(new DateOnly(2024, 1, 8), null!);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void WalkBackward_WhenMaxLookbackLessThanOne_ThrowsDomainException()
    {
        var act = () => BusinessDayResolver.WalkBackward(new DateOnly(2024, 1, 8), NoHolidays(), maxLookbackDays: 0).ToList();

        act.Should().Throw<DomainException>().WithMessage("*maxLookbackDays*");
    }

    [Fact]
    public void FindPreviousBusinessDay_OnHolidayWeekday_WalksBackward()
    {
        // Monday Jan 8 is a holiday — should walk back to Friday Jan 5
        var monday = new DateOnly(2024, 1, 8);
        var holidays = new HolidayConf(new List<DateOnly> { monday });

        var result = BusinessDayResolver.FindPreviousBusinessDay(monday, holidays);

        result.Should().Be(new DateOnly(2024, 1, 5));
    }

    [Fact]
    public void FindPreviousBusinessDay_WhenNoBusinessDayFound_ThrowsDomainException()
    {
        // Saturday Jan 13 + all weekdays Jan 4-12 blocked → no business day within 10 days
        var saturday = new DateOnly(2024, 1, 13);
        var holidays = new HolidayConf(new List<DateOnly>
        {
            new(2024, 1, 4),  // Thursday
            new(2024, 1, 5),  // Friday
            new(2024, 1, 8),  // Monday
            new(2024, 1, 9),  // Tuesday
            new(2024, 1, 10), // Wednesday
            new(2024, 1, 11), // Thursday
            new(2024, 1, 12), // Friday
        });

        var act = () => BusinessDayResolver.FindPreviousBusinessDay(saturday, holidays);

        act.Should().Throw<DomainException>().WithMessage("*No business day found*");
    }

    [Fact]
    public void FindPreviousBusinessDay_OnBusinessDay_ReturnsSameDay()
    {
        var wednesday = new DateOnly(2024, 1, 10);
        var result = BusinessDayResolver.FindPreviousBusinessDay(wednesday, NoHolidays());
        result.Should().Be(wednesday);
    }

    [Fact]
    public void FindPreviousBusinessDay_OnWeekend_ReturnsPriorFriday()
    {
        var saturday = new DateOnly(2024, 1, 6);
        var result = BusinessDayResolver.FindPreviousBusinessDay(saturday, NoHolidays());
        result.Should().Be(new DateOnly(2024, 1, 5));
    }

    [Theory]
    [InlineData(2024, 1, 13, 2024, 1, 12)]
    [InlineData(2024, 1, 14, 2024, 1, 12)]
    public void WalkBackward_WeekendUsdDates_FallsBackToFriday(
        int fromYear, int fromMonth, int fromDay,
        int expectedYear, int expectedMonth, int expectedDay)
    {
        var from = new DateOnly(fromYear, fromMonth, fromDay);
        var expected = new DateOnly(expectedYear, expectedMonth, expectedDay);
        var first = BusinessDayResolver.WalkBackward(from, NoHolidays()).First();
        first.Should().Be(expected);
    }

    [Fact]
    public void WalkBackward_MultidayHolidayBlock_SkipsAllNonBusinessDays()
    {
        var holidays = new HolidayConf(new List<DateOnly> { new(2024, 2, 16) });
        var from = new DateOnly(2024, 2, 18); // Sunday
        var first = BusinessDayResolver.WalkBackward(from, holidays).First();
        first.Should().Be(new DateOnly(2024, 2, 15));
    }
}