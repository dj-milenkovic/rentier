using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;
using Xunit;

namespace Rentier.UnitTests;

public class HolidayYearRangeTests
{
    [Fact]
    public void Constructor_Valid_SetsStartYearAndEndYear()
    {
        var range = new HolidayYearRange(2024, 2027);

        range.StartYear.Should().Be(2024);
        range.EndYear.Should().Be(2027);
    }

    [Fact]
    public void Constructor_Valid_HasSingletonId()
    {
        var range = new HolidayYearRange(2024, 2027);

        range.Id.Should().Be(HolidayYearRange.SingletonId);
    }

    [Fact]
    public void Constructor_ValidRange_DoesNotThrow()
    {
        var act = () => new HolidayYearRange(2024, 2027);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_StartYearBelowMinimum_ThrowsDomainException()
    {
        var act = () => new HolidayYearRange(2019, 2024);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Constructor_EndYearExceedsMax_ThrowsDomainException()
    {
        var act = () => new HolidayYearRange(2024, 2035);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Constructor_EndYearEqualsStartPlusTen_DoesNotThrow()
    {
        var act = () => new HolidayYearRange(2024, 2034);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_EndYearLessThanStartYear_ThrowsDomainException()
    {
        var act = () => new HolidayYearRange(2025, 2024);
        act.Should().Throw<DomainException>();
    }
}
