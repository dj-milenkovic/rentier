using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Xunit;

namespace Rentier.UnitTests;

public class FetchHolidaysFromWebCommandHandlerTests
{
    private readonly IHolidayImporter _importer = Substitute.For<IHolidayImporter>();
    private readonly FetchHolidaysFromWebCommandHandler _handler;

    public FetchHolidaysFromWebCommandHandlerTests()
    {
        _handler = new FetchHolidaysFromWebCommandHandler(_importer);
    }

    [Fact]
    public async Task HandleAsync_AllYearsSucceed_ReturnsMergedDeduplicatedList()
    {
        var holidays2024 = new List<HolidayEntryDto>
        {
            new(new DateOnly(2024, 1, 1), "Nova godina"),
            new(new DateOnly(2024, 2, 15), "Sretenje")
        };
        var holidays2025 = new List<HolidayEntryDto>
        {
            new(new DateOnly(2025, 1, 1), "Nova godina"),
            new(new DateOnly(2025, 5, 1), "Praznik rada")
        };

        _importer.ImportAsync(2024, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(holidays2024));
        _importer.ImportAsync(2025, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(holidays2025));

        var result = await _handler.HandleAsync(new FetchHolidaysFromWebCommand(2024, 2025));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(4);
        result.Value.Should().BeInAscendingOrder(d => d.Date);
    }

    [Fact]
    public async Task HandleAsync_OneYearFails_ReturnsSuccessWithRemainingYears()
    {
        var holidays2024 = new List<HolidayEntryDto>
        {
            new(new DateOnly(2024, 1, 1), "Nova godina")
        };

        _importer.ImportAsync(2024, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(holidays2024));
        _importer.ImportAsync(2025, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(
                new Error("HOLIDAY_FETCH_FAILED", "HTTP 503")));

        var result = await _handler.HandleAsync(new FetchHolidaysFromWebCommand(2024, 2025));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Nova godina");
    }

    [Fact]
    public async Task HandleAsync_AllYearsFail_ReturnsFailure()
    {
        _importer.ImportAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(
                new Error("HOLIDAY_FETCH_FAILED", "Network error")));

        var result = await _handler.HandleAsync(new FetchHolidaysFromWebCommand(2024, 2025));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("HOLIDAY_FETCH_ALL_FAILED");
        result.Error.Message.Should().Contain("2024");
        result.Error.Message.Should().Contain("2025");
    }

    [Fact]
    public async Task HandleAsync_DuplicateDatesAcrossYears_DeduplicatesByDate()
    {
        // Same date published in both calls (edge case: same date returned for different year queries)
        var date = new DateOnly(2024, 12, 31);
        var holidays1 = new List<HolidayEntryDto> { new(date, "New Year's Eve") };
        var holidays2 = new List<HolidayEntryDto> { new(date, "New Year's Eve (duplicate)") };

        _importer.ImportAsync(2024, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(holidays1));
        _importer.ImportAsync(2025, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(holidays2));

        var result = await _handler.HandleAsync(new FetchHolidaysFromWebCommand(2024, 2025));

        result.IsSuccess.Should().BeTrue();
        // De-duplicated by date: only the first occurrence (from 2024) kept
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("New Year's Eve");
    }
}
