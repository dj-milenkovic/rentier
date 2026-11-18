using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Rentier.Application.Common;
using Rentier.Application.Filings;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Application.Services;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests.Application.Filings;

/// <summary>
/// The income-event filer's interface: the one rule set shared by statement imports and manual
/// entries (rate, tax, deadline, duplicate/correction).
/// </summary>
public class IncomeEventFilerTests
{
    private static readonly Guid TaxpayerId = Guid.NewGuid();
    private static readonly DateOnly Date = new(2024, 3, 1); // Friday; +30 days = Sunday 31st → Mon 1 Apr

    private static readonly IncomeEvent Dividend = new(IncomeType.Dividend, "AAPL", Date, "USD", 100m, 10m);

    private static FilingContext Context() => new(TaxpayerId, new HolidayConf(new List<DateOnly>()));

    private static IncomeEventFiler MakeFiler(Result<ExchangeRate, Error>? rate = null)
    {
        var fetcher = Substitute.For<IExchangeRateFetcher>();
        fetcher.FetchRateAsync(Arg.Any<DateOnly>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(rate ?? Result<ExchangeRate, Error>.Success(new ExchangeRate(Date, "USD", 108.50m)));
        return new IncomeEventFiler(new ExchangeRateResolver(fetcher, NullLogger<ExchangeRateResolver>.Instance));
    }

    private static IFilingRepository FilingsWith(params decimal[] existingGrossRsd)
    {
        var repo = Substitute.For<IFilingRepository>();
        IReadOnlyList<Filing> existing = existingGrossRsd.Select(ExistingFiling).ToList();
        repo.GetByIncomeEventAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(existing);
        return repo;
    }

    private static Filing ExistingFiling(decimal grossRsd)
    {
        var grossTax = Math.Round(grossRsd * 0.15m, 2, MidpointRounding.AwayFromZero);
        return Filing.CreateFromIncome(
            new FilingInfo(IncomeType.Dividend, "AAPL", Date, grossRsd, 0m, grossTax, grossTax),
            TaxpayerId, Date.AddDays(31), new FilingProvenance(Ticker: "AAPL"));
    }

    // ── AssessAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AssessAsync_UsDividendWithTreatyWht_TaxDue542_50()
    {
        var result = await MakeFiler().AssessAsync(Dividend, Context(), TestContext.Current.CancellationToken);

        result.Value.TaxInfo.TaxPayableRsd.Should().Be(542.50m);
    }

    [Fact]
    public async Task AssessAsync_DeadlineOnSunday_ShiftsToMonday()
    {
        var result = await MakeFiler().AssessAsync(Dividend, Context(), TestContext.Current.CancellationToken);

        result.Value.Deadline.Should().Be(new DateOnly(2024, 4, 1));
    }

    [Fact]
    public async Task AssessAsync_NbsUnreachable_ReturnsRateSourceUnavailable()
    {
        var filer = MakeFiler(Result<ExchangeRate, Error>.Failure(new Error(ErrorCodes.NBS_HTTP_ERROR, "down")));

        var result = await filer.AssessAsync(Dividend, Context(), TestContext.Current.CancellationToken);

        result.Error.Code.Should().Be(ErrorCodes.RATE_SOURCE_UNAVAILABLE);
    }

    [Fact]
    public async Task AssessAsync_WhtCurrencyIsIncomeCurrency_NeverMismatches()
    {
        // Events carry withholding in their own currency, so the domain's WHT-currency rule holds.
        var result = await MakeFiler().AssessAsync(Dividend with { Withholding = 50m }, Context(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    // ── FileAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task FileAsync_NoExistingFiling_AddsFiling()
    {
        var filings = FilingsWith();

        var outcome = await MakeFiler().FileAsync(Dividend, Context(), filings, ct: TestContext.Current.CancellationToken);

        outcome.Should().BeOfType<FilingOutcome.Created>();
        await filings.Received(1).AddAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FileAsync_ExistingFilingSameGross_AlreadyFiledAndNothingAdded()
    {
        var filings = FilingsWith(10850m); // 100 × 108.50

        var outcome = await MakeFiler().FileAsync(Dividend, Context(), filings, ct: TestContext.Current.CancellationToken);

        outcome.Should().BeOfType<FilingOutcome.AlreadyFiled>();
        await filings.DidNotReceive().AddAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FileAsync_ExistingFilingDifferentGross_CorrectionDetectedAndNothingAdded()
    {
        var filings = FilingsWith(9000m);

        var outcome = await MakeFiler().FileAsync(Dividend, Context(), filings, ct: TestContext.Current.CancellationToken);

        outcome.Should().BeOfType<FilingOutcome.CorrectionDetected>()
            .Which.Error.Code.Should().Be(ErrorCodes.FILING_CORRECTION_DETECTED);
        await filings.DidNotReceive().AddAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FileAsync_RateNotFound_FailedAndNothingAdded()
    {
        var filings = FilingsWith();
        var filer = MakeFiler(Result<ExchangeRate, Error>.Failure(new Error(ErrorCodes.RATE_NOT_FOUND, "none")));

        var outcome = await filer.FileAsync(Dividend, Context(), filings, ct: TestContext.Current.CancellationToken);

        outcome.Should().BeOfType<FilingOutcome.Failed>();
        await filings.DidNotReceive().AddAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FileAsync_WithReportContext_FilingCarriesReportId()
    {
        var reportId = Guid.NewGuid();

        var outcome = await MakeFiler().FileAsync(
            Dividend, Context() with { ReportId = reportId }, FilingsWith(), ct: TestContext.Current.CancellationToken);

        ((FilingOutcome.Created)outcome).Filing.ReportId.Should().Be(reportId);
    }

    // ── IncomeEvent.FromManualEntry ──────────────────────────────────────────

    [Fact]
    public void FromManualEntry_LowercaseTickerWithSpaces_TrimmedAndUppercased()
    {
        var result = IncomeEvent.FromManualEntry(IncomeType.Dividend, "  aapl ", Date, "USD", 100m, 85m);

        result.Value.PayingEntity.Should().Be("AAPL");
    }

    [Fact]
    public void FromManualEntry_NetReceived_WithholdingIsGrossMinusNet()
    {
        var result = IncomeEvent.FromManualEntry(IncomeType.Dividend, "AAPL", Date, "USD", 100m, 85m);

        result.Value.Withholding.Should().Be(15m);
    }

    [Fact]
    public void FromManualEntry_NoNetReceived_NoWithholding()
    {
        var result = IncomeEvent.FromManualEntry(IncomeType.Dividend, "AAPL", Date, "USD", 100m, null);

        result.Value.Withholding.Should().Be(0m);
    }
}
