using FluentAssertions;
using NSubstitute;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Domain.Entities;
using Xunit;

namespace Rentier.Application.Tests;

public class GetHolidayConfQueryHandlerTests
{
    private readonly IHolidayRepository _repo = Substitute.For<IHolidayRepository>();
    private readonly GetHolidayConfQueryHandler _handler;

    public GetHolidayConfQueryHandlerTests()
    {
        _handler = new GetHolidayConfQueryHandler(_repo);
    }

    [Fact]
    public async Task FirstRun_NoYearRange_SeedsAndReturnsDto()
    {
        var currentYear = DateOnly.FromDateTime(DateTime.Today).Year;
        var seedDto = new HolidayConfDto(
            new List<HolidayEntryDto> { new(new DateOnly(currentYear, 1, 1), "Nova godina") },
            currentYear, currentYear + 3);

        _repo.GetYearRangeAsync(Arg.Any<CancellationToken>()).Returns((HolidayYearRange?)null);
        _repo.GetHolidayConfAsync(Arg.Any<CancellationToken>()).Returns(seedDto);

        var result = await _handler.HandleAsync(new GetHolidayConfQuery());

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).SaveHolidaysAsync(
            Arg.Is<IReadOnlyList<PublicHoliday>>(list => list.Count > 0),
            Arg.Is<HolidayYearRange>(r => r.StartYear == currentYear),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmptyDatabase_AfterSeed_ReturnsPopulatedDto()
    {
        var currentYear = DateOnly.FromDateTime(DateTime.Today).Year;
        var populatedDto = new HolidayConfDto(
            new List<HolidayEntryDto>
            {
                new(new DateOnly(currentYear, 1, 1), "Nova godina"),
                new(new DateOnly(currentYear, 1, 7), "Božić"),
            },
            currentYear, currentYear + 3);

        _repo.GetYearRangeAsync(Arg.Any<CancellationToken>()).Returns((HolidayYearRange?)null);
        _repo.GetHolidayConfAsync(Arg.Any<CancellationToken>()).Returns(populatedDto);

        var result = await _handler.HandleAsync(new GetHolidayConfQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.Holidays.Count.Should().Be(2);
    }

    [Fact]
    public async Task PopulatedDatabase_ReturnsMappedDto()
    {
        var existingRange = new HolidayYearRange(2025, 2028);
        var existingDto = new HolidayConfDto(
            new List<HolidayEntryDto>
            {
                new(new DateOnly(2025, 1, 1), "Nova godina"),
                new(new DateOnly(2025, 1, 7), "Božić"),
            },
            2025, 2028);

        _repo.GetYearRangeAsync(Arg.Any<CancellationToken>()).Returns(existingRange);
        _repo.GetHolidayConfAsync(Arg.Any<CancellationToken>()).Returns(existingDto);

        var result = await _handler.HandleAsync(new GetHolidayConfQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.StartYear.Should().Be(2025);
        result.Value.EndYear.Should().Be(2028);
        result.Value.Holidays.Count.Should().Be(2);
        await _repo.DidNotReceive().SaveHolidaysAsync(
            Arg.Any<IReadOnlyList<PublicHoliday>>(),
            Arg.Any<HolidayYearRange>(),
            Arg.Any<CancellationToken>());
    }
}
