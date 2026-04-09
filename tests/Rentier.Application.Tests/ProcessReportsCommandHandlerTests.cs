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

namespace Rentier.Application.Tests;

public class ProcessReportsCommandHandlerTests
{
    private readonly Guid ProfileId = Guid.NewGuid();
    private static readonly DateOnly TestDate = new(2024, 6, 17);

    private static HolidayConfDto MakeHolidayDto()
        => new HolidayConfDto([], 2024, 2024);

    private static StatementParseResult MakeEmptyParseResult()
        => new StatementParseResult([], [], [], [], []);

    private Importer MakeImporter(Guid? profileId = null)
    {
        var importer = Importer.Create("Test Importer");
        importer.UpdateDetails("Test Importer", ReportType.IbkrCsv, profileId ?? ProfileId, null, "", "", "", "");
        return importer;
    }

    private static Report MakeReportWithContent(Guid importerId)
        => Report.Create(importerId, "statement.csv", [1, 2, 3], null);

    private static ExchangeRateResolver MakeResolver(IExchangeRateFetcher fetcher)
        => new ExchangeRateResolver(fetcher, NullLogger<ExchangeRateResolver>.Instance);

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
                .Returns(Result<StatementParseResult, Error>.Success(MakeEmptyParseResult()));
            parser = p;
        }

        var resolver = MakeResolver(rateFetcher ?? Substitute.For<IExchangeRateFetcher>());

        return new ProcessReportsCommandHandler(
            reportRepo ?? Substitute.For<IReportRepository>(),
            importerRepo ?? Substitute.For<IImporterRepository>(),
            filingRepo ?? Substitute.For<IFilingRepository>(),
            resolver,
            holidayRepo,
            parser,
            NullLogger<ProcessReportsCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_NoInitReports_ReturnsZeroProcessed()
    {
        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Report>());

        var handler = MakeHandler(reportRepo: reportRepo);
        var result = await handler.HandleAsync(new ProcessReportsCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.FilingsCreated.Should().Be(0);
        result.Value.ReportsProcessed.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_ReportWithNoContent_SetsReportToError()
    {
        var importer = MakeImporter();
        var report = Report.Create(importer.Id, "empty.csv", null, null);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var handler = MakeHandler(reportRepo: reportRepo, importerRepo: importerRepo);
        var result = await handler.HandleAsync(new ProcessReportsCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value.ReportsErrored.Should().Be(1);
        result.Value.EventErrors.Should().BeEmpty();
        await reportRepo.Received(1).UpdateAsync(
            Arg.Is<Report>(r => r.Status == ReportStatus.Error),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ImporterNotFound_SetsReportToError()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>())
            .Returns((Importer?)null);

        var handler = MakeHandler(reportRepo: reportRepo, importerRepo: importerRepo);
        var result = await handler.HandleAsync(new ProcessReportsCommand());

        result.Value.ReportsErrored.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_ParseFailure_SetsReportToError()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Failure(Error.Domain("bad CSV")));

        var handler = MakeHandler(reportRepo: reportRepo, importerRepo: importerRepo, parser: parser);
        var result = await handler.HandleAsync(new ProcessReportsCommand());

        result.Value.ReportsErrored.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_EmptyStatement_SetsReportProcessedNoFilings()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var handler = MakeHandler(reportRepo: reportRepo, importerRepo: importerRepo);
        var result = await handler.HandleAsync(new ProcessReportsCommand());

        result.Value.ReportsProcessed.Should().Be(1);
        result.Value.FilingsCreated.Should().Be(0);
        await reportRepo.Received(1).UpdateAsync(
            Arg.Is<Report>(r => r.Status == ReportStatus.Processed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DividendWithNoExistingFiling_CreatesFiling()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var dividends = new[] { new DividendRecord(TestDate, "USD", "ACME Corp", 100m) };
        var parseResult = new StatementParseResult(dividends, [], [], [], []);
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(parseResult));

        var rateFetcher = Substitute.For<IExchangeRateFetcher>();
        rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 117m)));

        var filingRepo = Substitute.For<IFilingRepository>();
        filingRepo.ExistsByIncomeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(),
                Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = MakeHandler(
            reportRepo: reportRepo, importerRepo: importerRepo,
            filingRepo: filingRepo, rateFetcher: rateFetcher, parser: parser);

        var result = await handler.HandleAsync(new ProcessReportsCommand());

        result.Value.FilingsCreated.Should().Be(1);
        await filingRepo.Received(1).AddAsync(
            Arg.Is<Filing>(f => f.PayingEntity == "ACME Corp" && f.IncomeType == IncomeType.Dividend),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DividendAlreadyExists_SkipsFiling()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var dividends = new[] { new DividendRecord(TestDate, "USD", "ACME Corp", 100m) };
        var parseResult = new StatementParseResult(dividends, [], [], [], []);
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(parseResult));

        var rateFetcher = Substitute.For<IExchangeRateFetcher>();
        rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 117m)));

        var filingRepo = Substitute.For<IFilingRepository>();
        filingRepo.ExistsByIncomeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(),
                Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = MakeHandler(
            reportRepo: reportRepo, importerRepo: importerRepo,
            filingRepo: filingRepo, rateFetcher: rateFetcher, parser: parser);

        var result = await handler.HandleAsync(new ProcessReportsCommand());

        result.Value.FilingsCreated.Should().Be(0);
        await filingRepo.DidNotReceive().AddAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InterestDebit_IsNotProcessed()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var interests = new[] { new InterestRecord(TestDate, "USD", "IB", 10m, InterestType.Debit) };
        var parseResult = new StatementParseResult([], interests, [], [], []);
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(parseResult));

        var filingRepo = Substitute.For<IFilingRepository>();
        var handler = MakeHandler(
            reportRepo: reportRepo, importerRepo: importerRepo,
            filingRepo: filingRepo, parser: parser);

        var result = await handler.HandleAsync(new ProcessReportsCommand());

        result.Value.FilingsCreated.Should().Be(0);
        await filingRepo.DidNotReceive().AddAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InterestCredit_CreatesFiling()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var interests = new[] { new InterestRecord(TestDate, "USD", "IB LLC", 50m, InterestType.Credit) };
        var parseResult = new StatementParseResult([], interests, [], [], []);
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(parseResult));

        var rateFetcher = Substitute.For<IExchangeRateFetcher>();
        rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 117m)));

        var filingRepo = Substitute.For<IFilingRepository>();
        filingRepo.ExistsByIncomeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(),
                Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = MakeHandler(
            reportRepo: reportRepo, importerRepo: importerRepo,
            filingRepo: filingRepo, rateFetcher: rateFetcher, parser: parser);

        var result = await handler.HandleAsync(new ProcessReportsCommand());

        result.Value.FilingsCreated.Should().Be(1);
        await filingRepo.Received(1).AddAsync(
            Arg.Is<Filing>(f => f.IncomeType == IncomeType.Interest),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RateNotFound_AddsDividendError()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var dividends = new[] { new DividendRecord(TestDate, "CHF", "Swiss Co", 100m) };
        var parseResult = new StatementParseResult(dividends, [], [], [], []);
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(parseResult));

        var rateFetcher = Substitute.For<IExchangeRateFetcher>();
        rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("RATE_NOT_FOUND", "Rate not found")));

        var filingRepo = Substitute.For<IFilingRepository>();
        var handler = MakeHandler(
            reportRepo: reportRepo, importerRepo: importerRepo,
            filingRepo: filingRepo, rateFetcher: rateFetcher, parser: parser);

        var result = await handler.HandleAsync(new ProcessReportsCommand());

        result.Value.ReportsErrored.Should().Be(1);
        result.Value.EventErrors.Should().HaveCount(1);
        result.Value.EventErrors[0].EntityName.Should().Be("Swiss Co");
    }

    [Fact]
    public async Task HandleAsync_CrossRateFallback_UsesIbkrRate()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var dividends = new[] { new DividendRecord(TestDate, "CHF", "Swiss Co", 100m) };
        var ibkrRates = new[] { new IbkrExchangeRate(TestDate, "CHF", "USD", 1.1m) };
        var parseResult = new StatementParseResult(dividends, [], [], ibkrRates, []);
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(parseResult));

        var rateFetcher = Substitute.For<IExchangeRateFetcher>();
        // CHF direct fails (non-retryable for fast execution), USD succeeds
        rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), "CHF", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("UNSUPPORTED_CURRENCY", "CHF not supported")));
        rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 100m)));

        var filingRepo = Substitute.For<IFilingRepository>();
        filingRepo.ExistsByIncomeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(),
                Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = MakeHandler(
            reportRepo: reportRepo, importerRepo: importerRepo,
            filingRepo: filingRepo, rateFetcher: rateFetcher, parser: parser);

        var result = await handler.HandleAsync(new ProcessReportsCommand());

        // CHF cross-rate: 100 * (1.1 * 100) = 11000 RSD gross
        result.Value.FilingsCreated.Should().Be(1);
        result.Value.EventErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ImporterHasNoTaxpayerProfile_SetsReportToError()
    {
        var importer = Importer.Create("No Profile");
        // TaxpayerProfileId stays null — not set

        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var handler = MakeHandler(reportRepo: reportRepo, importerRepo: importerRepo);
        var result = await handler.HandleAsync(new ProcessReportsCommand());

        result.Value.ReportsErrored.Should().Be(1);
        result.Value.EventErrors.Should().BeEmpty();
    }
}