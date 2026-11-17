using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Parsing;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Handlers;

public sealed class ProcessReportsCommandHandler
    : ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>
{
    private readonly IReportRepository _reportRepository;
    private readonly IImporterRepository _importerRepository;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IExchangeRateResolver _exchangeRateResolver;
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
        _exchangeRateResolver = exchangeRateResolver;
        _holidayRepository = holidayRepository;
        _statementParser = statementParser;
        _logger = logger;
    }

    public async Task<Result<ProcessReportsResult, Error>> HandleAsync(
        ProcessReportsCommand command, CancellationToken ct = default)
    {
        var holidayDto = await _holidayRepository.GetHolidayConfAsync(ct);
        var holidays = new HolidayConf(holidayDto.Holidays.Select(h => h.Date).ToList());

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
                _logger.LogError(ex, "Report {ReportId} failed unexpectedly: {Message}", report.Id, ex.Message);
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
                _logger.LogWarning(
                    "Report {ReportId} rejected: [{Code}] {Message}",
                    report.Id, rejected.Error.Code, rejected.Error.Message);
                severity = SyncProgressSeverity.Error;
                message = $"Report '{report.ReportName}': processing error — {rejected.Error.Message}.";
                break;

            case ReportOutcome.Committed committed:
                tally.FilingsCreated += committed.Created;
                tally.EventErrors.AddRange(committed.EventErrors);
                tally.Count(committed.Status);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Report {ReportId}: {Total} events, {Created} filings, {Failed} failed -> {Status}",
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
                _logger.LogError(persistEx,
                    "Report {ReportId}: rejection [{Code}] could not be saved: {Message}",
                    report.Id, rejection.Error.Code, persistEx.Message);
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
                _logger.LogWarning(
                    "Report {ReportId} failed but is no longer pending (status {Status}); nothing to retry",
                    report.Id, persisted?.Status.ToString() ?? "deleted");
                return new ReportFailedAttempt(report.Id, report.ReportName, reason, 0, FailedAttemptKind.ReportGone);
            }

            var exhausted = persisted.RecordFailedAttempt(reason, MAX_PROCESSING_ATTEMPTS);
            await unitOfWork.Reports.UpdateProcessingStateAsync(persisted, ct);
            await unitOfWork.SaveChangesAsync(ct);

            if (exhausted)
            {
                _logger.LogError(
                    "Report {ReportId} failed {Attempts} times and was marked Error: {Reason}",
                    report.Id, persisted.ProcessingAttempts, reason);
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
            _logger.LogError(ex, "Report {ReportId}: the failed attempt could not be recorded; it stays pending", report.Id);
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
        var rateProvider = BuildRateProvider(parsed, holidays, ct);
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
                _logger.LogWarning(
                    "Report {ReportId} row {Row}: [{Code}] {Message}",
                    report.Id, parseError.RowNumber, parseError.Code, parseError.Message);
            }
        }

        // Dividends first, then credit-side interest — same order as before the merge,
        // preserving error-list ordering.
        foreach (var ev in BuildIncomeEvents(parsed, taxpayerProfileId, report.Id, holidays))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var outcome = await ProcessIncomeEventAsync(ev, rateProvider, filings, errors, ct);
                if (outcome.Kind == IncomeEventOutcomeKind.SourceUnavailable)
                {
                    // The rate may exist but could not be fetched: committing now would turn a
                    // temporary outage into a permanent event failure. Defer the whole report.
                    return new ReportProcessing.Deferred(outcome.Reason!);
                }

                ApplyOutcome(outcome.Kind, ref created, ref succeeded, ref failed);
            }
            catch (DomainException ex)
            {
                // A business-rule violation for this one event (validation, tax calculation,
                // deadline). Anything else (e.g. a database error) is not caught here: it fails
                // the whole report, which is then retried, instead of losing this event forever.
                errors.Add(new FilingCreationError(ev.EntityName, ev.Date, ev.Currency, ev.Amount, ErrorCodes.DOMAIN_ERROR, ex.Message));
                failed++;
            }
        }

        return new ReportProcessing.Completed(created, succeeded, failed, errors);
    }

    /// <summary>
    /// Flattens the parsed statement into a single ordered event stream: all dividends
    /// first, then credit-side interest (debit interest is never taxable income). The parser
    /// has already paired each event's withholding.
    /// </summary>
    private static IEnumerable<IncomeEventRequest> BuildIncomeEvents(
        StatementParseResult parsed, Guid taxpayerProfileId, Guid reportId, HolidayConf holidays)
    {
        foreach (var div in parsed.Dividends)
        {
            yield return new IncomeEventRequest(IncomeType.Dividend, div.EntityName, div.Date, div.Currency, div.Amount,
                div.Withholding, taxpayerProfileId, reportId, holidays);
        }

        foreach (var interest in parsed.Interest.Where(i => i.Type == InterestType.Credit))
        {
            yield return new IncomeEventRequest(IncomeType.Interest, interest.EntityName, interest.Date, interest.Currency,
                interest.Amount, interest.Withholding, taxpayerProfileId, reportId, holidays);
        }
    }

    private static void ApplyOutcome(IncomeEventOutcomeKind outcome, ref int created, ref int succeeded, ref int failed)
    {
        switch (outcome)
        {
            case IncomeEventOutcomeKind.Created:
                created++;
                succeeded++;
                break;
            case IncomeEventOutcomeKind.SkippedIdempotent:
                succeeded++;
                break;
            case IncomeEventOutcomeKind.Failed:
                failed++;
                break;
        }
    }

    /// <summary>Groups the per-event data needed to process a single dividend or interest entry.</summary>
    private sealed record IncomeEventRequest(
        IncomeType IncomeType,
        string EntityName,
        DateOnly Date,
        string Currency,
        decimal Amount,
        decimal Withholding,
        Guid TaxpayerProfileId,
        Guid ReportId,
        HolidayConf Holidays);

    /// <summary>
    /// Resolves the exchange rate, computes tax, and either creates a filing, skips a
    /// duplicate (idempotent re-import), or flags a broker correction for manual review.
    /// Shared tail for both the dividend and interest processing loops; the caller
    /// supplies the already-matched WHT record (the matching rule differs per income type).
    /// </summary>
    private static async Task<IncomeEventOutcome> ProcessIncomeEventAsync(
        IncomeEventRequest request,
        Func<DateOnly, string, Task<Result<RateResolution, Error>>> rateProvider,
        IFilingRepository filings,
        List<FilingCreationError> errors,
        CancellationToken ct)
    {
        var rateResult = await rateProvider(request.Date, request.Currency);
        if (!rateResult.IsSuccess)
        {
            if (rateResult.Error.Code == ErrorCodes.RATE_SOURCE_UNAVAILABLE)
                return IncomeEventOutcome.SourceUnavailable(rateResult.Error.Message);

            errors.Add(new FilingCreationError(request.EntityName, request.Date, request.Currency, request.Amount,
                rateResult.Error.Code, rateResult.Error.Message));
            return IncomeEventOutcome.Failed;
        }
        var resolution = rateResult.Value;

        var info = await TaxCalculationService.CalculateAsync(
            new IncomeTaxInput(request.IncomeType, request.EntityName, request.Date, request.Amount, request.Currency,
                request.Withholding, request.Currency),
            (_, _) => Task.FromResult(resolution.Rate), ct);

        // The unit-of-work repository includes filings staged earlier in this report, so a
        // repeated or corrected event inside the same statement is detected before commit.
        var existingFilings = await filings.GetByIncomeEventAsync(
            request.TaxpayerProfileId, request.EntityName, request.Date, ct);

        if (existingFilings.Count == 0)
        {
            var deadline = FilingDeadlineCalculator.CalculateDeadline(request.Date, request.Holidays);
            var filing = Filing.CreateFromIncome(
                info, request.TaxpayerProfileId, deadline,
                new FilingProvenance(
                    ReportId: request.ReportId,
                    ExchangeRateSourceDate: resolution.SourceDate,
                    ExchangeRateSourceType: resolution.SourceType,
                    Ticker: request.EntityName));

            await filings.AddAsync(filing, ct);
            return IncomeEventOutcome.Created;
        }

        if (existingFilings.Any(f => f.GrossIncomeRsd == info.GrossIncomeRsd))
        {
            // Same income event already imported from an earlier statement — idempotent skip
            return IncomeEventOutcome.SkippedIdempotent;
        }

        // Same entity + income date but a different gross: the broker issued a
        // correction (reversal + re-book) in a later statement. Never silently
        // create a second filing for the same income event — flag for review.
        errors.Add(new FilingCreationError(request.EntityName, request.Date, request.Currency, request.Amount,
            ErrorCodes.FILING_CORRECTION_DETECTED,
            $"Broker correction detected for '{request.EntityName}' on {request.Date:yyyy-MM-dd}: " +
            $"an existing filing has gross {existingFilings[0].GrossIncomeRsd} RSD, this statement " +
            $"yields {info.GrossIncomeRsd} RSD. Review and adjust the filing manually."));
        return IncomeEventOutcome.Failed;
    }

    private enum IncomeEventOutcomeKind
    {
        Created,
        SkippedIdempotent,
        Failed,
        SourceUnavailable,
    }

    /// <summary>Per-event result; <see cref="Reason"/> is set only for <see cref="IncomeEventOutcomeKind.SourceUnavailable"/>.</summary>
    private readonly record struct IncomeEventOutcome(IncomeEventOutcomeKind Kind, string? Reason = null)
    {
        public static readonly IncomeEventOutcome Created = new(IncomeEventOutcomeKind.Created);
        public static readonly IncomeEventOutcome SkippedIdempotent = new(IncomeEventOutcomeKind.SkippedIdempotent);
        public static readonly IncomeEventOutcome Failed = new(IncomeEventOutcomeKind.Failed);

        public static IncomeEventOutcome SourceUnavailable(string reason)
            => new(IncomeEventOutcomeKind.SourceUnavailable, reason);
    }

    private Func<DateOnly, string, Task<Result<RateResolution, Error>>> BuildRateProvider(
        StatementParseResult parsed, HolidayConf holidays, CancellationToken ct)
    {
        return async (date, currency) =>
        {
            var result = await _exchangeRateResolver.ResolveAsync(date, currency, holidays, ct: ct);
            if (result.IsSuccess)
                return Result<RateResolution, Error>.Success(result.Value);

            // NBS unreachable: its rate for this currency may exist, so an IBKR cross-rate must
            // not replace it (IBKR rates are a fallback only for currencies NBS does not publish).
            if (result.Error.Code == ErrorCodes.RATE_SOURCE_UNAVAILABLE)
                return Result<RateResolution, Error>.Failure(result.Error);

            // Cross-rate fallback: find IBKR rate for this currency to USD
            var ibkrRate = parsed.EmbeddedRates
                .FirstOrDefault(r => r.FromCurrency.Equals(currency, StringComparison.OrdinalIgnoreCase));
            if (ibkrRate != null)
            {
                var usdResult = await _exchangeRateResolver.ResolveAsync(date, "USD", holidays, ct: ct);
                if (!usdResult.IsSuccess && usdResult.Error.Code == ErrorCodes.RATE_SOURCE_UNAVAILABLE)
                    return Result<RateResolution, Error>.Failure(usdResult.Error);

                if (usdResult.IsSuccess)
                {
                    var syntheticRateValue = ibkrRate.Rate * usdResult.Value.Rate.RateToRsd;
                    if (syntheticRateValue <= 0)
                        return Result<RateResolution, Error>.Failure(
                            new Error(ErrorCodes.INVALID_EXCHANGE_RATE,
                                $"Synthetic cross-rate for '{currency}' on {date:yyyy-MM-dd} is not positive ({syntheticRateValue})."));

                    var syntheticRate = new ExchangeRate(usdResult.Value.SourceDate, currency, syntheticRateValue);
                    return Result<RateResolution, Error>.Success(
                        new RateResolution(syntheticRate, usdResult.Value.SourceDate, usdResult.Value.SourceType));
                }
            }

            return Result<RateResolution, Error>.Failure(result.Error);
        };
    }
}

