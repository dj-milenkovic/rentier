using FluentAssertions;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.Domain.Tests.Services;

public class TaxCalculationServiceTests
{
    private static readonly DateOnly TestDate = new(2024, 1, 15);

    private static Func<DateOnly, string, Task<ExchangeRate>> FixedRate(decimal rateToRsd)
        => (date, currency) => Task.FromResult(new ExchangeRate(date, currency, rateToRsd));

    [Fact]
    public async Task CalculateAsync_Dividend_WithWht_ComputesAllAmountsCorrectly()
    {
        // income = 100 USD @ 117.21 RSD/USD
        // wht = 15 USD @ 117.21 RSD/USD
        // grossIncome = 11721.00
        // whtPaid = 1758.15
        // grossTax = Round(11721.00 * 0.15) = 1758.15
        // taxPayable = max(1758.15 - 1758.15, 0) = 0.00
        var result = await TaxCalculationService.CalculateAsync(
            IncomeType.Dividend, "AAPL", TestDate,
            100m, "USD", 15m, "USD",
            FixedRate(117.21m));

        result.GrossIncomeRsd.Should().Be(11721.00m);
        result.WhtPaidRsd.Should().Be(1758.15m);
        result.GrossTaxPayableRsd.Should().Be(1758.15m);
        result.TaxPayableRsd.Should().Be(0.00m);
        result.IncomeType.Should().Be(IncomeType.Dividend);
        result.PayingEntity.Should().Be("AAPL");
        result.IncomeDate.Should().Be(TestDate);
    }

    [Fact]
    public async Task CalculateAsync_WhtExceedsGrossTax_ClampsToZero()
    {
        // income = 100 @ 100 RSD → grossIncome = 10000, grossTax = 1500
        // wht = 20 @ 100 RSD → whtPaid = 2000 > 1500 → taxPayable = 0
        var result = await TaxCalculationService.CalculateAsync(
            IncomeType.Dividend, "MSFT", TestDate,
            100m, "USD", 20m, "USD",
            FixedRate(100m));

        result.WhtPaidRsd.Should().Be(2000.00m);
        result.GrossTaxPayableRsd.Should().Be(1500.00m);
        result.TaxPayableRsd.Should().Be(0.00m, because: "WHT credit cannot produce negative tax");
    }

    [Fact]
    public async Task CalculateAsync_ZeroIncome_ReturnsAllZeros()
    {
        var result = await TaxCalculationService.CalculateAsync(
            IncomeType.Interest, "IBKR", TestDate,
            0m, "USD", 0m, "USD",
            FixedRate(117m));

        result.GrossIncomeRsd.Should().Be(0m);
        result.WhtPaidRsd.Should().Be(0m);
        result.GrossTaxPayableRsd.Should().Be(0m);
        result.TaxPayableRsd.Should().Be(0m);
    }

    [Fact]
    public async Task CalculateAsync_ZeroWht_DoesNotCallRateProviderTwice()
    {
        int callCount = 0;
        Func<DateOnly, string, Task<ExchangeRate>> countingProvider = (date, currency) =>
        {
            callCount++;
            return Task.FromResult(new ExchangeRate(date, currency, 117m));
        };

        await TaxCalculationService.CalculateAsync(
            IncomeType.Dividend, "AAPL", TestDate,
            50m, "USD", 0m, "USD",
            countingProvider);

        callCount.Should().Be(1, because: "zero WHT should skip WHT rate call");
    }

    [Fact]
    public async Task CalculateAsync_NegativeIncome_ThrowsDomainException()
    {
        var act = () => TaxCalculationService.CalculateAsync(
            IncomeType.Dividend, "AAPL", TestDate,
            -1m, "USD", 0m, "USD", FixedRate(100m));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public async Task CalculateAsync_NegativeWht_ThrowsDomainException()
    {
        var act = () => TaxCalculationService.CalculateAsync(
            IncomeType.Dividend, "AAPL", TestDate,
            100m, "USD", -5m, "USD", FixedRate(100m));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public async Task CalculateAsync_WhtCurrencyMismatch_ThrowsDomainException()
    {
        var act = () => TaxCalculationService.CalculateAsync(
            IncomeType.Dividend, "AAPL", TestDate,
            100m, "USD", 5m, "EUR", FixedRate(100m));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*WHT currency*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CalculateAsync_NullOrWhitespacePayingEntity_ThrowsDomainException(string? entity)
    {
        var act = () => TaxCalculationService.CalculateAsync(
            IncomeType.Dividend, entity!, TestDate,
            100m, "USD", 0m, "USD", FixedRate(100m));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CalculateAsync_NullRateProvider_ThrowsDomainException()
    {
        var act = () => TaxCalculationService.CalculateAsync(
            IncomeType.Dividend, "AAPL", TestDate,
            100m, "USD", 0m, "USD", null!);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*provider*");
    }

    [Fact]
    public async Task CalculateAsync_InterestType_PassesThroughToFilingInfo()
    {
        var result = await TaxCalculationService.CalculateAsync(
            IncomeType.Interest, "Interactive Brokers", TestDate,
            10m, "EUR", 0m, "EUR", FixedRate(117m));

        result.IncomeType.Should().Be(IncomeType.Interest);
        result.PayingEntity.Should().Be("Interactive Brokers");
    }

    [Fact]
    public async Task CalculateAsync_RoundingHalfUp_RoundsAwayFromZero()
    {
        // 100 * 0.005 = 0.5 per unit → total = some value ending in .5 cents
        // Use rate that produces a .005 boundary: income=1, rate=100/3 → 33.3333...
        // Round(33.333... * 0.15) = Round(5.0000) = 5.00 — not a good boundary test
        // Better: income=1, rate=0.335 → grossIncome=Round(0.335,2)=0.34 (rounds .005 up)
        var result = await TaxCalculationService.CalculateAsync(
            IncomeType.Dividend, "TEST", TestDate,
            1m, "USD", 0m, "USD",
            (_, _) => Task.FromResult(new ExchangeRate(TestDate, "USD", 0.335m)));

        // Round(1 * 0.335, 2, AwayFromZero) = Round(0.335, 2) = 0.34
        result.GrossIncomeRsd.Should().Be(0.34m);
    }

    [Fact]
    public async Task CalculateAsync_CurrencyNormalized_UpperCase()
    {
        // Pass lowercase currency — should be normalized to uppercase for rate provider
        string? receivedCurrency = null;
        Func<DateOnly, string, Task<ExchangeRate>> capturingProvider = (date, currency) =>
        {
            receivedCurrency = currency;
            return Task.FromResult(new ExchangeRate(date, currency, 117m));
        };

        await TaxCalculationService.CalculateAsync(
            IncomeType.Dividend, "AAPL", TestDate,
            100m, "usd", 0m, "usd", capturingProvider);

        receivedCurrency.Should().Be("USD");
    }
}
