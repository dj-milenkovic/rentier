using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;

namespace Rentier.Domain.Services;

/// <summary>
/// Computes PP-OPO passive income tax for a single income event.
/// All amounts decimal, all dates DateOnly per constitution.
/// Rate provider is caller-supplied; cross-rate logic is caller responsibility.
/// </summary>
public static class TaxCalculationService
{
    private const decimal TaxRate = 0.15m;

    public static async Task<FilingInfo> CalculateAsync(
        IncomeType incomeType,
        string payingEntity,
        DateOnly incomeDate,
        decimal incomeAmount,
        string incomeCurrency,
        decimal whtAmount,
        string whtCurrency,
        Func<DateOnly, string, Task<ExchangeRate>> rateProvider,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(payingEntity))
            throw new DomainException("PayingEntity must not be empty");
        if (string.IsNullOrWhiteSpace(incomeCurrency))
            throw new DomainException("IncomeCurrency must not be empty");
        if (string.IsNullOrWhiteSpace(whtCurrency))
            throw new DomainException("WhtCurrency must not be empty");
        if (incomeAmount < 0)
            throw new DomainException("Income amount must not be negative");
        if (whtAmount < 0)
            throw new DomainException("WHT amount must not be negative");
        if (rateProvider is null)
            throw new DomainException("Rate provider must not be null");

        var upperIncome = incomeCurrency.ToUpperInvariant();
        var upperWht    = whtCurrency.ToUpperInvariant();

        if (whtAmount > 0 && upperWht != upperIncome)
            throw new DomainException("WHT currency must match income currency");

        var incomeRate = await rateProvider(incomeDate, upperIncome);
        if (incomeRate is null)
            throw new DomainException($"Exchange rate not found for currency '{upperIncome}' on {incomeDate}");
        ct.ThrowIfCancellationRequested();

        var grossIncomeRsd = Round(incomeAmount * incomeRate.RateToRsd);

        decimal whtPaidRsd = 0m;
        if (whtAmount > 0)
        {
            var whtRate = (upperWht == upperIncome) ? incomeRate : await rateProvider(incomeDate, upperWht);
            if (whtRate is null)
                throw new DomainException($"Exchange rate not found for WHT currency '{upperWht}' on {incomeDate}");
            ct.ThrowIfCancellationRequested();
            whtPaidRsd = Round(whtAmount * whtRate.RateToRsd);
        }

        var grossTaxPayableRsd = Round(grossIncomeRsd * TaxRate);
        var taxPayableRsd      = Math.Max(grossTaxPayableRsd - whtPaidRsd, 0m);

        return new FilingInfo(
            incomeType,
            payingEntity,
            incomeDate,
            grossIncomeRsd,
            whtPaidRsd,
            grossTaxPayableRsd,
            taxPayableRsd);
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
