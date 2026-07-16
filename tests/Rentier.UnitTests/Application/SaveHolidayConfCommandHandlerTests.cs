using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Xunit;

namespace Rentier.UnitTests.Application;

public class SaveHolidayConfCommandHandlerTests
{
    private readonly IHolidayRepository _repo = Substitute.For<IHolidayRepository>();
    private readonly SaveHolidayConfCommandHandler _handler;

    public SaveHolidayConfCommandHandlerTests()
    {
        _handler = new SaveHolidayConfCommandHandler(_repo);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_SavesHolidays()
    {
        var cmd = new SaveHolidayConfCommand(
            new List<HolidayEntryDto> { new(new DateOnly(2025, 1, 1), "Nova godina") },
            2025, 2028);

        var result = await _handler.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).SaveHolidaysAsync(
            Arg.Any<IReadOnlyList<PublicHoliday>>(),
            Arg.Any<HolidayYearRange>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SaveEmptyList_Allowed()
    {
        var cmd = new SaveHolidayConfCommand(new List<HolidayEntryDto>(), 2025, 2028);

        var result = await _handler.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).SaveHolidaysAsync(
            Arg.Is<IReadOnlyList<PublicHoliday>>(list => list!.Count == 0),
            Arg.Any<HolidayYearRange>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InvalidYearRange_ReturnsDomainError()
    {
        var cmd = new SaveHolidayConfCommand(new List<HolidayEntryDto>(), 2019, 2025);

        var result = await _handler.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("HOLIDAY_SAVE_INVALID_YEAR_RANGE");
        await _repo.DidNotReceive().SaveHolidaysAsync(
            Arg.Any<IReadOnlyList<PublicHoliday>>(),
            Arg.Any<HolidayYearRange>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DuplicateDates_ReturnsDuplicateError()
    {
        var date = new DateOnly(2025, 1, 1);
        var cmd = new SaveHolidayConfCommand(
            new List<HolidayEntryDto>
            {
                new(date, "Nova godina"),
                new(date, "Nova godina duplicate"),
            },
            2025, 2028);

        var result = await _handler.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("HOLIDAY_SAVE_DUPLICATE_DATES");
        await _repo.DidNotReceive().SaveHolidaysAsync(
            Arg.Any<IReadOnlyList<PublicHoliday>>(),
            Arg.Any<HolidayYearRange>(),
            Arg.Any<CancellationToken>());
    }
}
