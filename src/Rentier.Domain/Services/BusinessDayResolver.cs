using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;

namespace Rentier.Domain.Services;

/// <summary>
/// Pure domain service for walking backward through business days.
/// Business day = Monday-Friday, not in HolidayConf.Holidays.
/// </summary>
public static class BusinessDayResolver
{
    /// <summary>Returns true if date is a weekday not in the holiday list.</summary>
    public static bool IsBusinessDay(DateOnly date, HolidayConf holidays)
    {
        if (holidays is null) throw new DomainException("HolidayConf must not be null");
        return date.DayOfWeek != DayOfWeek.Saturday
            && date.DayOfWeek != DayOfWeek.Sunday
            && !holidays.ContainsHoliday(date);
    }

    /// <summary>
    /// Lazily yields business days starting from fromDate - 1 day, walking backward
    /// for at most maxLookbackDays calendar days.
    /// </summary>
    public static IEnumerable<DateOnly> WalkBackward(
        DateOnly fromDate, HolidayConf holidays, int maxLookbackDays = 10)
    {
        if (holidays is null) throw new DomainException("HolidayConf must not be null");
        if (maxLookbackDays < 1) throw new DomainException("maxLookbackDays must be at least 1");

        var candidate = fromDate.AddDays(-1);
        var limit = fromDate.AddDays(-maxLookbackDays);

        while (candidate >= limit)
        {
            if (IsBusinessDay(candidate, holidays))
                yield return candidate;
            candidate = candidate.AddDays(-1);
        }
    }

    /// <summary>
    /// Returns date if it is already a business day; otherwise returns the first
    /// business day found by WalkBackward. Throws <see cref="DomainException"/> if none found within 10 days.
    /// </summary>
    public static DateOnly FindPreviousBusinessDay(DateOnly date, HolidayConf holidays)
    {
        if (IsBusinessDay(date, holidays)) return date;
        var found = WalkBackward(date.AddDays(1), holidays)
            .Select(d => (DateOnly?)d)
            .FirstOrDefault();
        return found ?? throw new DomainException($"No business day found within 10 calendar days before {date}");
    }
}
