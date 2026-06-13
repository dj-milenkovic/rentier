using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Parsing;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Xunit;

namespace Rentier.UnitTests;

public class ImportReportCommandHandlerTests
{
    private readonly IReportRepository _reportRepo = Substitute.For<IReportRepository>();
    private readonly IStatementParser _parser = Substitute.For<IStatementParser>();
    private readonly ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>> _processReports
        = Substitute.For<ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>>();
    private readonly ImportReportCommandHandler _sut;

    public ImportReportCommandHandlerTests()
    {
        _sut = new ImportReportCommandHandler(_reportRepo, _parser, _processReports);

        // Default: parse succeeds with empty result
        _parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(
                new StatementParseResult([], [], [], [], [])));

        // Default: no duplicate
        _reportRepo.ExistsByImporterAndNameAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Default: process succeeds
        _processReports.HandleAsync(Arg.Any<ProcessReportsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProcessReportsResult, Error>.Success(
                new ProcessReportsResult(0, 0, 0, [])));
    }

    private static ImportReportCommand MakeCommand() =>
        new(Guid.NewGuid(), "stmt.csv", [1, 2, 3]);

    [Fact]
    public async Task HandleAsync_WithValidCsvAndNoExistingReport_PersistsReportAndReturnsId()
    {
        var cmd = MakeCommand();

        var result = await _sut.HandleAsync(cmd, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await _reportRepo.Received(1).AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithValidCsvAndNoExistingReport_TriggersProcessReportsCommand()
    {
        await _sut.HandleAsync(MakeCommand(), TestContext.Current.CancellationToken);

        await _processReports.Received(1).HandleAsync(
            Arg.Any<ProcessReportsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenCsvParseFailsBeforeDuplicateCheck_ReturnsInvalidCsvFailure()
    {
        _parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Failure(
                new Error("PARSE_ERROR", "Missing header")));

        var result = await _sut.HandleAsync(MakeCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("REPORT_IMPORT_INVALID_CSV");
        result.Error.Message.Should().Contain("Missing header");
    }

    [Fact]
    public async Task HandleAsync_WhenCsvParseFailsBeforeDuplicateCheck_DoesNotPersistReport()
    {
        _parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Failure(
                new Error("PARSE_ERROR", "Bad format")));

        await _sut.HandleAsync(MakeCommand(), TestContext.Current.CancellationToken);

        await _reportRepo.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenDuplicateExists_ReturnsDuplicateReportFailure()
    {
        _reportRepo.ExistsByImporterAndNameAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _sut.HandleAsync(MakeCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("REPORT_IMPORT_DUPLICATE");
    }

    [Fact]
    public async Task HandleAsync_WhenDuplicateExists_DoesNotPersistReport()
    {
        _reportRepo.ExistsByImporterAndNameAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await _sut.HandleAsync(MakeCommand(), TestContext.Current.CancellationToken);

        await _reportRepo.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenProcessingPipelineFails_ReturnsFailure()
    {
        _processReports.HandleAsync(Arg.Any<ProcessReportsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProcessReportsResult, Error>.Failure(
                new Error("PROCESS_FAILED", "Exchange rate unavailable")));

        var result = await _sut.HandleAsync(MakeCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PROCESS_FAILED");
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryThrows_ReturnsImportFailedError()
    {
        _reportRepo.AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("DB write failed"));

        var result = await _sut.HandleAsync(MakeCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("REPORT_IMPORT_FAILED");
        result.Error.Message.Should().Contain("DB write failed");
    }
}
