using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Parsing;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Rentier.Tests.Common.Fakes;
using Xunit;

namespace Rentier.Scenarios.Tests;

/// <summary>
/// Regression for the missing transaction boundary in report processing: a statement with
/// two income events is interrupted after the first filing has been produced but before the
/// second one. Previously each filing was committed on its own, so the interruption left the
/// report at Init with one orphaned filing. A report's filings and status must now commit
/// together or not at all — over the real repositories and a migrated SQLite database.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReportProcessingAtomicityScenario : IAsyncLifetime
{
    private const decimal USD_RATE = 117.5432m;

    // One dividend (2024-01-15) followed by one credit-interest entry (2024-01-31).
    private static readonly byte[] TWO_EVENT_CSV_BYTES = Encoding.UTF8.GetBytes(
        "Dividends,Header,Currency,Date,Description,Amount\n" +
        "Dividends,Data,USD,2024-01-15,\"AAPL(US0378331005) Cash Dividend USD 0.24 per Share\",24.00\n" +
        "Dividends,Total,USD,,Total,24.00\n" +
        "Interest,Header,Currency,Date,Description,Amount\n" +
        "Interest,Data,USD,2024-01-31,Credit Interest for Jan-2024,5.42\n" +
        "Interest,Total,USD,,Total,5.42\n");

    private readonly ScenarioFixture _fixture = new();
    private Guid _reportId;

    public async ValueTask InitializeAsync()
    {
        await _fixture.InitializeAsync();
        var ct = CancellationToken.None;

        var profile = new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "Test User", "Test Address", "011");
        await _fixture.Get<ITaxpayerProfileRepository>().SaveAsync(profile, ct);

        var importer = Importer.Create("IBKR Test");
        importer.UpdateDetails(new ImporterDetails("IBKR Test", ReportType.IbkrCsv, profile.Id, null,
            string.Empty, string.Empty, string.Empty, string.Empty));
        await _fixture.Get<IImporterRepository>().AddAsync(importer, ct);

        var rates = _fixture.Get<IExchangeRateCacheRepository>();
        await rates.SaveAsync(new ExchangeRate(new DateOnly(2024, 1, 15), "USD", USD_RATE), ct);
        await rates.SaveAsync(new ExchangeRate(new DateOnly(2024, 1, 31), "USD", USD_RATE), ct);

        var report = Report.Create(importer.Id, "two_events.csv", TWO_EVENT_CSV_BYTES, null, TestClock.TODAY);
        await _fixture.Get<IReportRepository>().AddAsync(report, ct);
        _reportId = report.Id;
    }

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    /// <summary>Real pipeline, except the rate resolver aborts when the second income event is reached.</summary>
    private ProcessReportsCommandHandler MakeHandlerInterruptedAtSecondEvent() => new(
        _fixture.Get<IReportRepository>(),
        _fixture.Get<IImporterRepository>(),
        _fixture.Get<IUnitOfWorkFactory>(),
        new InterruptingRateResolver(_fixture.Get<IExchangeRateResolver>(), interruptOnCall: 2),
        _fixture.Get<IHolidayRepository>(),
        _fixture.Get<IStatementParser>(),
        NullLogger<ProcessReportsCommandHandler>.Instance,
        _fixture.Get<TimeProvider>());

    private async Task ProcessInterruptedAsync()
    {
        var act = () => MakeHandlerInterruptedAtSecondEvent()
            .HandleAsync(new ProcessReportsCommand(), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private Task<Result<ProcessReportsResult, Error>> ProcessNormallyAsync()
        => _fixture.Get<ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>>()
            .HandleAsync(new ProcessReportsCommand(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task ProcessingInterruptedMidReport_LeavesNoPartialFilings()
    {
        await ProcessInterruptedAsync();

        var filings = await _fixture.Get<IFilingRepository>().GetAllAsync(TestContext.Current.CancellationToken);
        filings.Should().BeEmpty(because: "the first event's filing must not be committed without the rest of the report");
    }

    [Fact]
    public async Task ProcessingInterruptedMidReport_LeavesReportStatusUnchanged()
    {
        await ProcessInterruptedAsync();

        var report = await _fixture.Get<IReportRepository>().GetByIdAsync(_reportId, TestContext.Current.CancellationToken);
        report!.Status.Should().Be(ReportStatus.Init);
    }

    [Fact]
    public async Task ReprocessingAfterInterruption_CreatesEachFilingExactlyOnce()
    {
        await ProcessInterruptedAsync();

        await ProcessNormallyAsync();

        var filings = await _fixture.Get<IFilingRepository>().GetAllAsync(TestContext.Current.CancellationToken);
        filings.Select(f => f.IncomeType).Should().BeEquivalentTo([IncomeType.Dividend, IncomeType.Interest]);
    }

    [Fact]
    public async Task ReprocessingAfterInterruption_MarksReportProcessed()
    {
        await ProcessInterruptedAsync();

        await ProcessNormallyAsync();

        var report = await _fixture.Get<IReportRepository>().GetByIdAsync(_reportId, TestContext.Current.CancellationToken);
        report!.Status.Should().Be(ReportStatus.Processed);
    }

    // ── Unexpected failure: the report stays Init and the next run retries it ──

    private Task<Result<ProcessReportsResult, Error>> ProcessWithFailingParserAsync() =>
        new ProcessReportsCommandHandler(
                _fixture.Get<IReportRepository>(),
                _fixture.Get<IImporterRepository>(),
                _fixture.Get<IUnitOfWorkFactory>(),
                _fixture.Get<IExchangeRateResolver>(),
                _fixture.Get<IHolidayRepository>(),
                new FailingStatementParser(),
                NullLogger<ProcessReportsCommandHandler>.Instance,
                _fixture.Get<TimeProvider>())
            .HandleAsync(new ProcessReportsCommand(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task ProcessingFailsUnexpectedly_LeavesReportAtInitForRetry()
    {
        await ProcessWithFailingParserAsync();

        var report = await _fixture.Get<IReportRepository>().GetByIdAsync(_reportId, TestContext.Current.CancellationToken);
        report!.Status.Should().Be(ReportStatus.Init);
    }

    [Fact]
    public async Task NextRunAfterUnexpectedFailure_MarksReportProcessed()
    {
        await ProcessWithFailingParserAsync();

        await ProcessNormallyAsync();

        var report = await _fixture.Get<IReportRepository>().GetByIdAsync(_reportId, TestContext.Current.CancellationToken);
        report!.Status.Should().Be(ReportStatus.Processed);
    }

    [Fact]
    public async Task ProcessingFailsOnEveryRun_MarksReportErrorAfterAttemptLimit()
    {
        for (var i = 0; i < ProcessReportsCommandHandler.MAX_PROCESSING_ATTEMPTS; i++)
            await ProcessWithFailingParserAsync();

        var report = await _fixture.Get<IReportRepository>().GetByIdAsync(_reportId, TestContext.Current.CancellationToken);
        report!.Status.Should().Be(ReportStatus.Error);
    }

    [Fact]
    public async Task ProcessingFailsOnEveryRun_PersistsFailureReason()
    {
        for (var i = 0; i < ProcessReportsCommandHandler.MAX_PROCESSING_ATTEMPTS; i++)
            await ProcessWithFailingParserAsync();

        var report = await _fixture.Get<IReportRepository>().GetByIdAsync(_reportId, TestContext.Current.CancellationToken);
        report!.LastProcessingError.Should().Contain("Simulated storage failure");
    }

    [Fact]
    public async Task ProcessingFailsOnce_PersistsOneAttempt()
    {
        await ProcessWithFailingParserAsync();

        var report = await _fixture.Get<IReportRepository>().GetByIdAsync(_reportId, TestContext.Current.CancellationToken);
        report!.ProcessingAttempts.Should().Be(1);
    }

    /// <summary>Simulates an unexpected infrastructure failure (it throws rather than returning a Failure).</summary>
    private sealed class FailingStatementParser : IStatementParser
    {
        public Task<Result<StatementParseResult, Error>> ParseAsync(Stream stream, CancellationToken ct = default)
            => throw new IOException("Simulated storage failure while reading the statement.");
    }

    private sealed class InterruptingRateResolver(IExchangeRateResolver inner, int interruptOnCall) : IExchangeRateResolver
    {
        private int _calls;

        public Task<Result<RateResolution, Error>> ResolveAsync(
            DateOnly date, string currency, HolidayConf holidays, int maxLookbackDays = 10, CancellationToken ct = default)
        {
            if (++_calls >= interruptOnCall)
                throw new OperationCanceledException("Simulated interruption mid-report.");

            return inner.ResolveAsync(date, currency, holidays, maxLookbackDays, ct);
        }
    }
}
