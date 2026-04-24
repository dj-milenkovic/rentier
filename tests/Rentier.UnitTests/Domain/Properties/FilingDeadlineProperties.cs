using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;

namespace Rentier.UnitTests;

/// <summary>
/// Property-based tests for FilingDeadlineCalculator invariants.
/// Uses FsCheck to verify that deadline calculation rules hold for all valid dates.
/// </summary>
public class FilingDeadlineProperties
{
    /// <summary>
    /// Empty holiday configuration for property tests.
    /// </summary>
    private static readonly HolidayConf EmptyHolidays = new(Array.Empty<DateOnly>());

    /// <summary>
    /// Verifies that the filing deadline is always at least 30 days after the payment date.
    /// This is the minimum legal requirement for PP-OPO filings.
    /// </summary>
    [Property]
    public Property Deadline_IsAlwaysAtLeast30DaysAfterPaymentDate(
        PositiveInt yearOffset,
        PositiveInt monthVal,
        PositiveInt dayVal)
    {
        // Generate valid dates between 2020-2029
        var year = 2020 + (yearOffset.Get % 10);
        var month = (monthVal.Get % 12) + 1; // 1-12
        var day = (dayVal.Get % 28) + 1; // 1-28 (safe for all months)

        var paymentDate = new DateOnly(year, month, day);

        var deadline = FilingDeadlineCalculator.CalculateDeadline(paymentDate, EmptyHolidays);

        var minimumDeadline = paymentDate.AddDays(30);

        return (deadline >= minimumDeadline).ToProperty();
    }

    // ── T005: Deadline never falls on weekend or public holiday ───────────────

    /// <summary>
    /// Verifies that the filing deadline never lands on a Saturday, Sunday, or
    /// any date in the configured holiday set. Uses random income dates and
    /// random sparse holiday sets (up to 3 weekday holidays near the deadline).
    /// </summary>
    [Property]
    public Property CalculateDeadline_AnyIncomeDateAndHolidaySet_DeadlineNeverFallsOnWeekendOrHoliday(
        PositiveInt yearOffset,
        PositiveInt monthVal,
        PositiveInt dayVal,
        NonNegativeInt holiday1Offset,
        NonNegativeInt holiday2Offset,
        NonNegativeInt holiday3Offset)
    {
        var year = 2020 + (yearOffset.Get % 10);
        var month = (monthVal.Get % 12) + 1;
        var day = (dayVal.Get % 28) + 1;
        var incomeDate = new DateOnly(year, month, day);

        // Generate up to 3 distinct weekday holidays near the candidate deadline
        // Keep them sparse (within 20-day window) so MaxIterations (14) is never hit
        var baseCandidate = incomeDate.AddDays(30);
        var potentialHolidays = new[]
        {
            baseCandidate.AddDays(holiday1Offset.Get % 7),
            baseCandidate.AddDays(holiday2Offset.Get % 7),
            baseCandidate.AddDays(holiday3Offset.Get % 7),
        };

        // Only use weekday holidays to avoid pathological weekend-holiday chains
        var holidays = potentialHolidays
            .Where(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
            .Distinct()
            .Take(3)
            .ToArray();

        var conf = new HolidayConf(holidays);

        // With at most 3 weekday holidays in a 7-day window, we'll never exceed
        // MaxIterations (14) — the calculator is guaranteed to find a working day.
        var deadline = FilingDeadlineCalculator.CalculateDeadline(incomeDate, conf);

        bool notSaturday = deadline.DayOfWeek != DayOfWeek.Saturday;
        bool notSunday   = deadline.DayOfWeek != DayOfWeek.Sunday;
        bool notHoliday  = !conf.ContainsHoliday(deadline);

        return (notSaturday && notSunday && notHoliday).ToProperty();
    }
}
