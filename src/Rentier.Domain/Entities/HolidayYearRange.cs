using Rentier.Domain.Exceptions;

namespace Rentier.Domain.Entities;

public sealed class HolidayYearRange
{
    public const int SingletonId = 1;
    public const int MinStartYear = 2020;
    public const int MaxYearSpan = 10;

    public int Id { get; private set; }
    public int StartYear { get; private set; }
    public int EndYear { get; private set; }

    private HolidayYearRange() { }

    public HolidayYearRange(int startYear, int endYear)
    {
        if (startYear < MinStartYear)
            throw new DomainException($"StartYear must be >= {MinStartYear}.");
        if (endYear > startYear + MaxYearSpan)
            throw new DomainException($"EndYear must be <= StartYear + {MaxYearSpan}.");
        if (endYear < startYear)
            throw new DomainException("EndYear must be >= StartYear.");
        Id = SingletonId;
        StartYear = startYear;
        EndYear = endYear;
    }
}
