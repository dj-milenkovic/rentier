using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Filings;
using Rentier.Application.Interfaces;
using Rentier.Application.Parsing;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;
using Rentier.Application.Logging;

namespace Rentier.Application.Handlers;

public sealed class ProcessReportsCommandHandler
    : ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>
{
    private readonly IReportRepository _reportRepository;
    private readonly IImporterRepository _importerRepository;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IncomeEventFiler _filer;
    private readonly IHolidayRepository _holidayRepository;
    private readonly IStatementParser _statementParser;
    private readonly ILogger<ProcessReportsCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters",
        Justification = "DI-resolved handler constructor; each parameter is a distinct injected collaborator.")]
    public ProcessReportsCommandHandler(
        IReportRepository reportRepository,
        IImporterRepository importerRepository,
        IUnitOfWorkFactory unitOfWorkFactory,
        IExchangeRateResolver exchangeRateResolver,
        IHolidayRepository holidayRepository,
        IStatementParser statementParser,
        ILogger<ProcessReportsCommandHandler> logger,
        TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _reportRepository = reportRepository;
        _importerRepository = importerRepository;
        _unitOfWorkFactory = unitOfWorkFactory;
        _filer = new IncomeEventFiler(exchangeRateResolver);
        _holidayRepository = holidayRepository;
        _statementParser = statementParser;
        _logger = logger;
    }

    public async Task<Result<ProcessReportsResult, Error>> HandleAsync(
        ProcessReportsCommand command, CancellationToken ct = default)
    {
        var holidays = await _holidayRepository.GetHolidayCalendarAsync(ct);

        var initReports = await _reportRepository.GetByStatusAsync(ReportStatus.Init, ct);

        var tally = new ProcessingTally();

        foreach (var report in initReports)
        {
            ct.ThrowIfCancellationRequested();

            // Only processing and the commit sit inside the try: once the report is committed
            // its outcome is final, so nothing after that point may route it to the
            // failed-attempt path (which would count it twice).
            ReportOutcome outcome;
            try
            {
                outcome = await ProcessAndCommitReportAsync(report, holidays, ct);
            }
            // Only the caller's cancellation stops the run. An OperationCanceledException without
            // it (e.g. an internal timeout) is an unexpected failure of this report, handled below.
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            // Per-report isolation boundary: one report failing unexpectedly (DB error, a failed
            // commit, a bug) must not abort the remaining reports. The catch only classifies —
            // all I/O for the outcome happens in RecordOutcomeAsync, which never throws.
            catch (Exception ex)
            {
                // The report's unit of work is already disposed, so everything it staged
                // (filings, status) was discarded.
                // The exception is attached (the log renders it safely); its message is never put in the template.
                _logger.ReportFailedUnexpectedly(ex, report.Id);
                outcome = new ReportOutcome.AttemptFailed(ex.Message);
            }

            await RecordOutcomeAsync(report, outcome, tally, command.Progress, ct);
        }

        return Result<ProcessReportsResult, Error>.Success(
            new ProcessReportsResult(tally.FilingsCreated, tally.ReportsProcessed, tally.ReportsErrored,
                tally.EventErrors, tally.ReportsPartialError, tally.ReportDetails, tally.ReportsPendingRetry,
                tally.FailedAttempts));
    }

    /// <summary>
    /// The single place where a report's outcome becomes observable: records a failed attempt
    /// when nothing was committed, then updates the run counters, logs, reports progress and adds
    /// the <see cref="ReportProcessingDetail"/>. Expected failures never escape it, so one report
    /// cannot stop the remaining reports; it throws only on cancellation, if the caller's progress
    /// callback throws, or on an unhandled <see cref="ReportOutcome"/> subtype (a programming error).
    /// </summary>
    private async Task RecordOutcomeAsync(
        Report report,
        ReportOutcome outcome,
        ProcessingTally tally,
        IProgress<SyncProgressEntry>? progress,
        CancellationToken ct)
    {
        string message;
        SyncProgressSeverity severity;
        var created = 0;
        var failed = 0;

        switch (outcome)
        {
            case ReportOutcome.AttemptFailed failedAttempt:
                {
                    // Nothing was persisted. Record the attempt so the failure is visible and
                    // bounded: the report stays Init and is retried by the next run, until
                    // MAX_PROCESSING_ATTEMPTS failures move it to Error for manual attention.
                    var attempt = await RecordFailedAttemptAsync(report, failedAttempt.Reason, ct);
                    tally.Count(attempt);
                    severity = attempt.Kind == FailedAttemptKind.Exhausted
                        ? SyncProgressSeverity.Error
                        : SyncProgressSeverity.Warning;
                    message = attempt.ToLogMessage();
                    break;
                }

            // ── Committed outcomes: bookkeeping only from here on ──
            case ReportOutcome.Rejected rejected:
                // Data integrity failure (no attachment, missing importer, parse error) — not an unexpected exception
                tally.Count(ReportStatus.Error);
                // Code only: the message can quote parsed statement content (amounts, tickers).
                _logger.ReportRejected(report.Id, rejected.Error.Code);
                severity = SyncProgressSeverity.Error;
                message = $"Report '{report.ReportName}': processing error — {rejected.Error.Message}.";
                break;

            case ReportOutcome.Committed committed:
                tally.FilingsCreated += committed.Created;
                tally.EventErrors.AddRange(committed.EventErrors);
                tally.Count(committed.Status);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.ReportCommitted(
                        report.Id, committed.Succeeded + committed.Failed, committed.Created, committed.Failed,
                        committed.Status);
                }

                created = committed.Created;
                failed = committed.Failed;
                severity = ReportProcessingDetail.ClassifySeverity(created, failed);
                message = new ReportProcessingDetail(report.ReportName, created, failed, severity).ToLogMessage();
                break;

            default:
                throw new System.Diagnostics.UnreachableException(
                    $"Unhandled report outcome {outcome.GetType().Name}.");
        }

        progress?.Report(new SyncProgressEntry(_timeProvider.GetLocalNow(), message, severity));
        tally.ReportDetails.Add(new ReportProcessingDetail(report.ReportName, created, failed, severity));
    }

    /// <summary>Running totals for one <see cref="HandleAsync"/> invocation.</summary>
    private sealed class ProcessingTally
    {
        public int FilingsCreated { get; set; }
        public int ReportsProcessed { get; private set; }
        public int ReportsErrored { get; private set; }
        public int ReportsPartialError { get; private set; }
        public int ReportsPendingRetry { get; private set; }
        public List<FilingCreationError> EventErrors { get; } = [];
        public List<ReportProcessingDetail> ReportDetails { get; } = [];
        public List<ReportFailedAttempt> FailedAttempts { get; } = [];

        /// <summary>Counts a committed report by its final status.</summary>
        public void Count(ReportStatus status)
        {
            switch (status)
            {
                case ReportStatus.Processed:
                    ReportsProcessed++;
                    break;
                case ReportStatus.PartialError:
                    ReportsPartialError++;
                    break;
                default:
                    ReportsErrored++;
                    break;
            }
        }

        /// <summary>Counts a report whose attempt failed without committing anything.</summary>
        public void Count(ReportFailedAttempt attempt)
        {
            switch (attempt.Kind)
            {
                case FailedAttemptKind.PendingRetry:
                case FailedAttemptKind.NotRecorded:
                    ReportsPendingRetry++;
                    break;
                case FailedAttemptKind.Exhausted:
                    ReportsErrored++;
                    break;
            }

            if (attempt.Kind != FailedAttemptKind.ReportGone)
                FailedAttempts.Add(attempt);
        }
    }

    /// <summary>
    /// Failed attempts (that persisted nothing) a report may accumulate before it is moved to
    /// Error and no longer retried automatically.
    /// </summary>
    public const int MAX_PROCESSING_ATTEMPTS = 3;

    /// <summary>
    /// Processes one report inside its own unit of work and commits its filings and its new
    /// status together with a single SaveChangesAsync, so a failure or cancellation part-way
    /// through leaves the report at Init with no filings.
    /// </summary>
    /// <remarks>
    /// The status is applied to the in-memory <paramref name="report"/> before the commit. If the
    /// commit fails, that instance is discarded (never reused or re-saved): the failed-attempt
    /// path re-reads the persisted report instead.
    /// </remarks>
    private async Task<ReportOutcome> ProcessAndCommitReportAsync(Report report, HolidayConf holidays, CancellationToken ct)
    {
        await using var unitOfWork = _unitOfWorkFactory.Create();

        var processing = await ProcessReportAsync(report, holidays, unitOfWork.Filings, ct);

        if (processing is ReportProcessing.Deferred deferred)
        {
            // Not committed: the staged filings are discarded with the unit of work.
            return new ReportOutcome.AttemptFailed(deferred.Reason);
        }

        if (processing is ReportProcessing.Rejected rejection)
        {
            report.Reject(rejection.Error.Message);
            try
            {
                await unitOfWork.Reports.UpdateProcessingStateAsync(report, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }
            // Repositories do not return Result, so a failure to save the rejection is translated
            // here: the rejection — not the storage error — stays the reason recorded on the
            // failed attempt, so the cause is not masked. Cancellation propagates.
            catch (Exception persistEx) when (persistEx is not OperationCanceledException)
            {
                _logger.ReportRejectionNotSaved(persistEx, report.Id, rejection.Error.Code);
                return new ReportOutcome.AttemptFailed(rejection.Error.Message);
            }

            return new ReportOutcome.Rejected(rejection.Error);
        }

        var (created, succeeded, failed, eventErrors) = (ReportProcessing.Completed)processing;

        ReportStatus status;
        if (failed == 0)
            status = ReportStatus.Processed;
        else if (succeeded > 0)
            status = ReportStatus.PartialError;
        else
            status = ReportStatus.Error;

        report.SetStatus(status);
        await unitOfWork.Reports.UpdateProcessingStateAsync(report, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ReportOutcome.Committed(status, created, succeeded, failed, eventErrors);
    }

    /// <summary>
    /// Records a failed attempt on the persisted report in a fresh unit of work. Never throws
    /// (except on cancellation), so one report's failure cannot stop the remaining reports.
    /// </summary>
    private async Task<ReportFailedAttempt> RecordFailedAttemptAsync(Report report, string reason, CancellationToken ct)
    {
        try
        {
            await using var unitOfWork = _unitOfWorkFactory.Create();

            // Re-read rather than reuse the in-memory instance: a failed commit may already have
            // applied a status to it that never reached the database.
            var persisted = await unitOfWork.Reports.GetByIdAsync(report.Id, ct);
            if (persisted is null || persisted.Status != ReportStatus.Init)
            {
                // Deleted (or processed elsewhere) while this run was working on it.
                _logger.ReportNoLongerPending(report.Id, persisted?.Status.ToString() ?? "deleted");
                return new ReportFailedAttempt(report.Id, report.ReportName, reason, 0, FailedAttemptKind.ReportGone);
            }

            var exhausted = persisted.RecordFailedAttempt(reason, MAX_PROCESSING_ATTEMPTS);
            await unitOfWork.Reports.UpdateProcessingStateAsync(persisted, ct);
            await unitOfWork.SaveChangesAsync(ct);

            if (exhausted)
            {
                // No reason text: it is the raw failure message and can embed statement content.
                _logger.ReportMarkedError(report.Id, persisted.ProcessingAttempts);
            }

            return new ReportFailedAttempt(report.Id, report.ReportName, reason, persisted.ProcessingAttempts,
                exhausted ? FailedAttemptKind.Exhausted : FailedAttemptKind.PendingRetry);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        // Deliberately broad: this runs on the failure path, so its own failure must not replace
        // the original reason or stop the run; the report simply stays Init and is retried.
        catch (Exception ex)
        {
            _logger.ReportAttemptNotRecorded(ex, report.Id);
            return new ReportFailedAttempt(report.Id, report.ReportName, reason, 0, FailedAttemptKind.NotRecorded);
        }
    }

    /// <summary>A report's result for this run.</summary>
    private abstract record ReportOutcome
    {
        private ReportOutcome()
        {
        }

        /// <summary>Nothing was persisted (unexpected failure or rate source unavailable); retry later.</summary>
        public sealed record AttemptFailed(string Reason) : ReportOutcome;

        /// <summary>The report was rejected (and committed as Error) before any event was processed.</summary>
        public sealed record Rejected(Error Error) : ReportOutcome;

        /// <summary>The report's events were processed and its filings committed with <paramref name="Status"/>.</summary>
        public sealed record Committed(
            ReportStatus Status,
            int Created,
            int Succeeded,
            int Failed,
            List<FilingCreationError> EventErrors) : ReportOutcome;
    }

    /// <summary>Result of processing a report's content, before anything is committed.</summary>
    private abstract record ReportProcessing
    {
        private ReportProcessing()
        {
        }

        /// <summary>The report cannot be processed as a whole (data-integrity failure).</summary>
        public sealed record Rejected(Error Error) : ReportProcessing;

        /// <summary>Processing stopped for a temporary reason; the report must not be committed.</summary>
        public sealed record Deferred(string Reason) : ReportProcessing;

        /// <summary>All events were processed; filings are staged in the unit of work.</summary>
        public sealed record Completed(int Created, int Succeeded, int Failed, List<FilingCreationError> Errors)
            : ReportProcessing;
    }

    private async Task<ReportProcessing> ProcessReportAsync(
        Report report, HolidayConf holidays, IFilingRepository filings, CancellationToken ct)
    {
        if (report.AttachmentContent == null || report.AttachmentContent.Length == 0)
            return new ReportProcessing.Rejected(
                new Error(ErrorCodes.REPORT_PROCESS_NO_ATTACHMENT, "Report has no attachment content"));

        var importer = await _importerRepository.GetByIdAsync(report.ImporterId, ct);
        if (importer is null)
            return new ReportProcessing.Rejected(
                new Error(ErrorCodes.IMPORTER_NOT_FOUND, $"Importer {report.ImporterId} not found"));

        if (importer.TaxpayerProfileId == null)
            return new ReportProcessing.Rejected(
                new Error(ErrorCodes.REPORT_PROCESS_NO_TAXPAYER, $"Importer {report.ImporterId} has no TaxpayerProfileId"));

        var taxpayerProfileId = importer.TaxpayerProfileId.Value;

        using var stream = new MemoryStream(report.AttachmentContent);
        var parseResult = await _statementParser.ParseAsync(stream, ct);

        if (!parseResult.IsSuccess)
            return new ReportProcessing.Rejected(
                new Error(ErrorCodes.REPORT_PROCESS_PARSE_FAILED, parseResult.Error.Message));

        var parsed = parseResult.Value;
        var context = new FilingContext(taxpayerProfileId, holidays, report.Id, parsed.EmbeddedRates);
        var created = 0;
        var succeeded = 0;
        var failed = 0;
        var errors = new List<FilingCreationError>();

        // A row the parser could not use (bad amount/date, unpaired withholding) is income that
        // never became an event: count it as a failed event so the report cannot end as Processed
        // with tax silently under- or over-stated.
        foreach (var parseError in parsed.Errors)
        {
            if (parseError.Severity == ParseSeverity.Error)
            {
                errors.Add(FilingCreationError.ForStatementRow(parseError));
                failed++;
            }
            else
            {
                // Never the parser message: it embeds the raw cell value (rates, amounts).
                _logger.ReportRowDiagnostic(report.Id, parseError.RowNumber, parseError.Code);
            }
        }

        // Dividends first, then credit-side interest, preserving error-list ordering. The unit of
        // work's filing repository includes filings staged earlier in this report, so a repeated or
        // corrected event inside the same statement is detected before commit. Storage failures are
        // not caught: they fail the whole report, which is retried, instead of losing an event.
        foreach (var incomeEvent in ToIncomeEvents(parsed))
        {
            ct.ThrowIfCancellationRequested();

            switch (await _filer.FileAsync(incomeEvent, context, filings, ct: ct))
            {
                case FilingOutcome.Created:
                    created++;
                    succeeded++;
                    break;

                case FilingOutcome.AlreadyFiled:
                    // Same income event imported from an earlier statement — idempotent skip.
                    succeeded++;
                    break;

                case FilingOutcome.Failed { Error.Code: ErrorCodes.RATE_SOURCE_UNAVAILABLE } unavailable:
                    // The rate may exist but could not be fetched: committing now would turn a
                    // temporary outage into a permanent event failure. Defer the whole report.
                    return new ReportProcessing.Deferred(unavailable.Error.Message);

                case FilingOutcome.Failed rejected:
                    errors.Add(ToEventError(incomeEvent, rejected.Error));
                    failed++;
                    break;

                case FilingOutcome.CorrectionDetected correction:
                    errors.Add(ToEventError(incomeEvent, correction.Error));
                    failed++;
                    break;
            }
        }

        return new ReportProcessing.Completed(created, succeeded, failed, errors);
    }

    /// <summary>
    /// Flattens the parsed statement into a single ordered event stream: all dividends
    /// first, then credit-side interest (debit interest is never taxable income). The parser
    /// has already paired each event's withholding.
    /// </summary>
    private static IEnumerable<IncomeEvent> ToIncomeEvents(StatementParseResult parsed)
    {
        foreach (var div in parsed.Dividends)
            yield return new IncomeEvent(IncomeType.Dividend, div.EntityName, div.Date, div.Currency, div.Amount, div.Withholding);

        foreach (var interest in parsed.Interest.Where(i => i.Type == InterestType.Credit))
        {
            yield return new IncomeEvent(IncomeType.Interest, interest.EntityName, interest.Date, interest.Currency,
                interest.Amount, interest.Withholding);
        }
    }

    private static FilingCreationError ToEventError(IncomeEvent incomeEvent, Error error)
        => new(incomeEvent.PayingEntity, incomeEvent.Date, incomeEvent.Currency, incomeEvent.Amount, error.Code, error.Message);
}

