using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Services;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

public class ExchangeRateResolverTests
{
    private static HolidayConf NoHolidays() => new HolidayConf(new List<DateOnly>());

    private static ExchangeRateResolver MakeResolver(IExchangeRateFetcher fetcher)
        => new ExchangeRateResolver(fetcher, NullLogger<ExchangeRateResolver>.Instance);

    [Fact]
    public async Task ResolveAsync_ExactDateSuccess_ReturnsExactResolution()
    {
        var incomeDate = new DateOnly(2024, 6, 13);
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(incomeDate, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(incomeDate, "USD", 108m)));

        var resolver = MakeResolver(fetcher);
        var result = await resolver.ResolveAsync(incomeDate, "USD", NoHolidays(), ct: TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.SourceType.Should().Be(ExchangeRateSourceType.Exact);
        result.Value.SourceDate.Should().Be(incomeDate);
        result.Value.Rate.RateToRsd.Should().Be(108m);
    }

    [Fact]
    public async Task ResolveAsync_SaturdayFallback_ReturnsFriday()
    {
        var saturday = new DateOnly(2024, 6, 15);
        var friday = new DateOnly(2024, 6, 14);
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(saturday, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("RATE_NOT_FOUND", "no rate")));
        fetcher.FetchRateAsync(friday, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(friday, "USD", 108m)));

        var resolver = MakeResolver(fetcher);
        var result = await resolver.ResolveAsync(saturday, "USD", NoHolidays(), ct: TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.SourceType.Should().Be(ExchangeRateSourceType.Fallback);
        result.Value.SourceDate.Should().Be(friday);
        await fetcher.Received(1).FetchRateAsync(saturday, "USD", Arg.Any<CancellationToken>());
        await fetcher.Received(1).FetchRateAsync(friday, "USD", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_MaxLookbackExhausted_ReturnsRateNotFoundError()
    {
        var date = new DateOnly(2024, 6, 13);
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("RATE_NOT_FOUND", "no rate")));

        var resolver = MakeResolver(fetcher);
        var result = await resolver.ResolveAsync(date, "USD", NoHolidays(), maxLookbackDays: 3, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RATE_NOT_FOUND");
    }

    [Fact]
    public async Task ResolveAsync_UnsupportedCurrency_ReturnsImmediatelyNoWalkback()
    {
        var date = new DateOnly(2024, 6, 15);
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(date, "XYZ", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("UNSUPPORTED_CURRENCY", "not supported")));

        var resolver = MakeResolver(fetcher);
        var result = await resolver.ResolveAsync(date, "XYZ", NoHolidays(), ct: TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("UNSUPPORTED_CURRENCY");
        await fetcher.Received(1).FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_NbsParseError_ReturnsImmediatelyNoWalkback()
    {
        var date = new DateOnly(2024, 6, 15);
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(date, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("NBS_PARSE_ERROR", "bad xml")));

        var resolver = MakeResolver(fetcher);
        var result = await resolver.ResolveAsync(date, "USD", NoHolidays(), ct: TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NBS_PARSE_ERROR");
        await fetcher.Received(1).FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── A rate that could not be fetched is not a rate that was not published ──

    [Theory]
    [InlineData("NBS_HTTP_ERROR")]
    [InlineData("NBS_SCRAPE_ERROR")]
    public async Task ResolveAsync_ExactDateSourceUnavailable_ReturnsRateSourceUnavailable(string fetcherCode)
    {
        var date = new DateOnly(2024, 6, 13);
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(date, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error(fetcherCode, "connection refused")));

        var result = await MakeResolver(fetcher)
            .ResolveAsync(date, "USD", NoHolidays(), ct: TestContext.Current.CancellationToken);

        result.Error.Code.Should().Be(ErrorCodes.RATE_SOURCE_UNAVAILABLE);
    }

    [Fact]
    public async Task ResolveAsync_ExactDateSourceUnavailable_DoesNotFallBackToEarlierDate()
    {
        // The exact-date rate may exist; applying an earlier day's rate would be a wrong filing.
        var date = new DateOnly(2024, 6, 13);
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(date, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("NBS_HTTP_ERROR", "timeout")));
        fetcher.FetchRateAsync(date.AddDays(-1), "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(date.AddDays(-1), "USD", 108m)));

        await MakeResolver(fetcher).ResolveAsync(date, "USD", NoHolidays(), ct: TestContext.Current.CancellationToken);

        await fetcher.Received(1).FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WalkBackHitsSourceUnavailable_ReturnsRateSourceUnavailable()
    {
        var saturday = new DateOnly(2024, 6, 15);
        var friday = new DateOnly(2024, 6, 14);
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(saturday, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("RATE_NOT_FOUND", "weekend")));
        fetcher.FetchRateAsync(friday, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("NBS_HTTP_ERROR", "timeout")));

        var result = await MakeResolver(fetcher)
            .ResolveAsync(saturday, "USD", NoHolidays(), ct: TestContext.Current.CancellationToken);

        result.Error.Code.Should().Be(ErrorCodes.RATE_SOURCE_UNAVAILABLE);
    }

    [Fact]
    public async Task ResolveAsync_WalkBackHitsSourceUnavailable_DoesNotSkipToOlderDate()
    {
        var saturday = new DateOnly(2024, 6, 15);
        var friday = new DateOnly(2024, 6, 14);
        var thursday = new DateOnly(2024, 6, 13);
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(saturday, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("RATE_NOT_FOUND", "weekend")));
        fetcher.FetchRateAsync(friday, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("NBS_HTTP_ERROR", "timeout")));
        fetcher.FetchRateAsync(thursday, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(thursday, "USD", 108m)));

        await MakeResolver(fetcher).ResolveAsync(saturday, "USD", NoHolidays(), ct: TestContext.Current.CancellationToken);

        await fetcher.DidNotReceive().FetchRateAsync(thursday, "USD", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WalkBackHitsParseError_ReturnsThatError()
    {
        var saturday = new DateOnly(2024, 6, 15);
        var friday = new DateOnly(2024, 6, 14);
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(saturday, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("RATE_NOT_FOUND", "weekend")));
        fetcher.FetchRateAsync(friday, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("NBS_PARSE_ERROR", "bad xml")));

        var result = await MakeResolver(fetcher)
            .ResolveAsync(saturday, "USD", NoHolidays(), ct: TestContext.Current.CancellationToken);

        result.Error.Code.Should().Be("NBS_PARSE_ERROR");
    }

    [Fact]
    public async Task ResolveAsync_SretenjeHoliday_FallsBackToWednesdayFeb14()
    {
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        var holidays = new HolidayConf(new List<DateOnly>
        {
            new(2024, 2, 15),
            new(2024, 2, 16)
        });

        fetcher.FetchRateAsync(new DateOnly(2024, 2, 15), "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("RATE_NOT_FOUND", "")));
        fetcher.FetchRateAsync(new DateOnly(2024, 2, 14), "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(new DateOnly(2024, 2, 14), "USD", 108m)));

        var resolver = MakeResolver(fetcher);
        var result = await resolver.ResolveAsync(new DateOnly(2024, 2, 15), "USD", holidays, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.SourceDate.Should().Be(new DateOnly(2024, 2, 14));
        result.Value.SourceType.Should().Be(ExchangeRateSourceType.Fallback);
    }
}
