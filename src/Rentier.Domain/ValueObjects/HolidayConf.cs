using Rentier.Domain.Exceptions;

namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Represents a configured list of public holidays for filing deadline calculations.
/// All dates are DateOnly per constitution Principle III.
/// Properties are get-only to prevent validation bypass via <c>with</c> expressions.
/// </summary>
public sealed record HolidayConf
{
    public IReadOnlyList<DateOnly> Holidays { get; }

    // Pre-built set for O(1) holiday lookups in BusinessDayResolver.
    private readonly HashSet<DateOnly> _holidaySet;

    public HolidayConf(IReadOnlyList<DateOnly> holidays)
    {
        var arr = (holidays ?? throw new DomainException("Holidays list must not be null")).ToArray();
        Holidays = arr;
        _holidaySet = new HashSet<DateOnly>(arr);
    }

    /// <summary>Returns true when <paramref name="date"/> is in the configured holiday list.</summary>
    public bool ContainsHoliday(DateOnly date) => _holidaySet.Contains(date);
}
