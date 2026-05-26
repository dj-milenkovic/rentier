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
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

public class CreateManualFilingCommandHandlerTests
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

    private static IFilingRepository MakeFilingRepo(bool exists = false)
    {
        var repo = Substitute.For<IFilingRepository>();
        repo.ExistsByIncomeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(exists);
        return repo;
    }

    private static CreateManualFilingCommandHandler MakeHandler(
        IExchangeRateFetcher? fetcher = null,
        IHolidayRepository? holidayRepo = null,
        IFilingRepository? filingRepo = null)
    {
        var calculator = new ManualFilingCalculator(MakeResolver(fetcher), holidayRepo ?? MakeHolidayRepo());
        return new CreateManualFilingCommandHandler(calculator, filingRepo ?? MakeFilingRepo());
    }

    private static CreateManualFilingCommand ValidCommand(decimal? netReceived = 85.00m)
        => new(ProfileId, IncomeType.Dividend, "AAPL", TestDate, "USD", 100.00m, netReceived);

    // ── Happy Path (US1) ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidCommandWithWht_ReturnsSuccessWithFilingId()
    {
        var filingRepo = MakeFilingRepo(exists: false);
        var handler = MakeHandler(filingRepo: filingRepo);
        var cmd = ValidCommand(netReceived: 85.00m);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task HandleAsync_ValidCommandWithWht_CallsAddAsyncOnce()
    {
        var filingRepo = MakeFilingRepo(exists: false);
        var handler = MakeHandler(filingRepo: filingRepo);
        var cmd = ValidCommand(netReceived: 85.00m);

        await handler.HandleAsync(cmd);

        await filingRepo.Received(1).AddAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ValidCommandWithWht_TickerIsUppercasedInFiling()
    {
        var filingRepo = MakeFilingRepo(exists: false);
        var handler = MakeHandler(filingRepo: filingRepo);
        var cmd = new CreateManualFilingCommand(ProfileId, IncomeType.Dividend, "aapl", TestDate, "USD", 100.00m, 85.00m);

        await handler.HandleAsync(cmd);

        await filingRepo.Received(1).AddAsync(
            Arg.Is<Filing>(f => f.Ticker == "AAPL"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ValidCommandWithWht_FilingHasReportIdNull()
    {
        var filingRepo = MakeFilingRepo(exists: false);
        var handler = MakeHandler(filingRepo: filingRepo);
        var cmd = ValidCommand(netReceived: 85.00m);

        await handler.HandleAsync(cmd);

        await filingRepo.Received(1).AddAsync(
            Arg.Is<Filing>(f => f.ReportId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ValidCommandWithWht_FilingStatusIsInit()
    {
        var filingRepo = MakeFilingRepo(exists: false);
        var handler = MakeHandler(filingRepo: filingRepo);
        var cmd = ValidCommand(netReceived: 85.00m);

        await handler.HandleAsync(cmd);

        await filingRepo.Received(1).AddAsync(
            Arg.Is<Filing>(f => f.Status == FilingStatus.Init),
            Arg.Any<CancellationToken>());
    }

    // ── No-WHT Path (US2) ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoWht_FilingWhtPaidRsdIsZero()
    {
        var filingRepo = MakeFilingRepo(exists: false);
        var handler = MakeHandler(filingRepo: filingRepo);
        var cmd = ValidCommand(netReceived: null);

        await handler.HandleAsync(cmd);

        await filingRepo.Received(1).AddAsync(
            Arg.Is<Filing>(f => f.WhtPaidRsd == 0m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NoWht_ReturnsSuccess()
    {
        var handler = MakeHandler(filingRepo: MakeFilingRepo(exists: false));
        var cmd = ValidCommand(netReceived: null);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Duplicate Detection (US3) ─────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_DuplicateFiling_ReturnsDuplicateFilingError()
    {
        var filingRepo = MakeFilingRepo(exists: true);
        var handler = MakeHandler(filingRepo: filingRepo);
        var cmd = ValidCommand();

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("FILING_CREATE_DUPLICATE");
    }

    [Fact]
    public async Task HandleAsync_DuplicateFiling_AddAsyncNotCalled()
    {
        var filingRepo = MakeFilingRepo(exists: true);
        var handler = MakeHandler(filingRepo: filingRepo);
        var cmd = ValidCommand();

        await handler.HandleAsync(cmd);

        await filingRepo.DidNotReceive().AddAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }

    // ── Validation Failures (US3) ─────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_BlankTicker_ReturnsTickerRequiredError()
    {
        var handler = MakeHandler();
        var cmd = new CreateManualFilingCommand(ProfileId, IncomeType.Dividend, " ", TestDate, "USD", 100m, null);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("TICKER_REQUIRED");
    }

    [Fact]
    public async Task HandleAsync_GrossAmountZero_ReturnsGrossRequiredError()
    {
        var handler = MakeHandler();
        var cmd = new CreateManualFilingCommand(ProfileId, IncomeType.Dividend, "AAPL", TestDate, "USD", 0m, null);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("GROSS_REQUIRED");
    }

    [Fact]
    public async Task HandleAsync_NetExceedsGross_ReturnsNetExceedsGrossError()
    {
        var handler = MakeHandler();
        var cmd = new CreateManualFilingCommand(ProfileId, IncomeType.Dividend, "AAPL", TestDate, "USD", 100m, 200m);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NET_EXCEEDS_GROSS");
    }

    [Fact]
    public async Task HandleAsync_DefaultIncomeDate_ReturnsDateRequiredError()
    {
        var handler = MakeHandler();
        var cmd = new CreateManualFilingCommand(ProfileId, IncomeType.Dividend, "AAPL", default, "USD", 100m, null);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DATE_REQUIRED");
    }

    [Fact]
    public async Task HandleAsync_NegativeNetReceived_ReturnsNetNegativeError()
    {
        var handler = MakeHandler();
        var cmd = new CreateManualFilingCommand(ProfileId, IncomeType.Dividend, "AAPL", TestDate, "USD", 100m, -1m);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("NET_NEGATIVE");
    }



    [Fact]
    public async Task HandleAsync_NetworkError_ReturnsNetworkFailureError()
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
