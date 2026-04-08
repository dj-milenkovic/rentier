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
    private readonly IFilingRepository _filingRepository;
    private readonly IExchangeRateFetcher _exchangeRateFetcher;
    private readonly IHolidayRepository _holidayRepository;
    private readonly IStatementParser _statementParser;

    public ProcessReportsCommandHandler(
        IReportRepository reportRepository,
        IImporterRepository importerRepository,
        IFilingRepository filingRepository,
        IExchangeRateFetcher exchangeRateFetcher,
        IHolidayRepository holidayRepository,
        IStatementParser statementParser)
    {
        _reportRepository = reportRepository;
        _importerRepository = importerRepository;
        _filingRepository = filingRepository;
        _exchangeRateFetcher = exchangeRateFetcher;
        _holidayRepository = holidayRepository;
        _statementParser = statementParser;
    }

    public async Task<Result<ProcessReportsResult, Error>> HandleAsync(
        ProcessReportsCommand command, CancellationToken ct = default)
    {
        var holidayDto = await _holidayRepository.GetHolidayConfAsync(ct);
        var holidays = new HolidayConf(holidayDto.Holidays.Select(h => h.Date).ToList());

        var initReports = await _reportRepository.GetByStatusAsync(ReportStatus.Init, ct);

        var errors = new List<string>();
        var filingsCreated = 0;
        var reportsProcessed = 0;
        var reportsErrored = 0;

        foreach (var report in initReports)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var created = await ProcessReportAsync(report, holidays, errors, ct);
                filingsCreated += created;
                report.SetStatus(ReportStatus.Processed);
                await _reportRepository.UpdateAsync(report, ct);
                reportsProcessed++;
            }
            catch (Exception ex)
            {
                errors.Add($"Report {report.Id}: {ex.Message}");
                report.SetStatus(ReportStatus.Error);
                await _reportRepository.UpdateAsync(report, ct);
                reportsErrored++;
            }
        }

        return Result<ProcessReportsResult, Error>.Success(
            new ProcessReportsResult(filingsCreated, reportsProcessed, reportsErrored, errors));
    }

    private async Task<int> ProcessReportAsync(
        Report report,
        HolidayConf holidays,
        List<string> errors,
        CancellationToken ct)
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
        var rateProvider = BuildRateProvider(parsed, ct);
        var created = 0;

        // Process dividends
        foreach (var div in parsed.Dividends)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Match WHT by date, entity name, and currency
                var wht = parsed.Withholdings.FirstOrDefault(w =>
                    w.Date == div.Date &&
                    w.EntityName == div.EntityName &&
                    w.Currency == div.Currency);

                var info = await TaxCalculationService.CalculateAsync(
                    IncomeType.Dividend,
                    div.EntityName,
                    div.Date,
                    div.Amount,
                    div.Currency,
                    wht?.Amount ?? 0m,
                    wht?.Currency ?? div.Currency,
                    rateProvider,
                    ct);

                var exists = await _filingRepository.ExistsByIncomeAsync(
                    taxpayerProfileId, div.EntityName, div.Date, info.GrossIncomeRsd, ct);

                if (!exists)
                {
                    var deadline = FilingDeadlineCalculator.CalculateDeadline(div.Date, holidays);
                    var filing = Filing.CreateFromIncome(
                        taxpayerProfileId,
                        IncomeType.Dividend,
                        div.EntityName,
                        div.Date,
                        info.GrossIncomeRsd,
                        info.WhtPaidRsd,
                        info.GrossTaxPayableRsd,
                        info.TaxPayableRsd,
                        deadline,
                        report.Id);

                    await _filingRepository.AddAsync(filing, ct);
                    created++;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Dividend {div.EntityName} {div.Date}: {ex.Message}");
            }
        }

        // Process interest — credit entries only
        foreach (var interest in parsed.Interest.Where(i => i.Type == InterestType.Credit))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Match WHT by date and entity name (no currency filter for interest)
                var wht = parsed.Withholdings.FirstOrDefault(w =>
                    w.Date == interest.Date &&
                    w.EntityName == interest.EntityName);

                var info = await TaxCalculationService.CalculateAsync(
                    IncomeType.Interest,
                    interest.EntityName,
                    interest.Date,
                    interest.Amount,
                    interest.Currency,
                    wht?.Amount ?? 0m,
                    wht?.Currency ?? interest.Currency,
                    rateProvider,
                    ct);

                var exists = await _filingRepository.ExistsByIncomeAsync(
                    taxpayerProfileId, interest.EntityName, interest.Date, info.GrossIncomeRsd, ct);

                if (!exists)
                {
                    var deadline = FilingDeadlineCalculator.CalculateDeadline(interest.Date, holidays);
                    var filing = Filing.CreateFromIncome(
                        taxpayerProfileId,
                        IncomeType.Interest,
                        interest.EntityName,
                        interest.Date,
                        info.GrossIncomeRsd,
                        info.WhtPaidRsd,
                        info.GrossTaxPayableRsd,
                        info.TaxPayableRsd,
                        deadline,
                        report.Id);

                    await _filingRepository.AddAsync(filing, ct);
                    created++;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Interest {interest.EntityName} {interest.Date}: {ex.Message}");
            }
        }

        return created;
    }

    private Func<DateOnly, string, Task<ExchangeRate>> BuildRateProvider(
        StatementParseResult parsed,
        CancellationToken ct)
    {
        return async (date, currency) =>
        {
            var directResult = await _exchangeRateFetcher.FetchRateAsync(date, currency, ct);
            if (directResult.IsSuccess)
                return directResult.Value;

            // Cross-rate fallback: find IBKR rate for this currency to USD
            var ibkrRate = parsed.EmbeddedRates
                .FirstOrDefault(r => r.FromCurrency.Equals(currency, StringComparison.OrdinalIgnoreCase));
            if (ibkrRate != null)
            {
                var usdResult = await _exchangeRateFetcher.FetchRateAsync(date, "USD", ct);
                if (usdResult.IsSuccess)
                    return new ExchangeRate(date, currency, ibkrRate.Rate * usdResult.Value.RateToRsd);
            }

            throw new InvalidOperationException($"Exchange rate not found for {currency} on {date}");
        };
    }
}
