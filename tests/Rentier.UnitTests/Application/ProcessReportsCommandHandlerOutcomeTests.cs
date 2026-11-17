using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Parsing;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;
using Rentier.Tests.Common.Fakes;
using Xunit;

namespace Rentier.UnitTests.Application;

/// <summary>
/// Characterisation tests pinning the per-report outcome handling of
/// <see cref="Rentier.Application.Handlers.ProcessReportsCommandHandler"/>: persisted status,
/// counters, progress messages and <see cref="ReportProcessingDetail"/> for the rejected,
/// completed and unexpected-exception paths.
/// </summary>
/// <remarks>
/// Written against the handler before the unit of work (#159) and re-targeted to it: the
/// rejected and completed paths are unchanged; an unexpected exception now leaves the report
/// pending for retry (Warning) instead of committing it as Error, as #159 intended.
/// </remarks>
public partial class ProcessReportsCommandHandlerTests
{
    /// <summary>Synchronous progress sink — <see cref="Progress{T}"/> posts asynchronously.</summary>
    private sealed class RecordingProgress : IProgress<SyncProgressEntry>
    {
        public List<SyncProgressEntry> Entries { get; } = [];

        public void Report(SyncProgressEntry value) => Entries.Add(value);
    }

    private sealed class OutcomeFixture
    {
        public IReportRepository ReportRepo { get; } = Substitute.For<IReportRepository>();
        public IImporterRepository ImporterRepo { get; } = Substitute.For<IImporterRepository>();
        public IFilingRepository FilingRepo { get; } = Substitute.For<IFilingRepository>();
        public IExchangeRateFetcher RateFetcher { get; } = Substitute.For<IExchangeRateFetcher>();
        public IStatementParser Parser { get; } = Substitute.For<IStatementParser>();
        public RecordingProgress Progress { get; } = new();

        public FakeUnitOfWorkFactory UnitOfWorkFactory { get; }

        /// <summary>Report statuses in the order they were handed to <c>UpdateProcessingStateAsync</c>.</summary>
        public List<(string Name, ReportStatus Status)> Persisted { get; } = [];

        /// <summary>The "database" copy of each report, served to the failed-attempt re-read.</summary>
        public Dictionary<Guid, Report> Stored { get; } = [];

