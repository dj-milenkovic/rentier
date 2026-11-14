using Microsoft.Extensions.Logging;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Parsing;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
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

    public ProcessReportsCommandHandler(
        IReportRepository reportRepository,
        IImporterRepository importerRepository,
        IUnitOfWorkFactory unitOfWorkFactory,
        IExchangeRateResolver exchangeRateResolver,
        IHolidayRepository holidayRepository,
        IStatementParser statementParser,
        ILogger<ProcessReportsCommandHandler> logger)
    {
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

        var allEventErrors = new List<FilingCreationError>();
        var reportDetails = new List<ReportProcessingDetail>();
        var filingsCreated = 0;
        var reportsProcessed = 0;
        var reportsErrored = 0;
        var reportsPartialError = 0;

        foreach (var report in initReports)
        {
            ct.ThrowIfCancellationRequested();

            // Only processing and the commit sit inside the try: once the report is committed
            // its outcome is final, so nothing after that point may route it to the
            // unexpected-failure path (which would count it twice and try Processed → Error).
            ReportOutcome outcome;
            try
            {
                outcome = await ProcessAndCommitReportAsync(report, holidays, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Truly unexpected failures (network, DB, a failed commit) — log as error.
                // The report's unit of work is already disposed here, so anything it staged
                // (filings, status) was discarded; the Error status is its own commit.
                // A storage failure on any one filing therefore fails the whole report: it is
                // reported as Error (not PartialError) with no filings, and a later run retries
                // the complete report.
                await MarkReportErroredAsync(report, ct);
                reportsErrored++;
                _logger.LogError(ex, "Report {ReportId} failed unexpectedly: {Message}", report.Id, ex.Message);
                command.Progress?.Report(new SyncProgressEntry(
                    DateTimeOffset.Now,
                    $"Report '{report.ReportName}': processing error — {ex.Message}.",
                    SyncProgressSeverity.Error));
                reportDetails.Add(new ReportProcessingDetail(report.ReportName, 0, 0, SyncProgressSeverity.Error));
                continue;
            }

            // ── The report is committed: bookkeeping only from here on ──
            if (outcome.Rejection is { } rejection)
            {
                // Data integrity failure (no attachment, missing importer, parse error) — not an unexpected exception
                reportsErrored++;
                _logger.LogWarning(
                    "Report {ReportId} rejected: [{Code}] {Message}",
                    report.Id, rejection.Code, rejection.Message);
                command.Progress?.Report(new SyncProgressEntry(
                    DateTimeOffset.Now,
                    $"Report '{report.ReportName}': processing error — {rejection.Message}.",
                    SyncProgressSeverity.Error));
                reportDetails.Add(new ReportProcessingDetail(report.ReportName, 0, 0, SyncProgressSeverity.Error));
                continue;
            }

            filingsCreated += outcome.Created;
            allEventErrors.AddRange(outcome.EventErrors);
            switch (outcome.Status)
            {
                case ReportStatus.Processed:
                    reportsProcessed++;
                    break;
                case ReportStatus.PartialError:
                    reportsPartialError++;
                    break;
                default:
                    reportsErrored++;
                    break;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Report {ReportId}: {Total} events, {Created} filings, {Failed} failed -> {Status}",
                    report.Id, outcome.Succeeded + outcome.Failed, outcome.Created, outcome.Failed, outcome.Status);
            }

            var severity = ReportProcessingDetail.ClassifySeverity(outcome.Created, outcome.Failed);
            var detail = new ReportProcessingDetail(report.ReportName, outcome.Created, outcome.Failed, severity);
            command.Progress?.Report(new SyncProgressEntry(DateTimeOffset.Now, detail.ToLogMessage(), severity));
            reportDetails.Add(detail);
        }

        return Result<ProcessReportsResult, Error>.Success(
            new ProcessReportsResult(filingsCreated, reportsProcessed, reportsErrored, allEventErrors, reportsPartialError, reportDetails));
    }

    /// <summary>
    /// Processes one report inside its own unit of work and commits its filings and its new
    /// status together with a single SaveChangesAsync, so a failure or cancellation part-way
    /// through leaves the report at Init with no filings.
    /// </summary>
    private async Task<ReportOutcome> ProcessAndCommitReportAsync(Report report, HolidayConf holidays, CancellationToken ct)
    {
        await using var unitOfWork = _unitOfWorkFactory.Create();

        var processResult = await ProcessReportAsync(report, holidays, unitOfWork.Filings, ct);

        if (!processResult.IsSuccess)
        {
            report.SetStatus(ReportStatus.Error);
            await unitOfWork.Reports.UpdateStatusAsync(report, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return ReportOutcome.Rejected(processResult.Error);
        }

        var (created, succeeded, failed, eventErrors) = processResult.Value;

        ReportStatus status;
        if (failed == 0)
            status = ReportStatus.Processed;
        else if (succeeded > 0)
            status = ReportStatus.PartialError;
        else
            status = ReportStatus.Error;

        report.SetStatus(status);
        await unitOfWork.Reports.UpdateStatusAsync(report, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ReportOutcome(null, status, created, succeeded, failed, eventErrors);
    }

    /// <summary>
    /// Records Error for a report whose processing or commit failed unexpectedly, in a fresh
    /// unit of work. Never throws (except on cancellation): a report that cannot be marked
    /// stays at Init and is retried by the next run, and the remaining reports still run.
    /// </summary>
    private async Task MarkReportErroredAsync(Report report, CancellationToken ct)
    {
        try
        {
            await using var unitOfWork = _unitOfWorkFactory.Create();

            // If the failure happened before a status was applied, the in-memory report is
            // still Init and can be used directly. Otherwise its new status was applied but the
            // commit failed, so the in-memory instance no longer reflects the database: re-read it.
            var target = report.Status == ReportStatus.Init
                ? report
                : await unitOfWork.Reports.GetByIdAsync(report.Id, ct);

            if (target is null || target.Status != ReportStatus.Init)
            {
                _logger.LogWarning(
                    "Report {ReportId} not marked as Error: persisted status is {Status}",
                    report.Id, target?.Status.ToString() ?? "missing");
                return;
            }

            target.SetStatus(ReportStatus.Error);
            await unitOfWork.Reports.UpdateStatusAsync(target, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report {ReportId} could not be marked as Error; it stays at Init", report.Id);
        }
    }

    /// <summary>A committed report's result: either a rejection or its event tallies.</summary>
    private sealed record ReportOutcome(
        Error? Rejection,
        ReportStatus Status,
        int Created,
        int Succeeded,
        int Failed,
        List<FilingCreationError> EventErrors)
    {
        public static ReportOutcome Rejected(Error error) => new(error, ReportStatus.Error, 0, 0, 0, []);
    }

    private async Task<Result<(int created, int succeeded, int failed, List<FilingCreationError> errors), Error>>
        ProcessReportAsync(Report report, HolidayConf holidays, IFilingRepository filings, CancellationToken ct)
    {
        if (report.AttachmentContent == null || report.AttachmentContent.Length == 0)
            return Result<(int, int, int, List<FilingCreationError>), Error>.Failure(
                new Error(ErrorCodes.REPORT_PROCESS_NO_ATTACHMENT, "Report has no attachment content"));

        var importer = await _importerRepository.GetByIdAsync(report.ImporterId, ct);
        if (importer is null)
            return Result<(int, int, int, List<FilingCreationError>), Error>.Failure(
                new Error(ErrorCodes.IMPORTER_NOT_FOUND, $"Importer {report.ImporterId} not found"));

        if (importer.TaxpayerProfileId == null)
            return Result<(int, int, int, List<FilingCreationError>), Error>.Failure(
                new Error(ErrorCodes.REPORT_PROCESS_NO_TAXPAYER, $"Importer {report.ImporterId} has no TaxpayerProfileId"));

        var taxpayerProfileId = importer.TaxpayerProfileId.Value;

        using var stream = new MemoryStream(report.AttachmentContent);
        var parseResult = await _statementParser.ParseAsync(stream, ct);

        if (!parseResult.IsSuccess)
            return Result<(int, int, int, List<FilingCreationError>), Error>.Failure(
                new Error(ErrorCodes.REPORT_PROCESS_PARSE_FAILED, parseResult.Error.Message));

        var parsed = parseResult.Value;
        var rateProvider = BuildRateProvider(parsed, holidays, ct);
        var created = 0;
        var succeeded = 0;
        var failed = 0;
        var errors = new List<FilingCreationError>();

        // Dividends first, then credit-side interest — same order as before the merge,
        // preserving error-list ordering.
        foreach (var ev in BuildIncomeEvents(parsed, taxpayerProfileId, report.Id, holidays))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var outcome = await ProcessIncomeEventAsync(ev, rateProvider, filings, errors, ct);
                ApplyOutcome(outcome, ref created, ref succeeded, ref failed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Per-event failures before staging (domain validation, tax calculation).
                // Writes are only staged here, so storage failures surface at the report's
                // commit instead and fail the whole report (see HandleAsync).
                errors.Add(new FilingCreationError(ev.EntityName, ev.Date, ev.Currency, ev.Amount, ErrorCodes.DOMAIN_ERROR, ex.Message));
                failed++;
            }
        }

        return Result<(int, int, int, List<FilingCreationError>), Error>.Success(
            (created, succeeded, failed, errors));
    }

    /// <summary>
    /// Flattens the parsed statement into a single ordered event stream: all dividends
    /// first, then credit-side interest. Withholding matching differs between the two —
    /// dividends match on currency as well, interest does not.
    /// </summary>
    private static IEnumerable<IncomeEventRequest> BuildIncomeEvents(
        StatementParseResult parsed, Guid taxpayerProfileId, Guid reportId, HolidayConf holidays)
    {
        foreach (var div in parsed.Dividends)
        {
            var wht = parsed.Withholdings.FirstOrDefault(w =>
                w.Date == div.Date && w.EntityName == div.EntityName && w.Currency == div.Currency);

            yield return new IncomeEventRequest(IncomeType.Dividend, div.EntityName, div.Date, div.Currency, div.Amount, wht,
                taxpayerProfileId, reportId, holidays);
        }

        // Interest - credit entries only
        foreach (var interest in parsed.Interest.Where(i => i.Type == InterestType.Credit))
        {
            var wht = parsed.Withholdings.FirstOrDefault(w =>
                w.Date == interest.Date && w.EntityName == interest.EntityName);

            yield return new IncomeEventRequest(IncomeType.Interest, interest.EntityName, interest.Date, interest.Currency, interest.Amount, wht,
                taxpayerProfileId, reportId, holidays);
        }
    }

    private static void ApplyOutcome(IncomeEventOutcome outcome, ref int created, ref int succeeded, ref int failed)
    {
        switch (outcome)
        {
            case IncomeEventOutcome.Created:
                created++;
                succeeded++;
                break;
            case IncomeEventOutcome.SkippedIdempotent:
                succeeded++;
                break;
            case IncomeEventOutcome.Failed:
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
        WithholdingTaxRecord? Wht,
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
            errors.Add(new FilingCreationError(request.EntityName, request.Date, request.Currency, request.Amount,
                rateResult.Error.Code, rateResult.Error.Message));
            return IncomeEventOutcome.Failed;
        }
        var resolution = rateResult.Value;

        var info = await TaxCalculationService.CalculateAsync(
            new IncomeTaxInput(request.IncomeType, request.EntityName, request.Date, request.Amount, request.Currency,
                request.Wht?.Amount ?? 0m, request.Wht?.Currency ?? request.Currency),
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

    private enum IncomeEventOutcome
    {
        Created,
        SkippedIdempotent,
        Failed,
    }

    private Func<DateOnly, string, Task<Result<RateResolution, Error>>> BuildRateProvider(
        StatementParseResult parsed, HolidayConf holidays, CancellationToken ct)
    {
        return async (date, currency) =>
        {
            var result = await _exchangeRateResolver.ResolveAsync(date, currency, holidays, ct: ct);
            if (result.IsSuccess)
                return Result<RateResolution, Error>.Success(result.Value);

            // Cross-rate fallback: find IBKR rate for this currency to USD
            var ibkrRate = parsed.EmbeddedRates
                .FirstOrDefault(r => r.FromCurrency.Equals(currency, StringComparison.OrdinalIgnoreCase));
            if (ibkrRate != null)
            {
                var usdResult = await _exchangeRateResolver.ResolveAsync(date, "USD", holidays, ct: ct);
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

