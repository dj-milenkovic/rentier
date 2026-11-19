using FluentAssertions;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests.Domain.Services;

public class TaxCalculationServiceTests
{
    private static readonly DateOnly TestDate = new(2024, 1, 15);

    private static ExchangeRate Rate(decimal rateToRsd, string currency = "USD")
        => new(TestDate, currency, rateToRsd);

    [Fact]
    public void Calculate_Dividend_WithWht_ComputesAllAmountsCorrectly()
    {
        // income = 100 USD @ 117.21 RSD/USD
        // wht = 15 USD @ 117.21 RSD/USD
        // grossIncome = 11721.00
        // whtPaid = 1758.15
        // grossTax = Round(11721.00 * 0.15) = 1758.15
        // taxPayable = max(1758.15 - 1758.15, 0) = 0.00
        var result = TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, "AAPL", TestDate, 100m, "USD", 15m, "USD"), Rate(117.21m));

        result.GrossIncomeRsd.Should().Be(11721.00m);
        result.WhtPaidRsd.Should().Be(1758.15m);
        result.GrossTaxPayableRsd.Should().Be(1758.15m);
        result.TaxPayableRsd.Should().Be(0.00m);
        result.IncomeType.Should().Be(IncomeType.Dividend);
        result.PayingEntity.Should().Be("AAPL");
        result.IncomeDate.Should().Be(TestDate);
    }

    [Fact]
    public void Calculate_TaxRulesWorkedExample_TaxDue542_50()
    {
        // US dividend under the 10% treaty rate: $100 gross at NBS 108.50, $10 withheld.
        // gross 10,850.00 → gross tax 1,627.50 → WHT 1,085.00 → due 542.50 RSD.
        var result = TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, "AAPL", TestDate, 100m, "USD", 10m, "USD"), Rate(108.50m));

        result.TaxPayableRsd.Should().Be(542.50m);
    }

    [Fact]
    public void Calculate_WhtExceedsGrossTax_ClampsToZero()
    {
        // income = 100 @ 100 RSD → grossIncome = 10000, grossTax = 1500
        // wht = 20 @ 100 RSD → whtPaid = 2000 > 1500 → taxPayable = 0
        var result = TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, "MSFT", TestDate, 100m, "USD", 20m, "USD"), Rate(100m));

        result.WhtPaidRsd.Should().Be(2000.00m);
        result.GrossTaxPayableRsd.Should().Be(1500.00m);
        result.TaxPayableRsd.Should().Be(0.00m, because: "WHT credit cannot produce negative tax");
    }

    [Fact]
    public void Calculate_ZeroIncome_ReturnsAllZeros()
    {
        var result = TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Interest, "IBKR", TestDate, 0m, "USD", 0m, "USD"), Rate(117m));

        result.GrossIncomeRsd.Should().Be(0m);
        result.WhtPaidRsd.Should().Be(0m);
        result.GrossTaxPayableRsd.Should().Be(0m);
        result.TaxPayableRsd.Should().Be(0m);
    }

    [Fact]
    public void Calculate_ZeroWht_WhtPaidIsZero()
    {
        var result = TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, "AAPL", TestDate, 50m, "USD", 0m, "USD"), Rate(117m));

        result.WhtPaidRsd.Should().Be(0m);
    }

    [Fact]
    public void Calculate_NegativeIncome_ThrowsDomainException()
    {
        var act = () => TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, "AAPL", TestDate, -1m, "USD", 0m, "USD"), Rate(100m));

        act.Should().Throw<DomainException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public void Calculate_NegativeIncomeThatRoundsToZeroRsd_ThrowsDomainException()
    {
        // -0.001 × 1 rounds to 0.00 RSD, which would pass every downstream non-negative check
        // and silently produce a filing. Only the input guard can reject it.
        var act = () => TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, "AAPL", TestDate, -0.001m, "USD", 0m, "USD"), Rate(1m));

        act.Should().Throw<DomainException>()
            .WithMessage("*Income amount*negative*");
    }

    [Fact]
    public void Calculate_NegativeWht_ThrowsDomainException()
    {
        var act = () => TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, "AAPL", TestDate, 100m, "USD", -5m, "USD"), Rate(100m));

        act.Should().Throw<DomainException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public void Calculate_WhtCurrencyMismatch_ThrowsDomainException()
    {
        var act = () => TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, "AAPL", TestDate, 100m, "USD", 5m, "EUR"), Rate(100m));

        act.Should().Throw<DomainException>()
            .WithMessage("*WHT currency*");
    }

    [Fact]
    public void Calculate_ZeroWhtInDifferentCurrency_DoesNotThrow()
    {
        // The WHT/income currency match is only enforced when WHT was actually withheld.
        var act = () => TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Interest, "IBKR", TestDate, 100m, "USD", 0m, "EUR"), Rate(100m));

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Calculate_NullOrWhitespaceIncomeCurrency_ThrowsDomainException(string? currency)
    {
        var act = () => TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, "AAPL", TestDate, 100m, currency!, 0m, "USD"), Rate(100m));

        act.Should().Throw<DomainException>()
            .WithMessage("*IncomeCurrency*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Calculate_NullOrWhitespaceWhtCurrency_ThrowsDomainException(string? currency)
    {
        var act = () => TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, "AAPL", TestDate, 100m, "USD", 0m, currency!), Rate(100m));

        act.Should().Throw<DomainException>()
            .WithMessage("*WhtCurrency*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Calculate_NullOrWhitespacePayingEntity_ThrowsDomainException(string? entity)
    {
        var act = () => TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, entity!, TestDate, 100m, "USD", 0m, "USD"), Rate(100m));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Calculate_NullRate_ThrowsDomainException()
    {
        var act = () => TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, "AAPL", TestDate, 100m, "USD", 0m, "USD"), null!);

        act.Should().Throw<DomainException>()
            .WithMessage("*rate not found*");
    }

    [Fact]
    public void Calculate_RateForOtherCurrency_ThrowsDomainException()
    {
        var act = () => TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, "AAPL", TestDate, 100m, "USD", 0m, "USD"), Rate(117m, "EUR"));

        act.Should().Throw<DomainException>()
            .WithMessage("*income is in 'USD'*");
    }

    [Fact]
    public void Calculate_InterestType_PassesThroughToFilingInfo()
    {
        var result = TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Interest, "Interactive Brokers", TestDate, 10m, "EUR", 0m, "EUR"), Rate(117m, "EUR"));

        result.IncomeType.Should().Be(IncomeType.Interest);
        result.PayingEntity.Should().Be("Interactive Brokers");
    }

    [Fact]
    public void Calculate_RoundingHalfUp_RoundsAwayFromZero()
    {
        // income=1, rate=0.335 → grossIncome=Round(0.335,2)=0.34 (rounds .005 up)
        var result = TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, "TEST", TestDate, 1m, "USD", 0m, "USD"), Rate(0.335m));

        result.GrossIncomeRsd.Should().Be(0.34m);
    }

    [Fact]
    public void Calculate_LowercaseIncomeCurrency_MatchesUppercaseRate()
    {
        var result = TaxCalculationService.Calculate(new IncomeTaxInput(IncomeType.Dividend, "AAPL", TestDate, 100m, "usd", 0m, "usd"), Rate(117m, "USD"));

        result.GrossIncomeRsd.Should().Be(11700m);
    }
}
