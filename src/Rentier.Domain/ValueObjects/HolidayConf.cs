using Rentier.Domain.Exceptions;

namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Represents a configured list of public holidays for filing deadline calculations.
/// All dates are DateOnly per constitution Principle III.
/// </summary>
public record HolidayConf
{
    public IReadOnlyList<DateOnly> Holidays { get; init; }

    public HolidayConf(IReadOnlyList<DateOnly> holidays)
    {
        Holidays = (holidays ?? throw new DomainException("Holidays list must not be null")).ToArray();
    }
}
