using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Parsing;
using Rentier.Application.Services;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// The broker cross-rate policy at the resolver's interface: broker statement rates apply only to
/// currencies NBS does not publish, pivot through the statement's ToCurrency, and are tagged CrossRate.
/// </summary>
public class ExchangeRateResolverCrossRateTests
{
    private static readonly DateOnly Date = new(2024, 6, 13); // Thursday

    private static HolidayConf NoHolidays() => new(new List<DateOnly>());

    private static ExchangeRateResolver MakeResolver(IExchangeRateFetcher fetcher)
        => new(fetcher, NullLogger<ExchangeRateResolver>.Instance);

    private static IExchangeRateFetcher FetcherWhere(string unsupported, string pivot, decimal pivotRate)
    {
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), unsupported, Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error(ErrorCodes.UNSUPPORTED_CURRENCY, "not published")));
        fetcher.FetchRateAsync(Date, pivot, Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(Date, pivot, pivotRate)));
        return fetcher;
    }

    [Fact]
    public async Task ResolveAsync_UnsupportedCurrencyWithBrokerRate_SynthesizesRateThroughPivot()
    {
        var fetcher = FetcherWhere("MXN", "USD", 108m);
        IReadOnlyList<IbkrExchangeRate> broker = [new(Date, "MXN", "USD", 0.05m)];

        var result = await MakeResolver(fetcher).ResolveAsync(
            Date, "MXN", NoHolidays(), brokerRates: broker, ct: TestContext.Current.CancellationToken);

        result.Value.Rate.RateToRsd.Should().Be(5.4m); // 0.05 × 108
    }

    [Fact]
    public async Task ResolveAsync_UnsupportedCurrencyWithBrokerRate_TaggedCrossRate()
    {
        var fetcher = FetcherWhere("MXN", "USD", 108m);
        IReadOnlyList<IbkrExchangeRate> broker = [new(Date, "MXN", "USD", 0.05m)];

        var result = await MakeResolver(fetcher).ResolveAsync(
            Date, "MXN", NoHolidays(), brokerRates: broker, ct: TestContext.Current.CancellationToken);

        result.Value.SourceType.Should().Be(ExchangeRateSourceType.CrossRate);
    }

    [Fact]
    public async Task ResolveAsync_EurBaseCurrencyStatement_PivotsThroughEurNotUsd()
    {
        // An account with EUR base currency reports MXN→EUR; pivoting through USD would be wrong.
        var fetcher = FetcherWhere("MXN", "EUR", 117m);
        IReadOnlyList<IbkrExchangeRate> broker = [new(Date, "MXN", "EUR", 0.05m)];

        var result = await MakeResolver(fetcher).ResolveAsync(
            Date, "MXN", NoHolidays(), brokerRates: broker, ct: TestContext.Current.CancellationToken);

        result.Value.Rate.RateToRsd.Should().Be(5.85m); // 0.05 × 117
    }

    [Fact]
    public async Task ResolveAsync_NbsCurrencyWithNoRateInLookback_DoesNotUseBrokerRate()
    {
        // USD is published by NBS: running out of look-back days must fail, not silently switch
        // to the broker's rate (NBS is authoritative for the currencies it publishes).
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error(ErrorCodes.RATE_NOT_FOUND, "none")));
        IReadOnlyList<IbkrExchangeRate> broker = [new(Date, "USD", "EUR", 0.9m)];

        var result = await MakeResolver(fetcher).ResolveAsync(
            Date, "USD", NoHolidays(), maxLookbackDays: 2, brokerRates: broker,
            ct: TestContext.Current.CancellationToken);

        result.Error.Code.Should().Be(ErrorCodes.RATE_NOT_FOUND);
    }

    [Fact]
    public async Task ResolveAsync_UnsupportedCurrencyWithoutBrokerRate_ReturnsUnsupported()
    {
        var fetcher = FetcherWhere("MXN", "USD", 108m);

        var result = await MakeResolver(fetcher).ResolveAsync(
            Date, "MXN", NoHolidays(), brokerRates: [], ct: TestContext.Current.CancellationToken);

        result.Error.Code.Should().Be(ErrorCodes.UNSUPPORTED_CURRENCY);
    }

    [Fact]
    public async Task ResolveAsync_BrokerRateForEventDate_PreferredOverOtherDates()
    {
        var fetcher = FetcherWhere("MXN", "USD", 100m);
        IReadOnlyList<IbkrExchangeRate> broker =
        [
            new(Date.AddDays(-30), "MXN", "USD", 0.04m),
            new(Date, "MXN", "USD", 0.05m),
        ];

        var result = await MakeResolver(fetcher).ResolveAsync(
            Date, "MXN", NoHolidays(), brokerRates: broker, ct: TestContext.Current.CancellationToken);

        result.Value.Rate.RateToRsd.Should().Be(5m);
    }
}
