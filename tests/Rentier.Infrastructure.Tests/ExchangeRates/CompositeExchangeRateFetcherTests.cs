using FluentAssertions;
using NSubstitute;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.ExchangeRates;
using Xunit;

namespace Rentier.Infrastructure.Tests.ExchangeRates;

[Trait("Category", "Integration")]
public class CompositeExchangeRateFetcherTests
{
    private static readonly DateOnly TestDate = new(2024, 1, 15);

    private static CompositeExchangeRateFetcher MakeSut(
        IExchangeRateFetcher primary,
        IExchangeRateFetcher secondary)
        => new(primary, secondary);

    [Fact]
    public async Task GetRateAsync_PrimaryFetcherSucceeds_ReturnsPrimaryResult()
    {
        var primary = Substitute.For<IExchangeRateFetcher>();
        var secondary = Substitute.For<IExchangeRateFetcher>();
        primary.FetchRateAsync(TestDate, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 108m)));

        var result = await MakeSut(primary, secondary).FetchRateAsync(TestDate, "USD");

        result.IsSuccess.Should().BeTrue();
        result.Value.RateToRsd.Should().Be(108m);
        await secondary.DidNotReceive()
            .FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRateAsync_PrimaryFetcherFails_FallsBackToSecondary()
    {
        var primary = Substitute.For<IExchangeRateFetcher>();
        var secondary = Substitute.For<IExchangeRateFetcher>();
        primary.FetchRateAsync(TestDate, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("NBS_HTTP_ERROR", "500")));
        secondary.FetchRateAsync(TestDate, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 109m)));

        var result = await MakeSut(primary, secondary).FetchRateAsync(TestDate, "USD");

        result.IsSuccess.Should().BeTrue();
        result.Value.RateToRsd.Should().Be(109m);
    }

    [Fact]
    public async Task GetRateAsync_BothFetchersFail_ReturnsFailure()
    {
        var primary = Substitute.For<IExchangeRateFetcher>();
        var secondary = Substitute.For<IExchangeRateFetcher>();
        primary.FetchRateAsync(TestDate, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("NBS_HTTP_ERROR", "500")));
        secondary.FetchRateAsync(TestDate, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("NBS_SCRAPE_ERROR", "parse failed")));

        var result = await MakeSut(primary, secondary).FetchRateAsync(TestDate, "USD");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NBS_SCRAPE_ERROR");
    }

    [Fact]
    public async Task FetchRateAsync_PrimaryNbsParseError_CallsSecondaryAndReturnsSecondaryResult()
    {
        var primary = Substitute.For<IExchangeRateFetcher>();
        var secondary = Substitute.For<IExchangeRateFetcher>();
        primary.FetchRateAsync(TestDate, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("NBS_PARSE_ERROR", "bad xml")));
        secondary.FetchRateAsync(TestDate, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 110m)));

        var result = await MakeSut(primary, secondary).FetchRateAsync(TestDate, "USD");

        result.IsSuccess.Should().BeTrue();
        result.Value.RateToRsd.Should().Be(110m);
    }

    [Fact]
    public async Task FetchRateAsync_PrimaryRateNotFound_SecondaryNotCalled_ReturnsRateNotFound()
    {
        var primary = Substitute.For<IExchangeRateFetcher>();
        var secondary = Substitute.For<IExchangeRateFetcher>();
        primary.FetchRateAsync(TestDate, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("RATE_NOT_FOUND", "weekend")));

        var result = await MakeSut(primary, secondary).FetchRateAsync(TestDate, "USD");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RATE_NOT_FOUND");
        await secondary.DidNotReceive()
            .FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FetchRateAsync_PrimaryUnsupportedCurrency_SecondaryNotCalled_ReturnsUnsupportedCurrency()
    {
        var primary = Substitute.For<IExchangeRateFetcher>();
        var secondary = Substitute.For<IExchangeRateFetcher>();
        primary.FetchRateAsync(TestDate, "XYZ", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("UNSUPPORTED_CURRENCY", "not supported")));

        var result = await MakeSut(primary, secondary).FetchRateAsync(TestDate, "XYZ");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("UNSUPPORTED_CURRENCY");
        await secondary.DidNotReceive()
            .FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
