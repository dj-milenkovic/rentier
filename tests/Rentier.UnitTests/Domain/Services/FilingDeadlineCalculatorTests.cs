using FluentAssertions;
using Rentier.Domain.Exceptions;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

public class FilingDeadlineCalculatorTests
{
    private static HolidayConf NoHolidays => new(Array.Empty<DateOnly>());

    private static HolidayConf WithHolidays(params DateOnly[] dates) =>
        new(dates.ToList().AsReadOnly());

    // ── Happy path: weekday, no holidays ─────────────────────────────────
    [Theory]
    [InlineData("2024-01-01", "2024-01-31")] // +30 = Wed
    [InlineData("2024-01-06", "2024-02-05")] // +30 = Mon
    public void CalculateDeadline_WeekdayNoHolidays_ReturnsCandidateDirectly(
        string incomeStr, string expectedStr)
    {
        var incomeDate = DateOnly.Parse(incomeStr);
        var expected   = DateOnly.Parse(expectedStr);

        FilingDeadlineCalculator.CalculateDeadline(incomeDate, NoHolidays)
            .Should().Be(expected);
    }

    // ── Saturday advance ─────────────────────────────────────────────────
    [Fact]
    public void CalculateDeadline_PlusThirtyIsSaturday_AdvancesToMonday()
    {
        // 2024-01-04 + 30 = 2024-02-03 (Saturday) → 2024-02-05 (Monday)
        var result = FilingDeadlineCalculator.CalculateDeadline(
            new DateOnly(2024, 1, 4), NoHolidays);
        result.Should().Be(new DateOnly(2024, 2, 5));
        result.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    // ── Sunday advance ────────────────────────────────────────────────────
    [Fact]
    public void CalculateDeadline_PlusThirtyIsSunday_AdvancesToMonday()
    {
        // 2024-01-05 + 30 = 2024-02-04 (Sunday) → 2024-02-05 (Monday)
        var result = FilingDeadlineCalculator.CalculateDeadline(
            new DateOnly(2024, 1, 5), NoHolidays);
        result.Should().Be(new DateOnly(2024, 2, 5));
        result.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    // ── Weekday holiday ───────────────────────────────────────────────────
    [Fact]
    public void CalculateDeadline_PlusThirtyIsWeekdayHoliday_AdvancesOneDay()
    {
        // 2024-01-01 + 30 = 2024-01-31 (Wed), holiday on 2024-01-31 → 2024-02-01
        var result = FilingDeadlineCalculator.CalculateDeadline(
            new DateOnly(2024, 1, 1),
            WithHolidays(new DateOnly(2024, 1, 31)));
        result.Should().Be(new DateOnly(2024, 2, 1));
    }

    // ── Holiday on Saturday: weekend rule wins, Monday is clean ──────────
    [Fact]
    public void CalculateDeadline_HolidayOnSaturday_WeekendRuleWins_MondayClean()
    {
        // 2024-01-04 + 30 = 2024-02-03 (Sat). Holiday list has 2024-02-03 (Sat).
        // Weekend rule: Sat → Mon 2024-02-05. Monday is NOT in holiday list → return Mon.
        var result = FilingDeadlineCalculator.CalculateDeadline(
            new DateOnly(2024, 1, 4),
            WithHolidays(new DateOnly(2024, 2, 3))); // Sat is a "holiday" — irrelevant
        result.Should().Be(new DateOnly(2024, 2, 5));
    }

    // ── Friday is working day even if following Monday is holiday ─────────
    [Fact]
    public void CalculateDeadline_PlusThirtyIsFriday_MondayIsHoliday_ReturnsFriday()
    {
        // 2024-01-03 + 30 = 2024-02-02 (Fri). Holiday on Mon 2024-02-05 → Friday returned.
        var result = FilingDeadlineCalculator.CalculateDeadline(
            new DateOnly(2024, 1, 3),
            WithHolidays(new DateOnly(2024, 2, 5)));
        result.Should().Be(new DateOnly(2024, 2, 2));
        result.DayOfWeek.Should().Be(DayOfWeek.Friday);
    }

    // ── Consecutive holidays Mon+Tue → Wednesday ─────────────────────────
    [Fact]
    public void CalculateDeadline_ConsecutiveHolidaysMonTue_ReturnsWednesday()
    {
        // 2024-01-06 + 30 = 2024-02-05 (Mon). Holidays: Mon + Tue → 2024-02-07 (Wed).
        var result = FilingDeadlineCalculator.CalculateDeadline(
            new DateOnly(2024, 1, 6),
            WithHolidays(new DateOnly(2024, 2, 5), new DateOnly(2024, 2, 6)));
        result.Should().Be(new DateOnly(2024, 2, 7));
    }

    // ── Saturday → Monday (holiday) → Tuesday ────────────────────────────
    [Fact]
    public void CalculateDeadline_SaturdayAdvanceToHolidayMonday_ReturnsTuesday()
    {
        // 2024-01-04 + 30 = 2024-02-03 (Sat) → 2024-02-05 (Mon).
        // Mon is holiday → 2024-02-06 (Tue).
        var result = FilingDeadlineCalculator.CalculateDeadline(
            new DateOnly(2024, 1, 4),
            WithHolidays(new DateOnly(2024, 2, 5)));
        result.Should().Be(new DateOnly(2024, 2, 6));
    }

    // ── Leap year ─────────────────────────────────────────────────────────
    [Fact]
    public void CalculateDeadline_LeapYear_Feb29Included()
    {
        // 2024-01-29 + 30 = 2024-02-28 (Wed) — 2024 is leap year, Feb has 29 days
        var result = FilingDeadlineCalculator.CalculateDeadline(
            new DateOnly(2024, 1, 29), NoHolidays);
        result.Should().Be(new DateOnly(2024, 2, 28));
        result.DayOfWeek.Should().Be(DayOfWeek.Wednesday);
    }

    // ── Null holidays ─────────────────────────────────────────────────────
    [Fact]
    public void CalculateDeadline_NullHolidays_ThrowsDomainException()
    {
        var act = () => FilingDeadlineCalculator.CalculateDeadline(
            new DateOnly(2024, 1, 1), null!);
        act.Should().Throw<DomainException>()
            .WithMessage("*null*");
    }

    // ── Max iterations safety guard ───────────────────────────────────────
    [Fact]
    public void CalculateDeadline_MaxIterationsExceeded_ThrowsDomainException()
    {
        // 2024-04-22 + 30 = 2024-05-22 (Wed).
        // 14 weekday holidays starting at Wed 22 May exhaust all iterations:
        //   22,23,24 May (Wed–Fri) → Sat 25 skipped by weekend rule →
        //   27,28,29,30,31 May (Mon–Fri) → Sat 1 Jun skipped by weekend rule →
        //   3,4,5,6 Jun (Mon–Thu) = 12 weekday holidays + 2 Sat advances = 14 iterations, no return.
        var incomeDate = new DateOnly(2024, 4, 22);
        var holidays14 = new[]
        {
            new DateOnly(2024, 5, 22), new DateOnly(2024, 5, 23), new DateOnly(2024, 5, 24),
            new DateOnly(2024, 5, 27), new DateOnly(2024, 5, 28), new DateOnly(2024, 5, 29),
            new DateOnly(2024, 5, 30), new DateOnly(2024, 5, 31),
            new DateOnly(2024, 6, 3),  new DateOnly(2024, 6, 4),  new DateOnly(2024, 6, 5),
            new DateOnly(2024, 6, 6),  new DateOnly(2024, 6, 7),  new DateOnly(2024, 6, 10)
        };
        var act = () => FilingDeadlineCalculator.CalculateDeadline(
            incomeDate, WithHolidays(holidays14));
        act.Should().Throw<DomainException>()
            .WithMessage("*14*");
    }

    // ── Empty holiday list ────────────────────────────────────────────────
    [Fact]
    public void CalculateDeadline_EmptyHolidays_PureWeekendArithmetic()
    {
        // 2024-01-04 + 30 = 2024-02-03 (Sat) → 2024-02-05 (Mon)
        FilingDeadlineCalculator.CalculateDeadline(
            new DateOnly(2024, 1, 4), NoHolidays)
            .Should().Be(new DateOnly(2024, 2, 5));
    }
}
