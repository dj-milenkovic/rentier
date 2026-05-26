using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Xunit;

namespace Rentier.UnitTests;

public class EnsureHolidaysSeededCommandHandlerTests
{
    private readonly IHolidayRepository _repo = Substitute.For<IHolidayRepository>();
    private readonly EnsureHolidaysSeededCommandHandler _sut;

    public EnsureHolidaysSeededCommandHandlerTests()
    {
        _sut = new EnsureHolidaysSeededCommandHandler(_repo);
    }

    [Fact]
    public async Task HandleAsync_WhenHolidaysAlreadyExist_ReturnsFalseWithoutSaving()
    {
        var existingRange = new HolidayYearRange(2024, 2027);
        _repo.GetYearRangeAsync(Arg.Any<CancellationToken>()).Returns(existingRange);

        var result = await _sut.HandleAsync(new EnsureHolidaysSeededCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        await _repo.DidNotReceive().SaveHolidaysAsync(
            Arg.Any<IReadOnlyList<PublicHoliday>>(),
            Arg.Any<HolidayYearRange>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenNoHolidaysExist_SeedsAndReturnsTrue()
    {
        _repo.GetYearRangeAsync(Arg.Any<CancellationToken>()).Returns((HolidayYearRange?)null);

        var result = await _sut.HandleAsync(new EnsureHolidaysSeededCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        await _repo.Received(1).SaveHolidaysAsync(
            Arg.Any<IReadOnlyList<PublicHoliday>>(),
            Arg.Any<HolidayYearRange>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenNoHolidaysExist_SeedsNineHolidaysPerYearForFourYears()
    {
        _repo.GetYearRangeAsync(Arg.Any<CancellationToken>()).Returns((HolidayYearRange?)null);
        var currentYear = DateOnly.FromDateTime(DateTime.Today).Year;
        // 9 fixed-date holidays × 4 years (currentYear through currentYear+3)
        var expectedCount = 9 * (currentYear + 3 - currentYear + 1);

        await _sut.HandleAsync(new EnsureHolidaysSeededCommand(), CancellationToken.None);

        await _repo.Received(1).SaveHolidaysAsync(
            Arg.Is<IReadOnlyList<PublicHoliday>>(list => list.Count == expectedCount),
            Arg.Any<HolidayYearRange>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenNoHolidaysExist_SeedsCurrentYearRange()
    {
        _repo.GetYearRangeAsync(Arg.Any<CancellationToken>()).Returns((HolidayYearRange?)null);
        var expectedStartYear = DateOnly.FromDateTime(DateTime.Today).Year;

        await _sut.HandleAsync(new EnsureHolidaysSeededCommand(), CancellationToken.None);

        await _repo.Received(1).SaveHolidaysAsync(
            Arg.Any<IReadOnlyList<PublicHoliday>>(),
            Arg.Is<HolidayYearRange>(range => range.StartYear == expectedStartYear),
            Arg.Any<CancellationToken>());
    }
}
