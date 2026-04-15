using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Rentier.Domain.Enums;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;

namespace Rentier.UnitTests;

/// <summary>
/// Property-based tests for TaxCalculationService financial invariants.
/// Uses FsCheck to generate random inputs and verify that invariants hold for all cases.
/// </summary>
public class TaxCalculationProperties
{
    private const decimal FixedRateToRsd = 117.21m;
    private static readonly DateOnly FixedIncomeDate = new(2024, 1, 15);

    /// <summary>
    /// Creates a fake rate provider that always returns a fixed exchange rate.
    /// </summary>
    private static Func<DateOnly, string, Task<ExchangeRate>> CreateFixedRateProvider() =>
        (date, currency) => Task.FromResult(new ExchangeRate(date, currency, FixedRateToRsd));

    /// <summary>
    /// Verifies that TaxPayableRsd is never negative for any valid positive income and WHT amounts.
    /// This is a fundamental financial invariant: you cannot owe negative tax.
    /// </summary>
    [Property]
    public Property TaxPayable_IsNeverNegative(PositiveInt incomeInt, PositiveInt whtInt)
    {
        var incomeAmount = incomeInt.Get / 100m;
        var whtAmount = whtInt.Get / 100m;

        var result = TaxCalculationService.CalculateAsync(
            IncomeType.Dividend,
            "TEST_ENTITY",
            FixedIncomeDate,
            incomeAmount,
            "USD",
            whtAmount,
            "USD",
            CreateFixedRateProvider())
            .GetAwaiter().GetResult();

        return (result.TaxPayableRsd >= 0m).ToProperty();
    }

    /// <summary>
    /// Verifies that TaxPayableRsd never exceeds GrossTaxPayableRsd.
    /// The tax payable after WHT credit cannot be more than the gross tax liability.
    /// </summary>
    [Property]
    public Property TaxPayable_NeverExceedsGrossTax(PositiveInt incomeInt, PositiveInt whtInt)
    {
        var incomeAmount = incomeInt.Get / 100m;
        var whtAmount = whtInt.Get / 100m;

        var result = TaxCalculationService.CalculateAsync(
            IncomeType.Dividend,
            "TEST_ENTITY",
            FixedIncomeDate,
            incomeAmount,
            "USD",
            whtAmount,
            "USD",
            CreateFixedRateProvider())
            .GetAwaiter().GetResult();

        return (result.TaxPayableRsd <= result.GrossTaxPayableRsd).ToProperty();
    }

    /// <summary>
    /// Verifies that GrossIncomeRsd is always rounded to at most 2 decimal places.
    /// Financial amounts in RSD must have dinar precision (2 decimal places).
    /// </summary>
    [Property]
    public Property GrossIncome_RoundedToTwoDecimalPlaces(PositiveInt incomeInt, PositiveInt rateInt)
    {
        var incomeAmount = incomeInt.Get / 100m;
        // Ensure rate is positive and varied
        var rate = (rateInt.Get % 1000) / 100m + 0.01m;

        var result = TaxCalculationService.CalculateAsync(
            IncomeType.Interest,
            "TEST_ENTITY",
            FixedIncomeDate,
            incomeAmount,
            "EUR",
            0m,
            "EUR",
            (date, currency) => Task.FromResult(new ExchangeRate(date, currency, rate)))
            .GetAwaiter().GetResult();

        // Check that value equals itself when rounded to 2 decimal places
        var rounded = Math.Round(result.GrossIncomeRsd, 2, MidpointRounding.AwayFromZero);
        return (result.GrossIncomeRsd == rounded).ToProperty();
    }
}
