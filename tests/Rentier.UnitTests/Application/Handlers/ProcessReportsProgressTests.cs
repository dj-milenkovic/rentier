using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Parsing;
using Rentier.Application.Repositories;
using Rentier.Application.Services;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// T006 + T007 — Per-report progress emission tests for ProcessReportsCommandHandler.
/// Covers: success/partial-error/total-failure severity, error-branch severity, null-safe guard.
/// </summary>
public class ProcessReportsProgressTests
{
    private readonly Guid _profileId = Guid.NewGuid();
    private static readonly DateOnly TestDate = new(2024, 6, 17);

    // ── Factory helpers ───────────────────────────────────────────────────────

    private static HolidayConfDto MakeHolidayDto()
        => new HolidayConfDto([], 2024, 2024);

    private Importer MakeImporter()
    {
        var importer = Importer.Create("Test Importer");
        importer.UpdateDetails("Test Importer", ReportType.IbkrCsv, _profileId, null, "", "", "", "");
        return importer;
    }

    private static Report MakeReportWithContent(Guid importerId, string name = "statement.csv")
        => Report.Create(importerId, name, [1, 2, 3], null);

    private static ExchangeRateResolver MakeResolver(IExchangeRateFetcher fetcher)
        => new ExchangeRateResolver(fetcher, NullLogger<ExchangeRateResolver>.Instance);

