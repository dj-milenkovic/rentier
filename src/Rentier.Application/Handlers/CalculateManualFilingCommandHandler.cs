using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Services;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Handlers;

/// <summary>
/// Validates inputs, resolves NBS exchange rate, computes PP-OPO tax, and returns a preview DTO.
/// No data is persisted — this is a pure calculation step.
/// </summary>
public sealed class CalculateManualFilingCommandHandler
    : ICommandHandler<CalculateManualFilingCommand, Result<ManualFilingPreviewDto, Error>>
{
    private readonly ExchangeRateResolver _exchangeRateResolver;
    private readonly IHolidayRepository  _holidayRepository;

    public CalculateManualFilingCommandHandler(
        ExchangeRateResolver exchangeRateResolver,
        IHolidayRepository   holidayRepository)
    {
        _exchangeRateResolver = exchangeRateResolver;
        _holidayRepository    = holidayRepository;
    }

    public async Task<Result<ManualFilingPreviewDto, Error>> HandleAsync(
        CalculateManualFilingCommand command, CancellationToken ct = default)
    {
        // ── Validation ────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(command.Ticker))
            return Result<ManualFilingPreviewDto, Error>.Failure(
                new Error("TICKER_REQUIRED", "Ticker is required"));

        if (command.GrossAmount <= 0)
            return Result<ManualFilingPreviewDto, Error>.Failure(
                new Error("GROSS_REQUIRED", "Gross amount must be greater than zero"));

        if (command.IncomeDate == default)
            return Result<ManualFilingPreviewDto, Error>.Failure(
                new Error("DATE_REQUIRED", "Income date is required"));

        if (command.NetReceived.HasValue && command.NetReceived.Value > command.GrossAmount)
            return Result<ManualFilingPreviewDto, Error>.Failure(
                new Error("NET_EXCEEDS_GROSS", "Net received cannot exceed gross amount"));

        if (command.NetReceived.HasValue && command.NetReceived.Value < 0)
            return Result<ManualFilingPreviewDto, Error>.Failure(
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
                return Result<ManualFilingPreviewDto, Error>.Failure(
                    new Error("RATE_NOT_FOUND",
                        string.Format("Exchange rate not available for {0} on {1}",
                            command.Currency, command.IncomeDate)));

            var resolution = rateResult.Value;

            // ── Compute WHT ──────────────────────────────────────────────────
            var wht = command.NetReceived.HasValue
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

            // ── Filing deadline ──────────────────────────────────────────────
            var deadline = FilingDeadlineCalculator.CalculateDeadline(command.IncomeDate, holidays);

            return Result<ManualFilingPreviewDto, Error>.Success(new ManualFilingPreviewDto(
                info.GrossIncomeRsd,
                info.WhtPaidRsd,
                info.GrossTaxPayableRsd,
                info.TaxPayableRsd,
                deadline,
                resolution.Rate.RateToRsd,
                resolution.SourceDate,
                resolution.SourceType));
        }
        catch (HttpRequestException)
        {
            return Result<ManualFilingPreviewDto, Error>.Failure(
                new Error("NETWORK_FAILURE",
                    "Could not reach NBS exchange rate service. Please check your connection."));
        }
    }
}
