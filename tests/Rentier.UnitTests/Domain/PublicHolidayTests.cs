using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;
using Xunit;

namespace Rentier.UnitTests;

public class PublicHolidayTests
{
    [Fact]
    public void Create_ValidInputs_ReturnsEntity()
    {
        var holiday = PublicHoliday.Create(new DateOnly(2025, 1, 1), "New Year's Day");
        holiday.Id.Should().NotBe(Guid.Empty);
        holiday.Date.Year.Should().Be(2025);
        holiday.Name.Should().Be("New Year's Day");
        holiday.Year.Should().Be(2025);
    }

    [Fact]
    public void Create_EmptyName_ThrowsDomainException()
    {
        var act = () => PublicHoliday.Create(new DateOnly(2025, 1, 1), "   ");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void YearProperty_MatchesDateYear()
    {
        var holiday = PublicHoliday.Create(new DateOnly(2026, 6, 28), "Vidovdan");
        holiday.Year.Should().Be(2026);
    }
}