        public OutcomeFixture(Importer importer, params Report[] reports)
        {
            UnitOfWorkFactory = new FakeUnitOfWorkFactory(ReportRepo, FilingRepo);
            ReportRepo.GetByStatusAsync(ReportStatus.Init, Arg.Any<CancellationToken>()).Returns(reports);
            ReportRepo.UpdateProcessingStateAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask)
                .AndDoes(ci => Persisted.Add((ci.Arg<Report>().ReportName, ci.Arg<Report>().Status)));
            foreach (var report in reports)
            {
                var stored = StoredCopyOf(report);
                Stored[report.Id] = stored;
                ReportRepo.GetByIdAsync(report.Id, Arg.Any<CancellationToken>()).Returns(stored);
            }
            ImporterRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>()).Returns(importer);
            FilingRepo.GetByIncomeEventAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(),
                    Arg.Any<CancellationToken>())
                .Returns(Array.Empty<Filing>());

            // USD resolves at 117; every other currency has no rate.
            RateFetcher.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ci => ci.ArgAt<string>(1) == "USD"
                    ? Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 117m))
                    : Result<ExchangeRate, Error>.Failure(new Error("RATE_NOT_FOUND", "Rate not found")));
        }

        /// <summary>The caller's token for the run, linked to the test's own token.</summary>
        public CancellationTokenSource Caller { get; } =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        /// <summary>Simulates the caller cancelling mid-run: cancels its token and returns the exception to throw.</summary>
        public OperationCanceledException CancelCaller()
        {
            Caller.Cancel();
            return new OperationCanceledException(Caller.Token);
        }

        public void ParserReturns(params DividendRecord[] dividends)
            => Parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(Result<StatementParseResult, Error>.Success(
                    new StatementParseResult(dividends, [], [], [], [])));

        /// <summary>A shallow copy of <paramref name="report"/> as the database would hold it.</summary>
        private static Report StoredCopyOf(Report report)
            => (Report)typeof(object)
                .GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(report, null)!;
    }

    private Task<Result<ProcessReportsResult, Error>> RunAsync(OutcomeFixture f)
        => MakeHandler(
                reportRepo: f.ReportRepo, importerRepo: f.ImporterRepo, filingRepo: f.FilingRepo,
                rateFetcher: f.RateFetcher, parser: f.Parser, unitOfWorkFactory: f.UnitOfWorkFactory)
            .HandleAsync(new ProcessReportsCommand(f.Progress), f.Caller.Token);

    [Fact]
    public async Task HandleAsync_RejectedReport_PersistsErrorAndReportsRejectionMessage()
    {
        var importer = MakeImporter();
        var f = new OutcomeFixture(importer, Report.Create(importer.Id, "empty.csv", null, null, TestClock.TODAY));

        var result = await RunAsync(f);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new ProcessReportsResult(
            0, 0, 1, [], 0,
            [new ReportProcessingDetail("empty.csv", 0, 0, SyncProgressSeverity.Error)], 0, []));
        f.Persisted.Should().Equal(("empty.csv", ReportStatus.Error));
        f.Progress.Entries.Should().ContainSingle();
        f.Progress.Entries[0].Message.Should().Be(
            "Report 'empty.csv': processing error — Report has no attachment content.");
        f.Progress.Entries[0].Severity.Should().Be(SyncProgressSeverity.Error);
    }

    [Fact]
    public async Task HandleAsync_ParseFailure_ReportsParserMessage()
    {
        var importer = MakeImporter();
        var f = new OutcomeFixture(importer, MakeReportWithContent(importer.Id));
        f.Parser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<StatementParseResult, Error>.Failure(new Error("INVALID_FORMAT", "bad csv")));

        var result = await RunAsync(f);

        result.Value.ReportsErrored.Should().Be(1);
        f.Persisted.Should().Equal(("statement.csv", ReportStatus.Error));
        f.Progress.Entries.Should().ContainSingle().Which.Message.Should().Be(
            "Report 'statement.csv': processing error — bad csv.");
    }

    [Fact]
    public async Task HandleAsync_AllEventsSucceed_PersistsProcessedAndReportsInfo()
    {
        var importer = MakeImporter();
        var f = new OutcomeFixture(importer, MakeReportWithContent(importer.Id));
        f.ParserReturns(new DividendRecord(TestDate, "USD", "ACME Corp", 100m));

        var result = await RunAsync(f);

        result.Value.Should().BeEquivalentTo(new ProcessReportsResult(
            1, 1, 0, [], 0,
            [new ReportProcessingDetail("statement.csv", 1, 0, SyncProgressSeverity.Info)], 0, []));
        f.Persisted.Should().Equal(("statement.csv", ReportStatus.Processed));
        f.Progress.Entries.Should().ContainSingle();
        f.Progress.Entries[0].Message.Should().Be("Report 'statement.csv': 1 filing(s) created, 0 failed.");
        f.Progress.Entries[0].Severity.Should().Be(SyncProgressSeverity.Info);
    }

    [Fact]
    public async Task HandleAsync_SomeEventsFail_PersistsPartialErrorAndReportsWarning()
    {
        var importer = MakeImporter();
        var f = new OutcomeFixture(importer, MakeReportWithContent(importer.Id));
        f.ParserReturns(
            new DividendRecord(TestDate, "USD", "ACME Corp", 100m),
            new DividendRecord(TestDate, "CHF", "Swiss Co", 50m));

        var result = await RunAsync(f);

        result.Value.FilingsCreated.Should().Be(1);
        result.Value.ReportsProcessed.Should().Be(0);
        result.Value.ReportsPartialError.Should().Be(1);
        result.Value.ReportsErrored.Should().Be(0);
        result.Value.EventErrors.Should().ContainSingle().Which.ErrorCode.Should().Be("RATE_NOT_FOUND");
        result.Value.ReportDetails.Should().Equal(
            new ReportProcessingDetail("statement.csv", 1, 1, SyncProgressSeverity.Warning));
        f.Persisted.Should().Equal(("statement.csv", ReportStatus.PartialError));
        f.Progress.Entries.Should().ContainSingle();
        f.Progress.Entries[0].Message.Should().Be("Report 'statement.csv': 1 filing(s) created, 1 failed.");
        f.Progress.Entries[0].Severity.Should().Be(SyncProgressSeverity.Warning);
    }

    [Fact]
    public async Task HandleAsync_AllEventsFail_PersistsErrorAndReportsCounts()
    {
        var importer = MakeImporter();
        var f = new OutcomeFixture(importer, MakeReportWithContent(importer.Id));
        f.ParserReturns(new DividendRecord(TestDate, "CHF", "Swiss Co", 50m));

        var result = await RunAsync(f);

        result.Value.FilingsCreated.Should().Be(0);
        result.Value.ReportsProcessed.Should().Be(0);
        result.Value.ReportsPartialError.Should().Be(0);
        result.Value.ReportsErrored.Should().Be(1);
        result.Value.ReportDetails.Should().Equal(
            new ReportProcessingDetail("statement.csv", 0, 1, SyncProgressSeverity.Error));
        f.Persisted.Should().Equal(("statement.csv", ReportStatus.Error));
        f.Progress.Entries.Should().ContainSingle();
        f.Progress.Entries[0].Message.Should().Be("Report 'statement.csv': 0 filing(s) created, 1 failed.");
        f.Progress.Entries[0].Severity.Should().Be(SyncProgressSeverity.Error);
    }

    [Fact]
    public async Task HandleAsync_IdempotentSkip_CountsAsProcessedWithZeroCreated()
    {
        var importer = MakeImporter();
        var f = new OutcomeFixture(importer, MakeReportWithContent(importer.Id));
        f.ParserReturns(new DividendRecord(TestDate, "USD", "ACME Corp", 100m));
        f.FilingRepo.GetByIncomeEventAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>())
            .Returns(new[] { MakeExistingFiling("ACME Corp", 11700m) });

        var result = await RunAsync(f);

        result.Value.FilingsCreated.Should().Be(0);
        result.Value.ReportsProcessed.Should().Be(1);
        result.Value.ReportDetails.Should().Equal(
            new ReportProcessingDetail("statement.csv", 0, 0, SyncProgressSeverity.Info));
        f.Persisted.Should().Equal(("statement.csv", ReportStatus.Processed));
    }

    [Fact]
    public async Task HandleAsync_EventThrowsDomainException_RecordsDomainErrorForThatEventAndContinues()
    {
        var importer = MakeImporter();
        var f = new OutcomeFixture(importer, MakeReportWithContent(importer.Id));
        f.ParserReturns(
            new DividendRecord(TestDate, "USD", "Broken Co", 100m),
            new DividendRecord(TestDate, "USD", "ACME Corp", 100m));
        f.FilingRepo.AddAsync(Arg.Is<Filing>(x => x.PayingEntity == "Broken Co"), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("insert failed"));

        var result = await RunAsync(f);

        result.Value.FilingsCreated.Should().Be(1);
        result.Value.ReportsPartialError.Should().Be(1);
        var error = result.Value.EventErrors.Should().ContainSingle().Subject;
        error.EntityName.Should().Be("Broken Co");
        error.ErrorCode.Should().Be(ErrorCodes.DOMAIN_ERROR);
        error.Message.Should().Be("insert failed");
        f.Persisted.Should().Equal(("statement.csv", ReportStatus.PartialError));
    }

    [Fact]
    public async Task HandleAsync_UnexpectedException_KeepsReportPendingReportsRetryAndContinuesWithNextReport()
    {
        var importer = MakeImporter();
        var brokenImporterId = Guid.NewGuid();
        var broken = Report.Create(brokenImporterId, "broken.csv", [1, 2, 3], null, TestClock.TODAY);
        var good = Report.Create(importer.Id, "good.csv", [1, 2, 3], null, TestClock.TODAY);
        var f = new OutcomeFixture(importer, broken, good);
        f.ImporterRepo.GetByIdAsync(brokenImporterId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("db down"));
        f.ParserReturns(new DividendRecord(TestDate, "USD", "ACME Corp", 100m));

        var result = await RunAsync(f);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(
            new ProcessReportsResult(
                1, 1, 0, [], 0,
                [
                    new ReportProcessingDetail("broken.csv", 0, 0, SyncProgressSeverity.Warning),
                    new ReportProcessingDetail("good.csv", 1, 0, SyncProgressSeverity.Info),
                ],
                1,
                [new ReportFailedAttempt(broken.Id, "broken.csv", "db down", 1, FailedAttemptKind.PendingRetry)]),
            o => o.WithStrictOrdering());
        f.Persisted.Should().Equal(("broken.csv", ReportStatus.Init), ("good.csv", ReportStatus.Processed));
        f.Progress.Entries.Select(e => (e.Message, e.Severity)).Should().Equal(
            ("Report 'broken.csv': processing failed (attempt 1) — db down. Nothing was saved; it will be retried on the next sync.",
                SyncProgressSeverity.Warning),
            ("Report 'good.csv': 1 filing(s) created, 0 failed.", SyncProgressSeverity.Info));
    }

    [Fact]
    public async Task HandleAsync_CancellationDuringReport_PropagatesWithoutPersistingStatus()
    {
        var importer = MakeImporter();
        var f = new OutcomeFixture(importer, MakeReportWithContent(importer.Id));
        f.ImporterRepo.GetByIdAsync(importer.Id, Arg.Any<CancellationToken>())
            .ThrowsAsync(_ => f.CancelCaller());

        var act = () => RunAsync(f);

        await act.Should().ThrowAsync<OperationCanceledException>();
        f.Persisted.Should().BeEmpty();
        f.Progress.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_CancellationDuringEvent_Propagates()
    {
        var importer = MakeImporter();
        var f = new OutcomeFixture(importer, MakeReportWithContent(importer.Id));
        f.ParserReturns(new DividendRecord(TestDate, "USD", "ACME Corp", 100m));
        f.FilingRepo.AddAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(_ => f.CancelCaller());

        var act = () => RunAsync(f);

        await act.Should().ThrowAsync<OperationCanceledException>();
        f.Persisted.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_InternalCancellationWithoutCallerCancelling_KeepsReportPendingAndContinues()
    {
        // An OperationCanceledException the caller did not ask for (e.g. an internal timeout) is an
        // unexpected failure of that report, not a reason to abandon the remaining reports.
        var importer = MakeImporter();
        var brokenImporterId = Guid.NewGuid();
        var broken = Report.Create(brokenImporterId, "broken.csv", [1, 2, 3], null, TestClock.TODAY);
        var good = Report.Create(importer.Id, "good.csv", [1, 2, 3], null, TestClock.TODAY);
        var f = new OutcomeFixture(importer, broken, good);
        f.ImporterRepo.GetByIdAsync(brokenImporterId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("timed out"));
        f.ParserReturns(new DividendRecord(TestDate, "USD", "ACME Corp", 100m));

        var result = await RunAsync(f);

        (result.Value.ReportsPendingRetry, result.Value.ReportsProcessed).Should().Be((1, 1));
    }

    // ── Rejection commit failures ────────────────────────────────────────────
    // Saving a rejection can fail like any commit. The rejection is the cause, so it — not the
    // storage error — must be what the failed attempt records and what the user is told.

    private OutcomeFixture RejectionWhoseCommitFails()
    {
        var importer = MakeImporter();
        var f = new OutcomeFixture(importer, Report.Create(importer.Id, "empty.csv", null, null, TestClock.TODAY));
        // Unit of work #1 commits the rejection (fails); #2 records the failed attempt.
        f.UnitOfWorkFactory.SaveFailure = ordinal => ordinal == 1 ? new InvalidOperationException("disk full") : null;
        return f;
    }

    [Fact]
    public async Task HandleAsync_RejectionCommitFails_RecordsRejectionReasonNotStorageError()
    {
        var f = RejectionWhoseCommitFails();

        await RunAsync(f);

        f.Stored.Values.Single().LastProcessingError.Should().Be("Report has no attachment content");
    }

    [Fact]
    public async Task HandleAsync_RejectionCommitFails_ReportsRejectionReasonInRetryWarning()
    {
        var f = RejectionWhoseCommitFails();

        await RunAsync(f);

        f.Progress.Entries.Select(e => (e.Message, e.Severity)).Should().Equal(
            ("Report 'empty.csv': processing failed (attempt 1) — Report has no attachment content. " +
                "Nothing was saved; it will be retried on the next sync.",
                SyncProgressSeverity.Warning));
    }

    [Fact]
    public async Task HandleAsync_RejectionCommitFails_CountsReportAsPendingRetryNotErrored()
    {
        var f = RejectionWhoseCommitFails();

        var result = await RunAsync(f);

        (result.Value.ReportsErrored, result.Value.ReportsPendingRetry).Should().Be((0, 1));
    }

    [Fact]
    public async Task HandleAsync_CancellationWhileCommittingRejection_Propagates()
    {
        var f = RejectionWhoseCommitFails();
        f.UnitOfWorkFactory.SaveFailure = _ => f.CancelCaller();

        var act = () => RunAsync(f);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
