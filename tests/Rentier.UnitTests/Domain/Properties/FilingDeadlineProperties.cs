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
}