    private IExchangeRateFetcher MakeUsdFetcher()
    {
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 108m)));
        return fetcher;
    }

    private ProcessReportsCommandHandler MakeHandler(
        IReportRepository? reportRepo = null,
        IImporterRepository? importerRepo = null,
        IFilingRepository? filingRepo = null,
        IExchangeRateFetcher? rateFetcher = null,
        IHolidayRepository? holidayRepo = null,
        IStatementParser? parser = null)
    {
        if (holidayRepo == null)
        {
            var hr = Substitute.For<IHolidayRepository>();
            hr.GetHolidayConfAsync(Arg.Any<CancellationToken>()).Returns(MakeHolidayDto());
            holidayRepo = hr;
        }
        if (parser == null)
        {
            var p = Substitute.For<IStatementParser>();
            p.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(Result<StatementParseResult, Error>.Success(
                    new StatementParseResult([], [], [], [], [])));
            parser = p;
        }

        return new ProcessReportsCommandHandler(
            reportRepo ?? Substitute.For<IReportRepository>(),
            importerRepo ?? Substitute.For<IImporterRepository>(),
            filingRepo ?? Substitute.For<IFilingRepository>(),
            MakeResolver(rateFetcher ?? Substitute.For<IExchangeRateFetcher>()),
            holidayRepo,
            parser,
            NullLogger<ProcessReportsCommandHandler>.Instance);
    }

    // ── T006: success / partial-error / total-failure severity ───────────────

    [Fact]
    public async Task HandleAsync_AllSuccessReport_EmitsOneInfoEntry()
    {
        // Arrange
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id, "success.csv");

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var dividends = new[] { new DividendRecord(TestDate, "USD", "ACME Corp", 100m) };
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(
                new StatementParseResult(dividends, [], [], [], [])));

        var filingRepo = Substitute.For<IFilingRepository>();
        filingRepo.ExistsByIncomeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(),
            Arg.Any<decimal>(), Arg.Any<CancellationToken>()).Returns(false);

        var reported = new List<SyncProgressEntry>();
        var progress = new Progress<SyncProgressEntry>(e => reported.Add(e));

        var handler = MakeHandler(reportRepo, importerRepo, filingRepo, MakeUsdFetcher(), parser: parser);

        // Act
        await handler.HandleAsync(new ProcessReportsCommand(progress));

        // Assert — give Progress<T> callback time to fire
        await Task.Delay(50);

        var perReport = reported.Where(e => e.Message.StartsWith("Report '")).ToList();
        perReport.Should().HaveCount(1);
        perReport[0].Severity.Should().Be(SyncProgressSeverity.Info);
        perReport[0].Message.Should().Be("Report 'success.csv': 1 filing(s) created, 0 failed.");
    }

    [Fact]
    public async Task HandleAsync_PartialErrorReport_EmitsOneWarningEntry()
    {
        // Arrange
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id, "partial.csv");

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        // 1 USD succeeds, 1 CHF fails
        var dividends = new[]
        {
            new DividendRecord(TestDate, "USD", "Good Corp", 100m),
            new DividendRecord(TestDate, "CHF", "Bad Corp", 50m),
        };
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(
                new StatementParseResult(dividends, [], [], [], [])));

        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 108m)));
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), "CHF", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("UNSUPPORTED_CURRENCY", "CHF not supported")));

        var filingRepo = Substitute.For<IFilingRepository>();
        filingRepo.ExistsByIncomeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(),
            Arg.Any<decimal>(), Arg.Any<CancellationToken>()).Returns(false);

        var reported = new List<SyncProgressEntry>();
        var progress = new Progress<SyncProgressEntry>(e => reported.Add(e));

        var handler = MakeHandler(reportRepo, importerRepo, filingRepo, fetcher, parser: parser);

        // Act
        await handler.HandleAsync(new ProcessReportsCommand(progress));
        await Task.Delay(50);

        // Assert
        var perReport = reported.Where(e => e.Message.StartsWith("Report '")).ToList();
        perReport.Should().HaveCount(1);
        perReport[0].Severity.Should().Be(SyncProgressSeverity.Warning);
        perReport[0].Message.Should().Be("Report 'partial.csv': 1 filing(s) created, 1 failed.");
    }

    [Fact]
    public async Task HandleAsync_TotalFailureReport_EmitsOneErrorEntry()
    {
        // Arrange
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id, "total-fail.csv");

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        // Both dividends fail (CHF unsupported)
        var dividends = new[]
        {
            new DividendRecord(TestDate, "CHF", "Swiss A", 100m),
            new DividendRecord(TestDate, "CHF", "Swiss B", 50m),
        };
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(
                new StatementParseResult(dividends, [], [], [], [])));

        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), "CHF", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("UNSUPPORTED_CURRENCY", "CHF not supported")));

        var reported = new List<SyncProgressEntry>();
        var progress = new Progress<SyncProgressEntry>(e => reported.Add(e));

        var handler = MakeHandler(reportRepo, importerRepo, Substitute.For<IFilingRepository>(), fetcher, parser: parser);

        // Act
        await handler.HandleAsync(new ProcessReportsCommand(progress));
        await Task.Delay(50);

        // Assert
        var perReport = reported.Where(e => e.Message.StartsWith("Report '")).ToList();
        perReport.Should().HaveCount(1);
        perReport[0].Severity.Should().Be(SyncProgressSeverity.Error);
        perReport[0].Message.Should().Be("Report 'total-fail.csv': 0 filing(s) created, 2 failed.");
    }

    [Fact]
    public async Task HandleAsync_NullProgress_DoesNotThrow()
    {
        // Arrange — null progress (no-op scenario, backward compat)
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var filingRepo = Substitute.For<IFilingRepository>();

        var handler = MakeHandler(reportRepo, importerRepo, filingRepo);

        // Act + Assert — should not throw
        var act = async () => await handler.HandleAsync(new ProcessReportsCommand(Progress: null));
        await act.Should().NotThrowAsync();
    }

    // ── T007: error-branch severity ──────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoAttachmentReport_EmitsErrorSeverityEntry()
    {
        // Arrange — report has no attachment content
        var importer = MakeImporter();
        var report = Report.Create(importer.Id, "no-attach.csv", null, null);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var reported = new List<SyncProgressEntry>();
        var progress = new Progress<SyncProgressEntry>(e => reported.Add(e));

        var handler = MakeHandler(reportRepo);

        // Act
        await handler.HandleAsync(new ProcessReportsCommand(progress));
        await Task.Delay(50);

        // Assert
        var perReport = reported.Where(e => e.Message.StartsWith("Report '")).ToList();
        perReport.Should().HaveCount(1);
        perReport[0].Severity.Should().Be(SyncProgressSeverity.Error);
        perReport[0].Message.Should().StartWith("Report 'no-attach.csv': processing error —");
    }

    [Fact]
    public async Task HandleAsync_ImporterNotFound_EmitsErrorSeverityEntry()
    {
        // Arrange — importer repo returns null
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id, "no-importer.csv");

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>())
            .Returns((Importer?)null);

        var reported = new List<SyncProgressEntry>();
        var progress = new Progress<SyncProgressEntry>(e => reported.Add(e));

        var handler = MakeHandler(reportRepo, importerRepo);

        // Act
        await handler.HandleAsync(new ProcessReportsCommand(progress));
        await Task.Delay(50);

        // Assert
        var perReport = reported.Where(e => e.Message.StartsWith("Report '")).ToList();
        perReport.Should().HaveCount(1);
        perReport[0].Severity.Should().Be(SyncProgressSeverity.Error);
        perReport[0].Message.Should().StartWith("Report 'no-importer.csv': processing error —");
    }

    [Fact]
    public async Task HandleAsync_ParseFailure_EmitsErrorSeverityEntry()
    {
        // Arrange — parser returns failure
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id, "parse-fail.csv");

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Failure(
                new Error("PARSE_ERROR", "Invalid CSV format")));

        var reported = new List<SyncProgressEntry>();
        var progress = new Progress<SyncProgressEntry>(e => reported.Add(e));

        var handler = MakeHandler(reportRepo, importerRepo, parser: parser);

        // Act
        await handler.HandleAsync(new ProcessReportsCommand(progress));
        await Task.Delay(50);

        // Assert
        var perReport = reported.Where(e => e.Message.StartsWith("Report '")).ToList();
        perReport.Should().HaveCount(1);
        perReport[0].Severity.Should().Be(SyncProgressSeverity.Error);
        perReport[0].Message.Should().StartWith("Report 'parse-fail.csv': processing error —");
    }

    [Fact]
    public async Task HandleAsync_UnexpectedException_EmitsErrorSeverityEntry()
    {
        // Arrange — statementParser.ParseAsync throws (not returns Failure, but throws).
        // This exception is NOT caught inside ProcessReportAsync (which has no try-catch),
        // so it propagates to the outer foreach catch block in HandleAsync.
        // The outer catch emits a "processing error —" entry with Error severity.
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id, "exception.csv");

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });
        // UpdateAsync succeeds (called in the outer catch path)
        reportRepo.UpdateAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        // Parser *throws* (not returns Failure) — simulates unexpected infrastructure exception
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<StatementParseResult, Error>>>(_ =>
                throw new InvalidOperationException("DB connection lost"));

        var filingRepo = Substitute.For<IFilingRepository>();

        var reported = new List<SyncProgressEntry>();
        var progress = new Progress<SyncProgressEntry>(e => reported.Add(e));

        var handler = MakeHandler(reportRepo, importerRepo, filingRepo, parser: parser);

        // Act
        await handler.HandleAsync(new ProcessReportsCommand(progress));
        await Task.Delay(50);

        // Assert — an error-severity entry is emitted for the report-level exception
        var perReport = reported.Where(e => e.Message.StartsWith("Report '")).ToList();
        perReport.Should().HaveCount(1);
        perReport[0].Severity.Should().Be(SyncProgressSeverity.Error);
        perReport[0].Message.Should().StartWith("Report 'exception.csv': processing error —");
    }
}
