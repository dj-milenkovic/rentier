using Microsoft.Extensions.Logging;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Enums;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Services;

/// <summary>Encapsulates the shared result of a manual-filing tax calculation.</summary>
public sealed record ManualFilingCalculationResult(
    FilingInfo TaxInfo,
    RateResolution Rate,
    DateOnly Deadline,
    string TickerUpper);

/// <summary>
/// Validates inputs, loads holidays, resolves the NBS exchange rate, and computes PP-OPO tax
/// for a manual filing. Shared by <c>CalculateManualFilingCommandHandler</c> (preview only)
/// and <c>CreateManualFilingCommandHandler</c> (persists afterwards).
/// </summary>
public sealed class ManualFilingCalculator
{
    private readonly IExchangeRateResolver _exchangeRateResolver;
    private readonly IHolidayRepository _holidayRepository;
    private readonly ILogger<ManualFilingCalculator> _logger;

    public ManualFilingCalculator(
        IExchangeRateResolver exchangeRateResolver,
        IHolidayRepository holidayRepository,
        ILogger<ManualFilingCalculator>? logger = null)
    {
        _exchangeRateResolver = exchangeRateResolver;
        _holidayRepository = holidayRepository;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ManualFilingCalculator>.Instance;
    }

    /// <summary>
    /// Validates the inputs, resolves the exchange rate, and computes the tax.
    /// Returns a <see cref="ManualFilingCalculationResult"/> on success, or an
    /// <see cref="Error"/> describing the first validation or resolution failure.
    /// </summary>
    public async Task<Result<ManualFilingCalculationResult, Error>> CalculateAsync(
        IncomeType incomeType,
        string ticker,
        DateOnly incomeDate,
        string currency,
        decimal grossAmount,
        decimal? netReceived,
        CancellationToken ct = default)
    {
        // ── Validation ────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(ticker))
            return Result<ManualFilingCalculationResult, Error>.Failure(
                new Error("TICKER_REQUIRED", "Ticker is required"));

        if (grossAmount <= 0)
            return Result<ManualFilingCalculationResult, Error>.Failure(
                new Error("GROSS_REQUIRED", "Gross amount must be greater than zero"));

        if (incomeDate == default)
            return Result<ManualFilingCalculationResult, Error>.Failure(
                new Error("DATE_REQUIRED", "Income date is required"));

        if (netReceived.HasValue && netReceived.Value > grossAmount)
            return Result<ManualFilingCalculationResult, Error>.Failure(
                new Error("NET_EXCEEDS_GROSS", "Net received cannot exceed gross amount"));

        if (netReceived.HasValue && netReceived.Value < 0)
            return Result<ManualFilingCalculationResult, Error>.Failure(
                new Error("NET_NEGATIVE", "Net received cannot be negative"));

        try
        {
            // ── Load holidays ─────────────────────────────────────────────────
            var holidayDto = await _holidayRepository.GetHolidayConfAsync(ct);
            var holidays = new HolidayConf(holidayDto.Holidays.Select(h => h.Date).ToList());

            // ── Resolve NBS exchange rate ─────────────────────────────────────
            var rateResult = await _exchangeRateResolver.ResolveAsync(incomeDate, currency, holidays, ct: ct);
            if (!rateResult.IsSuccess)
                return Result<ManualFilingCalculationResult, Error>.Failure(
                    new Error("RATE_NOT_FOUND",
                        string.Format("Exchange rate not available for {0} on {1}", currency, incomeDate)));

            var resolution = rateResult.Value;

            // ── Compute WHT ──────────────────────────────────────────────────
            var wht = netReceived.HasValue ? grossAmount - netReceived.Value : 0m;
            var tickerUpper = ticker.Trim().ToUpperInvariant();

            // ── Tax calculation ──────────────────────────────────────────────
            var info = await TaxCalculationService.CalculateAsync(
                incomeType,
                tickerUpper,
                incomeDate,
                grossAmount,
                currency,
                wht,
                currency,
                (_, _) => Task.FromResult(resolution.Rate),
                ct);

            // ── Filing deadline ──────────────────────────────────────────────
            var deadline = FilingDeadlineCalculator.CalculateDeadline(incomeDate, holidays);

            return Result<ManualFilingCalculationResult, Error>.Success(
                new ManualFilingCalculationResult(info, resolution, deadline, tickerUpper));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network failure resolving NBS exchange rate for {Currency} on {Date}.", currency, incomeDate);
            return Result<ManualFilingCalculationResult, Error>.Failure(
                new Error("NETWORK_FAILURE",
                    "Could not reach NBS exchange rate service. Please check your connection."));
        }
    }
}
