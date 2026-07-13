using System.Text;
using FluentAssertions;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.Scenarios.Tests;

/// <summary>
/// End-to-end import pipeline scenarios spanning CSV parsing, exchange rate resolution,
/// tax computation, and deadline calculation over a real migrated SQLite database.
///
/// Exchange rate (117.5432 RSD/USD) is pre-seeded in the SQLite cache so the NBS
/// HTTP fetcher is never called; all assertions use hand-computed expected values.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ImportPipelineScenarios : IAsyncLifetime
{
    private readonly ScenarioFixture _fixture = new();
    private Guid _importerId;

    // Exact copy of tests/Rentier.Infrastructure.Tests/Parsers/Fixtures/happy_path.csv
    private static readonly byte[] HappyPathCsvBytes = Encoding.UTF8.GetBytes(
        "Dividends,Header,Currency,Date,Description,Amount\n" +
        "Dividends,Data,USD,2024-01-15,\"AAPL(US0378331005) Cash Dividend USD 0.24 per Share\",24.00\n" +
        "Dividends,Total,USD,,Total,24.00\n" +
        "Withholding Tax,Header,Currency,Date,Description,Amount\n" +
        "Withholding Tax,Data,USD,2024-01-15,\"AAPL(US0378331005) Cash Dividend USD 0.24 per Share - US Tax\",-3.60\n" +
        "Withholding Tax,Total,USD,,Total,-3.60\n" +
        "Interest,Header,Currency,Date,Description,Amount\n" +
        "Interest,Data,USD,2024-01-31,Credit Interest for Jan-2024,5.42\n" +
        "Interest,Total,USD,,Total,5.42\n" +
        "Base Currency Exchange Rate,Header,FromCurrency,Date,Description,ToCurrency,ExchangeRate\n" +
        "Base Currency Exchange Rate,Data,EUR,2024-01-15,EUR.USD 01/15/2024,USD,1.0943\n" +
        "Base Currency Exchange Rate,Total,,,Total,,\n");

    public async ValueTask InitializeAsync()
    {
        await _fixture.InitializeAsync();

        var ct = CancellationToken.None;

        var profile = new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "Test User", "Test Address", "011");
        await _fixture.Get<ITaxpayerProfileRepository>().SaveAsync(profile, ct);

        var importer = Importer.Create("IBKR Test");
        importer.UpdateDetails("IBKR Test", ReportType.IbkrCsv, profile.Id, null,
            string.Empty, string.Empty, string.Empty, string.Empty);
        await _fixture.Get<IImporterRepository>().AddAsync(importer, ct);
        _importerId = importer.Id;
    }

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private Task SeedRateAsync(DateOnly date, decimal rateToRsd, CancellationToken ct = default) =>
        _fixture.Get<IExchangeRateCacheRepository>().SaveAsync(new ExchangeRate(date, "USD", rateToRsd), ct);

    // ── Golden path ───────────────────────────────────────────────────────────

    /// <summary>
    /// Hand-computed amounts for rate = 117.5432 RSD/USD:
    /// AAPL dividend $24.00 / WHT $3.60:
    ///   grossIncomeRsd = Round(24.00 × 117.5432, 2) = 2821.04
    ///   whtPaidRsd     = Round( 3.60 × 117.5432, 2) =  423.16
    ///   grossTaxRsd    = Round(2821.04 × 0.15,   2) =  423.16
    ///   taxPayableRsd  = max(423.16 − 423.16, 0)    =    0.00
    /// Credit interest $5.42 / no WHT:
    ///   grossIncomeRsd = Round( 5.42 × 117.5432, 2) =  637.08
    ///   grossTaxRsd    = Round( 637.08 × 0.15,   2) =   95.56
    ///   taxPayableRsd  =  95.56
    /// </summary>
    [Fact]
    public async Task GoldenPath_ImportHappyPathCsv_CreatesFilingsWithExactAmounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedRateAsync(new DateOnly(2024, 1, 15), 117.5432m, ct);
        await SeedRateAsync(new DateOnly(2024, 1, 31), 117.5432m, ct);

        var handler = _fixture.Get<ICommandHandler<ImportReportCommand, Result<Guid, Error>>>();
        var result = await handler.HandleAsync(
            new ImportReportCommand(_importerId, "happy_path.csv", HappyPathCsvBytes), ct);

        result.IsSuccess.Should().BeTrue(because: result.IsSuccess ? string.Empty : result.Error.Message);

        var filings = await _fixture.Get<IFilingRepository>().GetAllAsync(ct);
        filings.Should().HaveCount(2, because: "one dividend filing + one interest filing");

        var dividend = filings.Single(f => f.IncomeType == IncomeType.Dividend);
        dividend.GrossIncomeRsd.Should().Be(2821.04m);
        dividend.WhtPaidRsd.Should().Be(423.16m);
        dividend.GrossTaxPayableRsd.Should().Be(423.16m);
        dividend.TaxPayableRsd.Should().Be(0.00m);
        dividend.FilingDeadline.Should().Be(new DateOnly(2024, 2, 14),
            because: "2024-01-15 + 30 days = 2024-02-14 (Wednesday, no holiday)");

        var interest = filings.Single(f => f.IncomeType == IncomeType.Interest);
        interest.GrossIncomeRsd.Should().Be(637.08m);
        interest.WhtPaidRsd.Should().Be(0.00m);
        interest.GrossTaxPayableRsd.Should().Be(95.56m);
        interest.TaxPayableRsd.Should().Be(95.56m);
        interest.FilingDeadline.Should().Be(new DateOnly(2024, 3, 1),
            because: "2024-01-31 + 30 days = 2024-03-01 (Friday, no holiday)");
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Import_SameCsvImportedTwice_RejectsDuplicate()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedRateAsync(new DateOnly(2024, 1, 15), 117.5432m, ct);
        await SeedRateAsync(new DateOnly(2024, 1, 31), 117.5432m, ct);

        var handler = _fixture.Get<ICommandHandler<ImportReportCommand, Result<Guid, Error>>>();

        var first = await handler.HandleAsync(
            new ImportReportCommand(_importerId, "dup_test.csv", HappyPathCsvBytes), ct);
        first.IsSuccess.Should().BeTrue(because: first.IsSuccess ? string.Empty : first.Error.Message);

        var second = await handler.HandleAsync(
            new ImportReportCommand(_importerId, "dup_test.csv", HappyPathCsvBytes), ct);
        second.IsSuccess.Should().BeFalse(because: "same file name + same importer is a duplicate");
        second.Error.Code.Should().Be("REPORT_IMPORT_DUPLICATE");
    }

    // ── WHT cap ───────────────────────────────────────────────────────────────

    /// <summary>
    /// When WHT (25%) exceeds the Serbian flat rate (15%), taxPayableRsd is floored at zero.
    /// Hand-computed for rate = 117.5432:
    ///   grossIncomeRsd = Round(100 × 117.5432, 2) = 11754.32
    ///   whtPaidRsd     = Round( 25 × 117.5432, 2) =  2938.58
    ///   grossTaxRsd    = Round(11754.32 × 0.15, 2) = 1763.15
    ///   taxPayableRsd  = max(1763.15 − 2938.58, 0) =    0.00
    /// </summary>
    [Fact]
    public async Task WhtExceedsSerbianRate_TaxPayableIsZero()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedRateAsync(new DateOnly(2024, 3, 1), 117.5432m, ct);

        var csvBytes = Encoding.UTF8.GetBytes(
            "Dividends,Header,Currency,Date,Description,Amount\n" +
            "Dividends,Data,USD,2024-03-01,\"HIGHWHT(US0000000002) Cash Dividend\",100.00\n" +
            "Dividends,Total,USD,,Total,100.00\n" +
            "Withholding Tax,Header,Currency,Date,Description,Amount\n" +
            "Withholding Tax,Data,USD,2024-03-01,\"HIGHWHT(US0000000002) Cash Dividend - US Tax\",-25.00\n" +
            "Withholding Tax,Total,USD,,Total,-25.00\n");

        var handler = _fixture.Get<ICommandHandler<ImportReportCommand, Result<Guid, Error>>>();
        var result = await handler.HandleAsync(
            new ImportReportCommand(_importerId, "wht_cap.csv", csvBytes), ct);

        result.IsSuccess.Should().BeTrue(because: result.IsSuccess ? string.Empty : result.Error.Message);

        var filings = await _fixture.Get<IFilingRepository>().GetAllAsync(ct);
        filings.Should().HaveCount(1);

        var filing = filings[0];
        filing.GrossIncomeRsd.Should().Be(11754.32m);
        filing.WhtPaidRsd.Should().Be(2938.58m);
        filing.GrossTaxPayableRsd.Should().Be(1763.15m);
        filing.TaxPayableRsd.Should().Be(0.00m,
            because: "foreign WHT (25%) exceeds Serbian rate (15%) — tax payable is floored at zero");
    }

    // ── Deadline on Serbian holiday ───────────────────────────────────────────

    /// <summary>
    /// Income date 2024-01-16, rate seeded for that Tuesday.
    /// Deadline candidate: 2024-01-16 + 30 = 2024-02-15 (Thursday).
    /// Seeded holidays: 2024-02-15 and 2024-02-16 (Serbian Statehood Day).
    /// Shift chain: 02-15 (holiday) → 02-16 (holiday) → 02-17 (Saturday) → 02-19 (Monday).
    /// Expected deadline: 2024-02-19.
    /// </summary>
    [Fact]
    public async Task DeadlineOnSerbianHoliday_ShiftsToNextBusinessDay()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedRateAsync(new DateOnly(2024, 1, 16), 117.5432m, ct);

        await _fixture.Get<IHolidayRepository>().SaveHolidaysAsync(
            [
                PublicHoliday.Create(new DateOnly(2024, 2, 15), "Statehood Day"),
                PublicHoliday.Create(new DateOnly(2024, 2, 16), "Statehood Day")
            ],
            new HolidayYearRange(2024, 2024),
            ct);

        var csvBytes = Encoding.UTF8.GetBytes(
            "Dividends,Header,Currency,Date,Description,Amount\n" +
            "Dividends,Data,USD,2024-01-16,\"TESTCO(US0000000001) Cash Dividend\",100.00\n" +
            "Dividends,Total,USD,,Total,100.00\n");

        var handler = _fixture.Get<ICommandHandler<ImportReportCommand, Result<Guid, Error>>>();
        var result = await handler.HandleAsync(
            new ImportReportCommand(_importerId, "holiday_deadline.csv", csvBytes), ct);

        result.IsSuccess.Should().BeTrue(because: result.IsSuccess ? string.Empty : result.Error.Message);

        var filings = await _fixture.Get<IFilingRepository>().GetAllAsync(ct);
        filings.Should().HaveCount(1);
        filings[0].FilingDeadline.Should().Be(new DateOnly(2024, 2, 19),
            because: "02-15 (holiday) → 02-16 (holiday) → 02-17 (Sat) → 02-19 (Mon)");
    }
}
