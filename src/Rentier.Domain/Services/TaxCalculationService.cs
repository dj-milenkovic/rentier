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
        IncomeTaxInput input,
        Func<DateOnly, string, Task<ExchangeRate>> rateProvider,
        CancellationToken ct = default)
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
        if (rateProvider is null)
            throw new DomainException("Rate provider must not be null");

        var upperIncome = input.IncomeCurrency.ToUpperInvariant();
        var upperWht = input.WhtCurrency.ToUpperInvariant();

        if (input.WhtAmount > 0 && upperWht != upperIncome)
            throw new DomainException("WHT currency must match income currency");

        var incomeRate = await rateProvider(input.IncomeDate, upperIncome);
        if (incomeRate is null)
            throw new DomainException($"Exchange rate not found for currency '{upperIncome}' on {input.IncomeDate}");
        ct.ThrowIfCancellationRequested();

        var grossIncomeRsd = Round(input.IncomeAmount * incomeRate.RateToRsd);

        decimal whtPaidRsd = 0m;
        if (input.WhtAmount > 0)
        {
            var whtRate = (upperWht == upperIncome) ? incomeRate : await rateProvider(input.IncomeDate, upperWht);
            if (whtRate is null)
                throw new DomainException($"Exchange rate not found for WHT currency '{upperWht}' on {input.IncomeDate}");
            ct.ThrowIfCancellationRequested();
            whtPaidRsd = Round(input.WhtAmount * whtRate.RateToRsd);
        }

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
