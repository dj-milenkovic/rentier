using FluentAssertions;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

public class ExchangeRateTests
{
    private static readonly DateOnly TestDate = new(2024, 6, 17);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceCurrency_ThrowsDomainException(string? currency)
    {
        var act = () => new ExchangeRate(TestDate, currency!, 117.21m);

        act.Should().Throw<DomainException>().WithMessage("*Currency*");
    }

    [Fact]
    public void Constructor_ZeroRate_ThrowsDomainException()
    {
        var act = () => new ExchangeRate(TestDate, "USD", 0m);

        act.Should().Throw<DomainException>().WithMessage("*RateToRsd*");
    }

    [Fact]
    public void Constructor_NegativeRate_ThrowsDomainException()
    {
        var act = () => new ExchangeRate(TestDate, "USD", -1m);

        act.Should().Throw<DomainException>().WithMessage("*RateToRsd*");
    }

    [Fact]
    public void Constructor_ValidInputs_NormalizesCurrencyToUppercase()
    {
        var rate = new ExchangeRate(TestDate, "usd", 117.21m);

        rate.Currency.Should().Be("USD");
    }

    [Fact]
    public void Constructor_ValidInputs_SetsDateProperty()
    {
        var rate = new ExchangeRate(TestDate, "USD", 117.21m);

        rate.Date.Should().Be(TestDate);
    }

    [Fact]
    public void Constructor_ValidInputs_SetsRateToRsdProperty()
    {
        var rate = new ExchangeRate(TestDate, "USD", 117.21m);

        rate.RateToRsd.Should().Be(117.21m);
    }
}
