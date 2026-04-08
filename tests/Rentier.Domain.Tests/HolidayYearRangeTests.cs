using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;
using Xunit;

namespace Rentier.Domain.Tests;

public class HolidayYearRangeTests
{
    [Fact]
    public void ValidRange_NoThrow()
    {
        var act = () => new HolidayYearRange(2024, 2027);
        act.Should().NotThrow();
    }

    [Fact]
    public void StartYearBelowMinimum_ThrowsDomainException()
    {
        var act = () => new HolidayYearRange(2019, 2024);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void EndYearExceedsMax_ThrowsDomainException()
    {
        var act = () => new HolidayYearRange(2024, 2035);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void EndYearEqualsStartPlusTen_IsValid()
    {
        var act = () => new HolidayYearRange(2024, 2034);
        act.Should().NotThrow();
    }

    [Fact]
    public void EndYearLessThanStartYear_ThrowsDomainException()
    {
        var act = () => new HolidayYearRange(2025, 2024);
        act.Should().Throw<DomainException>();
    }
}
