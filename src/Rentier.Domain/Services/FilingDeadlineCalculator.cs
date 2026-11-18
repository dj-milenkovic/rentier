using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;

namespace Rentier.Domain.Services;

/// <summary>
/// Computes the PP-OPO filing deadline for a given income date.
/// Deadline = incomeDate + 30 calendar days, advanced past weekends and Serbian public holidays.
/// Pure domain logic — no I/O, no async, DateOnly only.
/// </summary>
public static class FilingDeadlineCalculator
{
    private const int MaxIterations = 14;

    /// <summary>
    /// Returns the filing deadline: the first working day on or after incomeDate + 30 days.
    /// Working day = <see cref="HolidayConf.IsWorkingDay"/>.
    /// </summary>
    public static DateOnly CalculateDeadline(DateOnly incomeDate, HolidayConf holidays)
    {
        if (holidays is null)
            throw new DomainException("HolidayConf must not be null");

        var candidate = incomeDate.AddDays(30);

        for (int i = 0; i < MaxIterations; i++)
        {
            if (holidays.IsWorkingDay(candidate))
                return candidate;

            // Saturday jumps straight to Monday; Sunday or a holiday moves one day. Re-checked
            // each step, since a shift can land on another holiday or weekend.
            candidate = candidate.AddDays(candidate.DayOfWeek == DayOfWeek.Saturday ? 2 : 1);
        }

        throw new DomainException(
            $"Could not find a working day within {MaxIterations} days of the deadline — check holiday configuration.");
    }
}
