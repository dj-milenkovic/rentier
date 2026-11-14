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
            try
            {
                // One unit of work per report: the report's filings and its status change are
                // staged together and committed by a single SaveChangesAsync, so a failure or
                // cancellation part-way through leaves the report at Init with no filings.
                await using var unitOfWork = _unitOfWorkFactory.Create();

                var processResult = await ProcessReportAsync(report, holidays, unitOfWork.Filings, ct);

                if (!processResult.IsSuccess)
                {
                    // Data integrity failure (no attachment, missing importer, parse error) — not an unexpected exception
                    report.SetStatus(ReportStatus.Error);
                    await unitOfWork.Reports.UpdateAsync(report, ct);
                    await unitOfWork.SaveChangesAsync(ct);
                    reportsErrored++;
                    _logger.LogWarning(
                        "Report {ReportId} rejected: [{Code}] {Message}",
                        report.Id, processResult.Error.Code, processResult.Error.Message);
                    var errorMsg = processResult.Error.Message;
                    command.Progress?.Report(new SyncProgressEntry(
                        DateTimeOffset.Now,
                        $"Report '{report.ReportName}': processing error — {errorMsg}.",
                        SyncProgressSeverity.Error));
                    reportDetails.Add(new ReportProcessingDetail(report.ReportName, 0, 0, SyncProgressSeverity.Error));
                    continue;
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
                await unitOfWork.Reports.UpdateAsync(report, ct);
                await unitOfWork.SaveChangesAsync(ct);

                // Tallies are updated only after the commit: until then no filing exists.
                filingsCreated += created;
                allEventErrors.AddRange(eventErrors);
                switch (status)
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
                        report.Id, succeeded + failed, created, failed, status);
                }

                var severity = ReportProcessingDetail.ClassifySeverity(created, failed);
                var detail = new ReportProcessingDetail(report.ReportName, created, failed, severity);
                command.Progress?.Report(new SyncProgressEntry(DateTimeOffset.Now, detail.ToLogMessage(), severity));
                reportDetails.Add(detail);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Truly unexpected failures (network, DB) — log as error.
                // The report's unit of work is already disposed here, so anything it staged
                // (filings, status) was discarded; the Error status is its own commit.
                await MarkReportErroredAsync(report.Id, ct);
                reportsErrored++;
                _logger.LogError(ex, "Report {ReportId} failed unexpectedly: {Message}", report.Id, ex.Message);
                command.Progress?.Report(new SyncProgressEntry(
                    DateTimeOffset.Now,
                    $"Report '{report.ReportName}': processing error — {ex.Message}.",
                    SyncProgressSeverity.Error));
                reportDetails.Add(new ReportProcessingDetail(report.ReportName, 0, 0, SyncProgressSeverity.Error));
            }
        }

        return Result<ProcessReportsResult, Error>.Success(
            new ProcessReportsResult(filingsCreated, reportsProcessed, reportsErrored, allEventErrors, reportsPartialError, reportDetails));
    }

    private async Task MarkReportErroredAsync(Guid reportId, CancellationToken ct)
    {
        // Re-read the report: the in-memory instance may already carry the status that the
        // discarded unit of work failed to commit, which would make Init → Error invalid.
        await using var unitOfWork = _unitOfWorkFactory.Create();
        var persisted = await unitOfWork.Reports.GetByIdAsync(reportId, ct);
        if (persisted is null)
            return;

        persisted.SetStatus(ReportStatus.Error);
        await unitOfWork.Reports.UpdateAsync(persisted, ct);
        await unitOfWork.SaveChangesAsync(ct);
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
        var staging = new FilingStaging(filings, []);

        // Dividends first, then credit-side interest — same order as before the merge,
        // preserving error-list ordering.
        foreach (var ev in BuildIncomeEvents(parsed, taxpayerProfileId, report.Id, holidays))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var outcome = await ProcessIncomeEventAsync(ev, rateProvider, staging, errors, ct);
                ApplyOutcome(outcome, ref created, ref succeeded, ref failed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
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
    private async Task<IncomeEventOutcome> ProcessIncomeEventAsync(
        IncomeEventRequest request,
        Func<DateOnly, string, Task<Result<RateResolution, Error>>> rateProvider,
        FilingStaging staging,
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

        // Filings staged earlier in this report are not in the database yet, so the query
        // alone would miss them; include them so a repeated or corrected event inside the
        // same statement is still detected.
        var persistedFilings = await staging.Repository.GetByIncomeEventAsync(
            request.TaxpayerProfileId, request.EntityName, request.Date, ct);
        var existingFilings = persistedFilings
            .Concat(staging.Staged.Where(f =>
                f.TaxpayerProfileId == request.TaxpayerProfileId &&
                f.PayingEntity == request.EntityName &&
                f.IncomeDate == request.Date))
            .ToList();

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

            await staging.Repository.AddAsync(filing, ct);
            staging.Staged.Add(filing);
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

    /// <summary>
    /// The current report's staging repository plus the filings staged so far (staged
    /// filings are invisible to repository queries until the unit of work commits).
    /// </summary>
    private sealed record FilingStaging(IFilingRepository Repository, List<Filing> Staged);

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

