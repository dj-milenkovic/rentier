using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;

namespace Rentier.Domain.Services;

/// <summary>
/// Computes PP-OPO passive income tax for a single income event.
/// All amounts decimal, all dates DateOnly per constitution.
/// The caller resolves the exchange rate (NBS lookup, walk-back and cross-rate policy live in
/// the Application layer); this is pure arithmetic over that rate.
/// </summary>
public static class TaxCalculationService
{
    private const decimal TaxRate = 0.15m;

    /// <summary>
    /// Computes, in this order and rounding each step before the next:
    /// gross income RSD, WHT paid RSD, gross tax (15%) and tax payable (gross tax − WHT, floored at 0).
    /// </summary>
    /// <param name="input">The income event. WHT, when present, must be in the income currency.</param>
    /// <param name="rate">The exchange rate for the income currency; applied to both income and WHT.</param>
    /// <exception cref="DomainException">Invalid input, a currency mismatch, or a missing rate.</exception>
    public static FilingInfo Calculate(IncomeTaxInput input, ExchangeRate rate)
    {
        if (string.IsNullOrWhiteSpace(input.PayingEntity))
            throw new DomainException("PayingEntity must not be empty");
        if (string.IsNullOrWhiteSpace(input.IncomeCurrency))
            throw new DomainException("IncomeCurrency must not be empty");
        if (string.IsNullOrWhiteSpace(input.WhtCurrency))
            throw new DomainException("WhtCurrency must not be empty");
        if (input.IncomeAmount < 0)
            throw new DomainException("Income amount must not be negative");
        if (input.WhtAmount < 0)
            throw new DomainException("WHT amount must not be negative");
        if (rate is null)
            throw new DomainException(
                $"Exchange rate not found for currency '{input.IncomeCurrency}' on {input.IncomeDate}");

        var upperIncome = input.IncomeCurrency.ToUpperInvariant();
        var upperWht = input.WhtCurrency.ToUpperInvariant();

        if (input.WhtAmount > 0 && upperWht != upperIncome)
            throw new DomainException("WHT currency must match income currency");
        if (!string.Equals(rate.Currency, upperIncome, StringComparison.OrdinalIgnoreCase))
            throw new DomainException(
                $"Exchange rate is for '{rate.Currency}' but the income is in '{upperIncome}'");

        var grossIncomeRsd = Round(input.IncomeAmount * rate.RateToRsd);
        var whtPaidRsd = input.WhtAmount > 0 ? Round(input.WhtAmount * rate.RateToRsd) : 0m;
        var grossTaxPayableRsd = Round(grossIncomeRsd * TaxRate);
        var taxPayableRsd = Math.Max(grossTaxPayableRsd - whtPaidRsd, 0m);

        return new FilingInfo(
            input.IncomeType,
            input.PayingEntity,
            input.IncomeDate,
            grossIncomeRsd,
            whtPaidRsd,
            grossTaxPayableRsd,
            taxPayableRsd);
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
