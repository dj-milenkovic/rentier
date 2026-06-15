using FluentAssertions;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

public class MoneyTests
{
    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidArgs_SetsAmountAndCurrency()
    {
        var money = new Money(100m, "USD");

        money.Amount.Should().Be(100m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Constructor_LowercaseCurrency_UppercasesIt()
    {
        var money = new Money(50m, "eur");

        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Constructor_MixedCaseCurrency_UppercasesIt()
    {
        var money = new Money(50m, "Usd");

        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Constructor_ZeroAmount_DoesNotThrow()
    {
        var act = () => new Money(0m, "USD");

        act.Should().NotThrow();
    }

    // ── Currency validation ───────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceCurrency_ThrowsDomainException(string? currency)
    {
        var act = () => new Money(100m, currency!);

        act.Should().Throw<DomainException>().WithMessage("*Currency*");
    }

    // ── Amount validation ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NegativeAmount_ThrowsDomainException()
    {
        var act = () => new Money(-0.01m, "USD");

        act.Should().Throw<DomainException>().WithMessage("*negative*");
    }

    [Fact]
    public void Constructor_LargeNegativeAmount_ThrowsDomainException()
    {
        var act = () => new Money(-1000m, "RSD");

        act.Should().Throw<DomainException>().WithMessage("*negative*");
    }

    // ── Record equality ───────────────────────────────────────────────────────

    [Fact]
    public void TwoMoneyWithSameValues_AreEqual()
    {
        var a = new Money(100m, "USD");
        var b = new Money(100m, "USD");

        a.Should().Be(b);
    }

    [Fact]
    public void TwoMoneyWithDifferentAmounts_AreNotEqual()
    {
        var a = new Money(100m, "USD");
        var b = new Money(200m, "USD");

        a.Should().NotBe(b);
    }

    [Fact]
    public void TwoMoneyWithDifferentCurrencies_AreNotEqual()
    {
        var a = new Money(100m, "USD");
        var b = new Money(100m, "EUR");

        a.Should().NotBe(b);
    }

    [Fact]
    public void TwoMoneyWithSameCurrencyDifferentCase_AreEqual()
    {
        // Both are stored as uppercase, so "usd" and "USD" should be equal
        var a = new Money(100m, "usd");
        var b = new Money(100m, "USD");

        a.Should().Be(b);
    }
}
