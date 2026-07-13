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

namespace Rentier.UnitTests.Application;

/// <summary>
/// Tests for the cross-rate synthesis logic inside BuildRateProvider
/// (ProcessReportsCommandHandler): CHF→USD→RSD synthetic rate path.
/// </summary>
public class CrossRateSynthesisTests
{
    private readonly Guid _profileId = Guid.NewGuid();
    private static readonly DateOnly TestDate = new(2024, 6, 17);

    private Importer MakeImporter()
    {
        var importer = Importer.Create("Test Importer");
        importer.UpdateDetails("Test Importer", ReportType.IbkrCsv, _profileId, null, "", "", "", "");
        return importer;
    }

    private static Report MakeReportWithContent(Guid importerId)
        => Report.Create(importerId, "statement.csv", [1, 2, 3], null);

    private ProcessReportsCommandHandler MakeHandler(
        IReportRepository reportRepo,
        IImporterRepository importerRepo,
        IFilingRepository filingRepo,
        IExchangeRateFetcher rateFetcher,
        IStatementParser parser)
    {
        var holidayRepo = Substitute.For<IHolidayRepository>();
        holidayRepo.GetHolidayConfAsync(Arg.Any<CancellationToken>())
            .Returns(new HolidayConfDto([], 2024, 2024));

        var resolver = new ExchangeRateResolver(rateFetcher, NullLogger<ExchangeRateResolver>.Instance);

        return new ProcessReportsCommandHandler(
            reportRepo, importerRepo, filingRepo, resolver,
            holidayRepo, parser,
            NullLogger<ProcessReportsCommandHandler>.Instance);
    }

