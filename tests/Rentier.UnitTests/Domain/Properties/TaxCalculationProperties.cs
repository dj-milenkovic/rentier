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

    // ── T003: Exchange-rate conversion rounding invariants ────────────────────

    /// <summary>
    /// Verifies that Math.Round(amount * rate, 2, AwayFromZero) produces a result
    /// with at most 2 decimal places and is within 0.005m of the original product.
    /// This is the fundamental rounding invariant for RSD conversion.
    /// </summary>
    [Property]
    public Property ConvertToRsd_AnyPositiveAmountAndRate_ResultIsRoundedToTwoDecimalPlaces(
        PositiveInt amountInt, PositiveInt rateInt)
    {
        var amount = amountInt.Get / 100m;
        var rate = (rateInt.Get % 10000) / 100m + 0.01m;
        var product = amount * rate;

        var result = Math.Round(product, 2, MidpointRounding.AwayFromZero);

        // Idempotent: rounding the result again must yield the same value
        bool idempotent = Math.Round(result, 2, MidpointRounding.AwayFromZero) == result;

        // Within half-a-cent of the original product
        bool withinBound = Math.Abs(result - product) <= 0.005m;

        return (idempotent && withinBound).ToProperty();
    }

    /// <summary>
    /// Verifies the rounding invariant holds even for extreme exchange rates
    /// (very small like 0.0001 or very large like 99999.99).
    /// </summary>
    [Property]
    public Property ConvertToRsd_ExtremeRates_ResultIsStillRoundedToTwoDecimalPlaces(
        PositiveInt amountInt)
    {
        var amount = amountInt.Get / 100m;

        var extremeRates = new[] { 0.0001m, 0.001m, 99999.99m };

        return Prop.ForAll(
            Gen.Elements(extremeRates).ToArbitrary(),
            rate =>
            {
                var product = amount * rate;
                var result = Math.Round(product, 2, MidpointRounding.AwayFromZero);
                var roundedAgain = Math.Round(result, 2, MidpointRounding.AwayFromZero);
                return result == roundedAgain;
            });
    }

    // ── T004: Report aggregation sum consistency ──────────────────────────────

    /// <summary>
    /// Verifies that decimal-based sum aggregation produces no drift:
    /// LINQ Sum() and manual Aggregate() always agree, the total is non-negative
    /// when all inputs are non-negative, and total is zero iff all items are zero.
    /// </summary>
    [Property]
    public Property AggregateAmounts_AnyCollection_TotalEqualsSumOfIndividuals(
        NonNegativeInt[] rawAmounts)
    {
        var amounts = rawAmounts.Take(50).Select(n => n.Get / 100m).ToList();

        var totalByLinqSum = amounts.Sum();
        var totalByManualAccumulation = amounts.Aggregate(0m, (acc, x) => acc + x);

        // Both aggregation methods produce identical results (no decimal drift)
        bool sumsEqual = totalByLinqSum == totalByManualAccumulation;

        // Non-negative input → non-negative total
        bool totalNonNegative = totalByLinqSum >= 0m;

        // Total is zero iff every item is zero
        bool zeroConsistency = (totalByLinqSum == 0m) == amounts.All(a => a == 0m);

        return (sumsEqual && totalNonNegative && zeroConsistency).ToProperty();
    }
}
