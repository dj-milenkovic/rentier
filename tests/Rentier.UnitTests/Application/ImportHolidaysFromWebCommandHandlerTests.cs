using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Xunit;

namespace Rentier.UnitTests;

public class ImportHolidaysFromWebCommandHandlerTests
{
    private readonly IHolidayImporter _importer = Substitute.For<IHolidayImporter>();
    private readonly ImportHolidaysFromWebCommandHandler _handler;

    public ImportHolidaysFromWebCommandHandlerTests()
    {
        _handler = new ImportHolidaysFromWebCommandHandler(_importer);
    }

    [Fact]
    public async Task HandleAsync_ImporterSuccess_ReturnsHolidayList()
    {
        var holidays = new List<HolidayEntryDto> { new(new DateOnly(2025, 1, 1), "New Year") };
        _importer.ImportAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Success(holidays));

        var result = await _handler.HandleAsync(new ImportHolidaysFromWebCommand(2025));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("New Year");
    }

    [Fact]
    public async Task HandleAsync_ImporterFailure_ReturnsFailureResult()
    {
        _importer.ImportAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HolidayEntryDto>, Error>.Failure(new Error("HOLIDAY_IMPORT_FAILED", "HTTP 503")));

        var result = await _handler.HandleAsync(new ImportHolidaysFromWebCommand(2025));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("HOLIDAY_IMPORT_FAILED");
        result.Error.Message.Should().Be("HTTP 503");
    }
}

