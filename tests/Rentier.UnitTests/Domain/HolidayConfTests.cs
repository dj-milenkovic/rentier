using FluentAssertions;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

public class HolidayConfTests
{
    [Fact]
    public void Constructor_NullHolidays_ThrowsDomainException()
    {
        var act = () => new HolidayConf(null!);

        act.Should().Throw<DomainException>().WithMessage("*null*");
    }

    [Fact]
    public void Constructor_ValidHolidays_SetsHolidaysProperty()
    {
        var dates = new List<DateOnly> { new(2024, 1, 1), new(2024, 4, 5) };

        var conf = new HolidayConf(dates);

        conf.Holidays.Should().BeEquivalentTo(dates);
    }

    [Fact]
    public void Constructor_EmptyHolidays_IsValid()
    {
        var act = () => new HolidayConf(new List<DateOnly>());

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_EmptyHolidays_HolidaysPropertyIsEmpty()
    {
        var conf = new HolidayConf(new List<DateOnly>());

        conf.Holidays.Should().BeEmpty();
    }
}
