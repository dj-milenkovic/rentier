using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Application.Services;
using Rentier.Domain.Entities;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Handlers;

/// <summary>
/// Validates inputs, resolves exchange rate, computes tax, checks for duplicate filings,
/// persists the new Filing, and returns its ID.
/// </summary>
public sealed class CreateManualFilingCommandHandler
    : ICommandHandler<CreateManualFilingCommand, Result<Guid, Error>>
{
    private readonly ExchangeRateResolver  _exchangeRateResolver;
    private readonly IHolidayRepository   _holidayRepository;
    private readonly IFilingRepository    _filingRepository;

    public CreateManualFilingCommandHandler(
        ExchangeRateResolver  exchangeRateResolver,
        IHolidayRepository   holidayRepository,
        IFilingRepository    filingRepository)
    {
        _exchangeRateResolver = exchangeRateResolver;
        _holidayRepository   = holidayRepository;
        _filingRepository    = filingRepository;
    }

    public async Task<Result<Guid, Error>> HandleAsync(
        CreateManualFilingCommand command, CancellationToken ct = default)
    {
        // ── Validation ────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(command.Ticker))
            return Result<Guid, Error>.Failure(
                new Error("TICKER_REQUIRED", "Ticker is required"));

        if (command.GrossAmount <= 0)
            return Result<Guid, Error>.Failure(
                new Error("GROSS_REQUIRED", "Gross amount must be greater than zero"));

        if (command.IncomeDate == default)
            return Result<Guid, Error>.Failure(
                new Error("DATE_REQUIRED", "Income date is required"));

        if (command.NetReceived.HasValue && command.NetReceived.Value > command.GrossAmount)
            return Result<Guid, Error>.Failure(
                new Error("NET_EXCEEDS_GROSS", "Net received cannot exceed gross amount"));

        if (command.NetReceived.HasValue && command.NetReceived.Value < 0)
            return Result<Guid, Error>.Failure(
                new Error("NET_NEGATIVE", "Net received cannot be negative"));

        try
        {
            // ── Load holidays ─────────────────────────────────────────────────
            var holidayDto = await _holidayRepository.GetHolidayConfAsync(ct);
            var holidays   = new HolidayConf(holidayDto.Holidays.Select(h => h.Date).ToList());

            // ── Resolve NBS exchange rate ─────────────────────────────────────
            var rateResult = await _exchangeRateResolver.ResolveAsync(
                command.IncomeDate, command.Currency, holidays, ct: ct);

            if (!rateResult.IsSuccess)
                return Result<Guid, Error>.Failure(
                    new Error("RATE_NOT_FOUND",
                        string.Format("Exchange rate not available for {0} on {1}",
                            command.Currency, command.IncomeDate)));

            var resolution = rateResult.Value;

            // ── Compute WHT ──────────────────────────────────────────────────
            var wht         = command.NetReceived.HasValue
                ? command.GrossAmount - command.NetReceived.Value
                : 0m;
            var tickerUpper = command.Ticker.Trim().ToUpperInvariant();

            // ── Tax calculation ──────────────────────────────────────────────
            var info = await TaxCalculationService.CalculateAsync(
                command.IncomeType,
                tickerUpper,
                command.IncomeDate,
                command.GrossAmount,
                command.Currency,
                wht,
                command.Currency,
                (_, _) => Task.FromResult(resolution.Rate),
                ct);

            // ── Duplicate check ──────────────────────────────────────────────
            var exists = await _filingRepository.ExistsByIncomeAsync(
                command.TaxpayerProfileId, tickerUpper, command.IncomeDate, info.GrossIncomeRsd, ct);

            if (exists)
                return Result<Guid, Error>.Failure(
                    new Error("DUPLICATE_FILING",
                        "A filing with the same details already exists"));

            // ── Filing deadline ──────────────────────────────────────────────
            var deadline = FilingDeadlineCalculator.CalculateDeadline(command.IncomeDate, holidays);

            // ── Persist filing ───────────────────────────────────────────────
            var filing = Filing.CreateFromIncome(
                command.TaxpayerProfileId,
                command.IncomeType,
                tickerUpper,
                command.IncomeDate,
                info.GrossIncomeRsd,
                info.WhtPaidRsd,
                info.GrossTaxPayableRsd,
                info.TaxPayableRsd,
                deadline,
                reportId:                null,
                exchangeRateSourceDate:  resolution.SourceDate,
                exchangeRateSourceType:  resolution.SourceType,
                ticker:                  tickerUpper);

            await _filingRepository.AddAsync(filing, ct);

            return Result<Guid, Error>.Success(filing.Id);
        }
        catch (HttpRequestException)
        {
            return Result<Guid, Error>.Failure(
                new Error("NETWORK_FAILURE",
                    "Could not reach NBS exchange rate service. Please check your connection."));
        }
    }
}