    private IStatementParser MakeParserReturning(StatementParseResult result)
    {
        var parser = Substitute.For<IStatementParser>();
        parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(result));
        return parser;
    }

    /// <summary>
    /// CHF has no NBS rate (UNSUPPORTED_CURRENCY = non-retryable),
    /// IBKR CHF→USD rate is present, but USD NBS rate is also unavailable.
    /// Expected: BuildRateProvider falls through to return the original CHF failure.
    /// </summary>
    [Fact]
    public async Task HandleAsync_CrossRate_UsdPivotFails_AddsEventError()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        // CHF fails (non-retryable), IBKR rate exists, USD also fails
        var dividends = new[] { new DividendRecord(TestDate, "CHF", "Swiss Corp", 100m) };
        var ibkrRates = new[] { new IbkrExchangeRate(TestDate, "CHF", "USD", 1.1m) };
        var parseResult = new StatementParseResult(dividends, [], [], ibkrRates, []);
        var parser = MakeParserReturning(parseResult);

        var rateFetcher = Substitute.For<IExchangeRateFetcher>();
        // CHF: UNSUPPORTED_CURRENCY (non-retryable — resolver returns immediately)
        rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), "CHF", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("UNSUPPORTED_CURRENCY", "CHF not in NBS")));
        // USD: also fails (e.g. holiday weekend exhausted)
        rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("RATE_NOT_FOUND", "USD unavailable")));

        var filingRepo = Substitute.For<IFilingRepository>();
        var handler = MakeHandler(reportRepo, importerRepo, filingRepo, rateFetcher, parser);

        var result = await handler.HandleAsync(new ProcessReportsCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.FilingsCreated.Should().Be(0);
        result.Value.EventErrors.Should().HaveCount(1);
        result.Value.EventErrors[0].EntityName.Should().Be("Swiss Corp");
    }

    /// <summary>
    /// CHF has no NBS rate, IBKR CHF→USD rate has value 0 (edge case — parser
    /// would normally reject this, but tests the guard inside BuildRateProvider).
    /// Expected: synthetic rate = 0 * usdRate ≤ 0 → INVALID_EXCHANGE_RATE error.
    /// </summary>
    [Fact]
    public async Task HandleAsync_CrossRate_ZeroIbkrRate_ReturnsInvalidExchangeRateError()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        // CHF fails (non-retryable), IBKR rate = 0 (so synthetic = 0 * usdRate = 0)
        var dividends = new[] { new DividendRecord(TestDate, "CHF", "Swiss Corp", 100m) };
        var ibkrRates = new[] { new IbkrExchangeRate(TestDate, "CHF", "USD", 0m) };
        var parseResult = new StatementParseResult(dividends, [], [], ibkrRates, []);
        var parser = MakeParserReturning(parseResult);

        var rateFetcher = Substitute.For<IExchangeRateFetcher>();
        rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), "CHF", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("UNSUPPORTED_CURRENCY", "CHF not in NBS")));
        // USD succeeds so the synthetic path IS tried
        rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 100m)));

        var filingRepo = Substitute.For<IFilingRepository>();
        var handler = MakeHandler(reportRepo, importerRepo, filingRepo, rateFetcher, parser);

        var result = await handler.HandleAsync(new ProcessReportsCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.FilingsCreated.Should().Be(0);
        result.Value.EventErrors.Should().HaveCount(1);
        result.Value.EventErrors[0].ErrorCode.Should().Be("INVALID_EXCHANGE_RATE");
    }

    /// <summary>
    /// CHF has no NBS rate and no IBKR cross-rate is available either.
    /// Expected: BuildRateProvider returns the original CHF RATE_NOT_FOUND error.
    /// </summary>
    [Fact]
    public async Task HandleAsync_CrossRate_NoIbkrRateAndNbsFails_AddsRateNotFoundError()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        // No IBKR rates at all
        var dividends = new[] { new DividendRecord(TestDate, "CHF", "Swiss Corp", 100m) };
        var parseResult = new StatementParseResult(dividends, [], [], [], []);
        var parser = MakeParserReturning(parseResult);

        var rateFetcher = Substitute.For<IExchangeRateFetcher>();
        // All fetches fail with RATE_NOT_FOUND (walk-back will exhaust and return RATE_NOT_FOUND)
        rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), "CHF", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("RATE_NOT_FOUND", "CHF unavailable")));

        var filingRepo = Substitute.For<IFilingRepository>();
        var handler = MakeHandler(reportRepo, importerRepo, filingRepo, rateFetcher, parser);

        var result = await handler.HandleAsync(new ProcessReportsCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.FilingsCreated.Should().Be(0);
        result.Value.EventErrors.Should().HaveCount(1);
        result.Value.EventErrors[0].ErrorCode.Should().Be("RATE_NOT_FOUND");
    }

    /// <summary>
    /// NBS rate for EUR is directly available. Cross-rate path is NOT entered.
    /// Verifies the happy-path short-circuits before touching IBKR rates.
    /// </summary>
    [Fact]
    public async Task HandleAsync_DirectNbsRateAvailable_DoesNotUseCrossRate()
    {
        var importer = MakeImporter();
        var report = MakeReportWithContent(importer.Id);

        var reportRepo = Substitute.For<IReportRepository>();
        reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>())
            .Returns(new[] { report });

        var importerRepo = Substitute.For<IImporterRepository>();
        importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var dividends = new[] { new DividendRecord(TestDate, "EUR", "Euro Corp", 100m) };
        // Provide an IBKR cross-rate that would yield a different value — it should be ignored
        var ibkrRates = new[] { new IbkrExchangeRate(TestDate, "EUR", "USD", 99m) };
        var parseResult = new StatementParseResult(dividends, [], [], ibkrRates, []);
        var parser = MakeParserReturning(parseResult);

        var rateFetcher = Substitute.For<IExchangeRateFetcher>();
        // EUR: NBS returns directly
        rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), "EUR", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "EUR", 117m)));

        var filingRepo = Substitute.For<IFilingRepository>();
        filingRepo.GetByIncomeEventAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Filing>());

        var handler = MakeHandler(reportRepo, importerRepo, filingRepo, rateFetcher, parser);

        var result = await handler.HandleAsync(new ProcessReportsCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.FilingsCreated.Should().Be(1);
        result.Value.EventErrors.Should().BeEmpty();
        // USD should never have been fetched — NBS EUR succeeded immediately
        await rateFetcher.DidNotReceive().FetchRateAsync(Arg.Any<DateOnly>(), "USD", Arg.Any<CancellationToken>());
    }
}
