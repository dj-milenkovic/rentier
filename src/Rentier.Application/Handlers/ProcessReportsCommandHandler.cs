using Microsoft.Extensions.Logging;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Parsing;
using Rentier.Application.Repositories;
using Rentier.Application.Services;
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
    private readonly IFilingRepository _filingRepository;
    private readonly ExchangeRateResolver _exchangeRateResolver;
    private readonly IHolidayRepository _holidayRepository;
    private readonly IStatementParser _statementParser;
    private readonly ILogger<ProcessReportsCommandHandler> _logger;

    public ProcessReportsCommandHandler(
        IReportRepository reportRepository,
        IImporterRepository importerRepository,
        IFilingRepository filingRepository,
        ExchangeRateResolver exchangeRateResolver,
        IHolidayRepository holidayRepository,
        IStatementParser statementParser,
        ILogger<ProcessReportsCommandHandler> logger)
    {
        _reportRepository = reportRepository;
        _importerRepository = importerRepository;
        _filingRepository = filingRepository;
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
        var filingsCreated = 0;
        var reportsProcessed = 0;
        var reportsErrored = 0;
        var reportsPartialError = 0;

        foreach (var report in initReports)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var (created, succeeded, failed, eventErrors) =
                    await ProcessReportAsync(report, holidays, ct);

                filingsCreated += created;
                allEventErrors.AddRange(eventErrors);

                ReportStatus status;
                if (failed == 0)
                {
                    status = ReportStatus.Processed;
                    reportsProcessed++;
                }
                else if (succeeded > 0)
                {
                    status = ReportStatus.PartialError;
                    reportsPartialError++;
                }
                else
                {
                    status = ReportStatus.Error;
                    reportsErrored++;
                }

                report.SetStatus(status);
                await _reportRepository.UpdateAsync(report, ct);

                _logger.LogInformation(
                    "Report {ReportId}: {Total} events, {Created} filings, {Failed} failed -> {Status}",
                    report.Id, succeeded + failed, created, failed, status);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                report.SetStatus(ReportStatus.Error);
                await _reportRepository.UpdateAsync(report, ct);
                reportsErrored++;
                _logger.LogError(ex, "Report {ReportId} failed: {Message}", report.Id, ex.Message);
            }
        }

        return Result<ProcessReportsResult, Error>.Success(
            new ProcessReportsResult(filingsCreated, reportsProcessed, reportsErrored, allEventErrors, reportsPartialError));
    }

    private async Task<(int created, int succeeded, int failed, List<FilingCreationError> errors)>
        ProcessReportAsync(Report report, HolidayConf holidays, CancellationToken ct)
    {
        if (report.AttachmentContent == null || report.AttachmentContent.Length == 0)
            throw new InvalidOperationException("Report has no attachment content");

        var importer = await _importerRepository.GetByIdAsync(report.ImporterId, ct)
            ?? throw new InvalidOperationException($"Importer {report.ImporterId} not found");

        if (importer.TaxpayerProfileId == null)
            throw new InvalidOperationException($"Importer {report.ImporterId} has no TaxpayerProfileId");

        var taxpayerProfileId = importer.TaxpayerProfileId.Value;

        using var stream = new MemoryStream(report.AttachmentContent);
        var parseResult = await _statementParser.ParseAsync(stream, ct);

        if (!parseResult.IsSuccess)
            throw new InvalidOperationException($"Parse failed: {parseResult.Error.Message}");

        var parsed = parseResult.Value;
        var rateProvider = BuildRateProvider(parsed, holidays, ct);
        var created = 0;
        var succeeded = 0;
        var failed = 0;
        var errors = new List<FilingCreationError>();

        // Process dividends
        foreach (var div in parsed.Dividends)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var wht = parsed.Withholdings.FirstOrDefault(w =>
                    w.Date == div.Date && w.EntityName == div.EntityName && w.Currency == div.Currency);

                var rateResult = await rateProvider(div.Date, div.Currency);
                if (!rateResult.IsSuccess)
                {
                    errors.Add(new FilingCreationError(div.EntityName, div.Date, div.Currency, div.Amount,
                        rateResult.Error.Code, rateResult.Error.Message));
                    failed++;
                    continue;
                }
                var resolution = rateResult.Value;

                var info = await TaxCalculationService.CalculateAsync(
                    IncomeType.Dividend, div.EntityName, div.Date, div.Amount, div.Currency,
                    wht?.Amount ?? 0m, wht?.Currency ?? div.Currency,
                    (_, _) => Task.FromResult(resolution.Rate), ct);

                var exists = await _filingRepository.ExistsByIncomeAsync(
                    taxpayerProfileId, div.EntityName, div.Date, info.GrossIncomeRsd, ct);

                if (!exists)
                {
                    var deadline = FilingDeadlineCalculator.CalculateDeadline(div.Date, holidays);
                    var filing = Filing.CreateFromIncome(
                        taxpayerProfileId, IncomeType.Dividend, div.EntityName, div.Date,
                        info.GrossIncomeRsd, info.WhtPaidRsd, info.GrossTaxPayableRsd, info.TaxPayableRsd,
                        deadline, report.Id, resolution.SourceDate, resolution.SourceType,
                        ticker: div.EntityName);

                    await _filingRepository.AddAsync(filing, ct);
                    created++;
                }
                succeeded++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(new FilingCreationError(div.EntityName, div.Date, div.Currency, div.Amount, "DOMAIN_ERROR", ex.Message));
                failed++;
            }
        }

        // Process interest - credit entries only
        foreach (var interest in parsed.Interest.Where(i => i.Type == InterestType.Credit))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var wht = parsed.Withholdings.FirstOrDefault(w =>
                    w.Date == interest.Date && w.EntityName == interest.EntityName);

                var rateResult = await rateProvider(interest.Date, interest.Currency);
                if (!rateResult.IsSuccess)
                {
                    errors.Add(new FilingCreationError(interest.EntityName, interest.Date, interest.Currency, interest.Amount,
                        rateResult.Error.Code, rateResult.Error.Message));
                    failed++;
                    continue;
                }
                var resolution = rateResult.Value;

                var info = await TaxCalculationService.CalculateAsync(
                    IncomeType.Interest, interest.EntityName, interest.Date, interest.Amount, interest.Currency,
                    wht?.Amount ?? 0m, wht?.Currency ?? interest.Currency,
                    (_, _) => Task.FromResult(resolution.Rate), ct);

                var exists = await _filingRepository.ExistsByIncomeAsync(
                    taxpayerProfileId, interest.EntityName, interest.Date, info.GrossIncomeRsd, ct);

                if (!exists)
                {
                    var deadline = FilingDeadlineCalculator.CalculateDeadline(interest.Date, holidays);
                    var filing = Filing.CreateFromIncome(
                        taxpayerProfileId, IncomeType.Interest, interest.EntityName, interest.Date,
                        info.GrossIncomeRsd, info.WhtPaidRsd, info.GrossTaxPayableRsd, info.TaxPayableRsd,
                        deadline, report.Id, resolution.SourceDate, resolution.SourceType,
                        ticker: interest.EntityName);

                    await _filingRepository.AddAsync(filing, ct);
                    created++;
                }
                succeeded++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(new FilingCreationError(interest.EntityName, interest.Date, interest.Currency, interest.Amount, "DOMAIN_ERROR", ex.Message));
                failed++;
            }
        }

        return (created, succeeded, failed, errors);
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
                    var syntheticRate = new ExchangeRate(usdResult.Value.SourceDate, currency,
                        ibkrRate.Rate * usdResult.Value.Rate.RateToRsd);
                    return Result<RateResolution, Error>.Success(
                        new RateResolution(syntheticRate, usdResult.Value.SourceDate, usdResult.Value.SourceType));
                }
            }

            return Result<RateResolution, Error>.Failure(result.Error);
        };
    }
}