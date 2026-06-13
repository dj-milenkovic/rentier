using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Services;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests.Application.Services;

public class ExchangeRateResolverLoggingTests
{
    private static HolidayConf NoHolidays() => new HolidayConf(new List<DateOnly>());

    private static ExchangeRateResolver MakeResolver(IExchangeRateFetcher fetcher)
        => new ExchangeRateResolver(fetcher, NullLogger<ExchangeRateResolver>.Instance);

    [Fact]
    public async Task ResolveAsync_ExactSuccess_ReturnsExactSourceType()
    {
        var date = new DateOnly(2024, 6, 17);
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(date, "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(date, "USD", 108m)));

        var result = await MakeResolver(fetcher).ResolveAsync(date, "USD", NoHolidays(), ct: TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.SourceType.Should().Be(ExchangeRateSourceType.Exact);
    }

    [Fact]
    public async Task ResolveAsync_FallbackUsed_ReturnsFallbackSourceType()
    {
        var saturday = new DateOnly(2024, 6, 15);
        var friday = new DateOnly(2024, 6, 14);
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(saturday, "EUR", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("RATE_NOT_FOUND", "weekend")));
        fetcher.FetchRateAsync(friday, "EUR", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(friday, "EUR", 117m)));

        var result = await MakeResolver(fetcher).ResolveAsync(saturday, "EUR", NoHolidays(), ct: TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.SourceType.Should().Be(ExchangeRateSourceType.Fallback);
        result.Value.SourceDate.Should().Be(friday);
    }

    [Fact]
    public async Task ResolveAsync_AllExhausted_ReturnsRateNotFoundFailure()
    {
        var date = new DateOnly(2024, 6, 17);
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), "USD", Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("RATE_NOT_FOUND", "no rate")));

        var result = await MakeResolver(fetcher).ResolveAsync(date, "USD", NoHolidays(), maxLookbackDays: 2, ct: TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RATE_NOT_FOUND");
    }
}
