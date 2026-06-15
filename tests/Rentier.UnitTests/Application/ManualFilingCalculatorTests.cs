using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Application.Services;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests.Application;

/// <summary>
/// Unit tests for <see cref="ManualFilingCalculator"/> — focuses on edge cases
/// not covered by the handler-level tests (e.g. network failures propagated through
/// the exchange rate resolver).
/// </summary>
public class ManualFilingCalculatorTests
{
    private static readonly DateOnly TestDate = new(2024, 6, 17);

    private static IHolidayRepository MakeHolidayRepo()
    {
        var repo = Substitute.For<IHolidayRepository>();
        repo.GetHolidayConfAsync(Arg.Any<CancellationToken>())
            .Returns(new HolidayConfDto([], 2024, 2024));
        return repo;
    }

    [Fact]
    public async Task CalculateAsync_ResolverThrowsHttpRequestException_ReturnsNetworkFailureError()
    {
        // Arrange — resolver throws as if the NBS HTTP endpoint is unreachable
        var resolver = Substitute.For<IExchangeRateResolver>();
        resolver.ResolveAsync(
                Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<HolidayConf>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var calculator = new ManualFilingCalculator(resolver, MakeHolidayRepo());

        // Act
        var result = await calculator.CalculateAsync(
            IncomeType.Dividend, "AAPL", TestDate, "USD", 100m, null, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NETWORK_FAILURE");
    }

    [Fact]
    public async Task CalculateAsync_EmptyTicker_ReturnsTickerRequiredError()
    {
        var resolver = Substitute.For<IExchangeRateResolver>();
        var calculator = new ManualFilingCalculator(resolver, MakeHolidayRepo());

        var result = await calculator.CalculateAsync(
            IncomeType.Dividend, "   ", TestDate, "USD", 100m, null, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("TICKER_REQUIRED");
    }
}
