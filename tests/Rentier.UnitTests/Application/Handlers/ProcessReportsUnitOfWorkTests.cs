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
using Rentier.Tests.Common.Fakes;
using Xunit;

namespace Rentier.UnitTests.Application.Handlers;

/// <summary>
/// Transaction-boundary behaviour of <see cref="ProcessReportsCommandHandler"/>: a report's
/// filings and its status change are staged in one unit of work and committed exactly once.
/// </summary>
public class ProcessReportsUnitOfWorkTests
{
    private static readonly DateOnly TEST_DATE = new(2024, 6, 17);
    private const decimal USD_RATE = 117m;

    private readonly Guid _profileId = Guid.NewGuid();
    private readonly IReportRepository _reportRepo = Substitute.For<IReportRepository>();
    private readonly IFilingRepository _filingRepo = Substitute.For<IFilingRepository>();
    private readonly IImporterRepository _importerRepo = Substitute.For<IImporterRepository>();
    private readonly IExchangeRateFetcher _rateFetcher = Substitute.For<IExchangeRateFetcher>();
    private readonly IStatementParser _parser = Substitute.For<IStatementParser>();
    private readonly FakeUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ProcessReportsCommandHandler _sut;

    public ProcessReportsUnitOfWorkTests()
    {
        _unitOfWorkFactory = new FakeUnitOfWorkFactory(_reportRepo, _filingRepo);

        _filingRepo.GetByIncomeEventAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Filing>());
        _rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TEST_DATE, "USD", USD_RATE)));

        var holidayRepo = Substitute.For<IHolidayRepository>();
        holidayRepo.GetHolidayConfAsync(Arg.Any<CancellationToken>())
            .Returns(new HolidayConfDto([], 2024, 2024));

        _sut = new ProcessReportsCommandHandler(
            _reportRepo,
            _importerRepo,
            _unitOfWorkFactory,
            new ExchangeRateResolver(_rateFetcher, NullLogger<ExchangeRateResolver>.Instance),
            holidayRepo,
            _parser,
            NullLogger<ProcessReportsCommandHandler>.Instance);
    }

    /// <summary>Arranges <paramref name="reportCount"/> Init reports that all parse to <paramref name="dividends"/>.</summary>
    private IReadOnlyList<Report> ArrangeReports(int reportCount, params DividendRecord[] dividends)
    {
        var importer = Importer.Create("Test Importer");
        importer.UpdateDetails(new ImporterDetails("Test Importer", ReportType.IbkrCsv, _profileId, null, "", "", "", ""));
        _importerRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);

        var reports = Enumerable.Range(1, reportCount)
            .Select(i => Report.Create(importer.Id, $"statement{i}.csv", [1, 2, 3], null))
            .ToList();
        _reportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>()).Returns(reports);

        _parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Success(
                new StatementParseResult(dividends, [], [], [], [])));
        return reports;
    }

    private static DividendRecord Dividend(string entity, decimal amount = 100m)
        => new(TEST_DATE, "USD", entity, amount);

    private Task<Result<ProcessReportsResult, Error>> ActAsync()
        => _sut.HandleAsync(new ProcessReportsCommand(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task HandleAsync_ReportWithSeveralFilings_CommitsExactlyOnce()
    {
        ArrangeReports(1, Dividend("ACME Corp"), Dividend("Globex"));

        await ActAsync();

        _unitOfWorkFactory.CommittedCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_SeveralReports_UsesOneUnitOfWorkPerReport()
    {
        ArrangeReports(3, Dividend("ACME Corp"));

        await ActAsync();

        _unitOfWorkFactory.CommittedCount.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_SeveralReports_DisposesEveryUnitOfWork()
    {
        ArrangeReports(3, Dividend("ACME Corp"));

        await ActAsync();

        _unitOfWorkFactory.DisposedCount.Should().Be(_unitOfWorkFactory.CreatedCount);
    }

    [Fact]
    public async Task HandleAsync_CommitFails_ReportsNoFilingsCreated()
    {
        ArrangeReports(1, Dividend("ACME Corp"), Dividend("Globex"));
        _unitOfWorkFactory.SaveFailure = ordinal => ordinal == 1 ? new InvalidOperationException("disk full") : null;

        var result = await ActAsync();

        result.Value.FilingsCreated.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_CommitFails_CountsReportAsErroredOnly()
    {
        ArrangeReports(1, Dividend("ACME Corp"));
        _unitOfWorkFactory.SaveFailure = ordinal => ordinal == 1 ? new InvalidOperationException("disk full") : null;

        var result = await ActAsync();

        (result.Value.ReportsProcessed, result.Value.ReportsErrored).Should().Be((0, 1));
    }

    [Fact]
    public async Task HandleAsync_CommitFailsForFirstReport_StillCommitsSecondReport()
    {
        ArrangeReports(2, Dividend("ACME Corp"));
        _unitOfWorkFactory.SaveFailure = ordinal => ordinal == 1 ? new InvalidOperationException("disk full") : null;

        var result = await ActAsync();

        result.Value.ReportsProcessed.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_CancelledMidReport_CommitsNothing()
    {
        ArrangeReports(1, Dividend("ACME Corp"), Dividend("Globex"));
        var calls = 0;
        _rateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++calls == 1
                ? Result<ExchangeRate, Error>.Success(new ExchangeRate(TEST_DATE, "USD", USD_RATE))
                : throw new OperationCanceledException());

        var act = ActAsync;

        await act.Should().ThrowAsync<OperationCanceledException>();
        _unitOfWorkFactory.CommittedCount.Should().Be(0);
    }

    /// <summary>
    /// Makes the substitute honour the unit-of-work repository contract that
    /// <see cref="IFilingRepository.GetByIncomeEventAsync"/> includes filings staged earlier in
    /// the same unit of work (verified against EF Core in <c>EfUnitOfWorkTests</c>).
    /// </summary>
    private void ArrangeFilingRepositoryReturnsStagedFilings()
    {
        var staged = new List<Filing>();
        _filingRepo.AddAsync(Arg.Do<Filing>(staged.Add), Arg.Any<CancellationToken>());
        _filingRepo.GetByIncomeEventAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => staged
                .Where(f => f.TaxpayerProfileId == call.ArgAt<Guid>(0)
                            && f.PayingEntity == call.ArgAt<string>(1)
                            && f.IncomeDate == call.ArgAt<DateOnly>(2))
                .ToList());
    }

    [Fact]
    public async Task HandleAsync_SameIncomeEventTwiceInOneReport_StagesSingleFiling()
    {
        ArrangeFilingRepositoryReturnsStagedFilings();
        ArrangeReports(1, Dividend("ACME Corp"), Dividend("ACME Corp"));

        await ActAsync();

        await _filingRepo.Received(1).AddAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CorrectedIncomeEventInSameReport_FlagsCorrection()
    {
        ArrangeFilingRepositoryReturnsStagedFilings();
        ArrangeReports(1, Dividend("ACME Corp", 100m), Dividend("ACME Corp", 120m));

        var result = await ActAsync();

        result.Value.EventErrors.Should().ContainSingle(e => e.ErrorCode == ErrorCodes.FILING_CORRECTION_DETECTED);
    }

    [Fact]
    public async Task HandleAsync_CommitFailsAfterSeveralEventsStaged_FailsWholeReportNotPartially()
    {
        // A storage failure surfaces at the single commit, so the report is all-or-nothing:
        // counted as errored with no filings — never PartialError with some filings persisted.
        ArrangeReports(1, Dividend("ACME Corp"), Dividend("Globex"));
        _unitOfWorkFactory.SaveFailure = ordinal => ordinal == 1 ? new InvalidOperationException("FK violation") : null;

        var result = await ActAsync();

        (result.Value.ReportsPartialError, result.Value.ReportsErrored, result.Value.FilingsCreated)
            .Should().Be((0, 1, 0));
    }

    // ── Unexpected failures leave the report at Init so the next run retries it ──

    [Fact]
    public async Task HandleAsync_CommitFails_WritesNothingFurtherForTheReport()
    {
        // No second unit of work: the report is not marked Error (that status is terminal);
        // its staged changes were discarded and it stays Init in the database.
        ArrangeReports(1, Dividend("ACME Corp"));
        _unitOfWorkFactory.SaveFailure = ordinal => ordinal == 1 ? new InvalidOperationException("disk full") : null;

        await ActAsync();

        _unitOfWorkFactory.CreatedCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_UnexpectedExceptionBeforeCommit_CommitsNothing()
    {
        ArrangeReports(1, Dividend("ACME Corp"));
        _parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<StatementParseResult, Error>>>(_ => throw new InvalidOperationException("boom"));

        await ActAsync();

        _unitOfWorkFactory.CommittedCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_UnexpectedExceptionBeforeCommit_DoesNotStageErrorStatus()
    {
        ArrangeReports(1, Dividend("ACME Corp"));
        _parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<StatementParseResult, Error>>>(_ => throw new InvalidOperationException("boom"));

        await ActAsync();

        await _reportRepo.DidNotReceive().UpdateStatusAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CommitFails_TellsUserReportWillBeRetried()
    {
        ArrangeReports(1, Dividend("ACME Corp"));
        _unitOfWorkFactory.SaveFailure = ordinal => ordinal == 1 ? new InvalidOperationException("disk full") : null;
        var reported = new List<SyncProgressEntry>();

        await _sut.HandleAsync(
            new ProcessReportsCommand(new SynchronousProgress(reported.Add)), TestContext.Current.CancellationToken);

        reported.Should().ContainSingle(e => e.Severity == SyncProgressSeverity.Error)
            .Which.Message.Should().Contain("retried on the next sync");
    }

    /// <summary>Progress sink that records entries synchronously (Progress&lt;T&gt; posts asynchronously).</summary>
    private sealed class SynchronousProgress(Action<SyncProgressEntry> onReport) : IProgress<SyncProgressEntry>
    {
        public void Report(SyncProgressEntry value) => onReport(value);
    }

    [Fact]
    public async Task HandleAsync_ProgressCallbackThrowsAfterCommit_DoesNotMarkCommittedReportErrored()
    {
        ArrangeReports(1, Dividend("ACME Corp"));
        var progress = new ThrowingProgress();

        try
        {
            await _sut.HandleAsync(new ProcessReportsCommand(progress), TestContext.Current.CancellationToken);
        }
        catch (InvalidOperationException)
        {
            // The callback's own exception is allowed to surface; what matters is below.
        }

        await _reportRepo.DidNotReceive().UpdateStatusAsync(
            Arg.Is<Report>(r => r.Status == ReportStatus.Error), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ProgressCallbackThrowsAfterCommit_SurfacesCallbackExceptionNotDomainException()
    {
        ArrangeReports(1, Dividend("ACME Corp"));

        var act = () => _sut.HandleAsync(
            new ProcessReportsCommand(new ThrowingProgress()), TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage(ThrowingProgress.MESSAGE);
    }

    /// <summary>Synchronous progress sink that fails on every report — a faulty UI callback.</summary>
    private sealed class ThrowingProgress : IProgress<SyncProgressEntry>
    {
        public const string MESSAGE = "progress sink failed";

        public void Report(SyncProgressEntry value) => throw new InvalidOperationException(MESSAGE);
    }
}
