using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Application.Services;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

public class CalculateManualFilingCommandHandlerTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly DateOnly TestDate = new(2024, 6, 17);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static HolidayConfDto MakeHolidayDto()
        => new HolidayConfDto([], 2024, 2024);

    private static IHolidayRepository MakeHolidayRepo()
    {
        var repo = Substitute.For<IHolidayRepository>();
        repo.GetHolidayConfAsync(Arg.Any<CancellationToken>()).Returns(MakeHolidayDto());
        return repo;
    }

    private static ExchangeRateResolver MakeResolver(IExchangeRateFetcher? fetcher = null)
    {
        if (fetcher == null)
        {
            var f = Substitute.For<IExchangeRateFetcher>();
            f.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result<ExchangeRate, Error>.Success(new ExchangeRate(TestDate, "USD", 108.50m)));
            fetcher = f;
        }
        return new ExchangeRateResolver(fetcher, NullLogger<ExchangeRateResolver>.Instance);
    }

    private static CalculateManualFilingCommandHandler MakeHandler(
        IExchangeRateFetcher? fetcher = null,
        IHolidayRepository? holidayRepo = null)
    {
        var calculator = new ManualFilingCalculator(MakeResolver(fetcher), holidayRepo ?? MakeHolidayRepo());
        return new CalculateManualFilingCommandHandler(calculator);
    }

    private static CalculateManualFilingCommand ValidCommand(decimal? netReceived = 85.00m)
        => new(ProfileId, IncomeType.Dividend, "AAPL", TestDate, "USD", 100.00m, netReceived);

    // ── Happy Path (US1) ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidCommandWithWht_ReturnsSuccessPreviewDto()
    {
        var handler = MakeHandler();
        var cmd = ValidCommand(netReceived: 85.00m);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.GrossIncomeRsd.Should().BeGreaterThan(0);
        dto.WhtPaidRsd.Should().BeGreaterThan(0);
        dto.GrossTaxPayableRsd.Should().BeGreaterThan(0);
        dto.FilingDeadline.Should().BeAfter(TestDate);
        dto.ExchangeRateValue.Should().Be(108.50m);
        dto.ExchangeRateSourceDate.Should().Be(TestDate);
        dto.ExchangeRateSourceType.Should().Be(ExchangeRateSourceType.Exact);
    }

    [Fact]
    public async Task HandleAsync_ValidCommandWithWht_ComputesCorrectGrossIncomeRsd()
    {
        var handler = MakeHandler();
        var cmd = ValidCommand(netReceived: 85.00m);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        // 100 USD * 108.50 RSD/USD = 10,850.00 RSD
        result.Value.GrossIncomeRsd.Should().Be(10_850.00m);
    }

    [Fact]
    public async Task HandleAsync_ValidCommandWithWht_WhtPaidRsdGreaterThanZero()
    {
        var handler = MakeHandler();
        var cmd = ValidCommand(netReceived: 85.00m);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        // WHT = 100 - 85 = 15 USD; 15 * 108.50 = 1627.50 RSD
        result.Value.WhtPaidRsd.Should().Be(1627.50m);
    }

    // ── No-WHT Path (US2) ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoWht_WhtPaidRsdIsZero()
    {
        var handler = MakeHandler();
        var cmd = ValidCommand(netReceived: null);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        result.Value.WhtPaidRsd.Should().Be(0m);
    }

    [Fact]
    public async Task HandleAsync_NoWht_TaxPayableEqualsGrossTaxPayable()
    {
        var handler = MakeHandler();
        var cmd = ValidCommand(netReceived: null);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        result.Value.TaxPayableRsd.Should().Be(result.Value.GrossTaxPayableRsd);
    }

    // ── Validation Failures (US3) ─────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_BlankTicker_ReturnsTickerRequiredError(string ticker)
    {
        var handler = MakeHandler();
        var cmd = new CalculateManualFilingCommand(ProfileId, IncomeType.Dividend, ticker, TestDate, "USD", 100m, null);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("TICKER_REQUIRED");
    }

    [Fact]
    public async Task HandleAsync_GrossAmountZero_ReturnsGrossRequiredError()
    {
        var handler = MakeHandler();
        var cmd = new CalculateManualFilingCommand(ProfileId, IncomeType.Dividend, "AAPL", TestDate, "USD", 0m, null);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("GROSS_REQUIRED");
    }

    [Fact]
    public async Task HandleAsync_DefaultIncomeDate_ReturnsDateRequiredError()
    {
        var handler = MakeHandler();
        var cmd = new CalculateManualFilingCommand(ProfileId, IncomeType.Dividend, "AAPL", default, "USD", 100m, null);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DATE_REQUIRED");
    }

    [Fact]
    public async Task HandleAsync_NetExceedsGross_ReturnsNetExceedsGrossError()
    {
        var handler = MakeHandler();
        var cmd = new CalculateManualFilingCommand(ProfileId, IncomeType.Dividend, "AAPL", TestDate, "USD", 100m, 150m);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NET_EXCEEDS_GROSS");
    }

    [Fact]
    public async Task HandleAsync_NegativeNetReceived_ReturnsNetNegativeError()
    {
        var handler = MakeHandler();
        var cmd = new CalculateManualFilingCommand(ProfileId, IncomeType.Dividend, "AAPL", TestDate, "USD", 100m, -1m);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NET_NEGATIVE");
    }

    // ── Rate Resolution Failures (US3) ────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_RateFetchReturnsFailure_ReturnsRateNotFoundError()
    {
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExchangeRate, Error>.Failure(new Error("RATE_NOT_FOUND", "No rate")));

        var handler = MakeHandler(fetcher: fetcher);
        var cmd = ValidCommand();

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("RATE_NOT_FOUND");
    }

    [Fact]
    public async Task HandleAsync_RateFetchThrowsHttpRequestException_ReturnsNetworkFailureError()
    {
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Result<ExchangeRate, Error>>(_ => throw new HttpRequestException("network down"));

        var handler = MakeHandler(fetcher: fetcher);
        var cmd = ValidCommand();

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NETWORK_FAILURE");
    }
}
