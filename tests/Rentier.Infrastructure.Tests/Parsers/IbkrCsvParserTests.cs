using System.Text;
using FluentAssertions;
using Rentier.Application.Interfaces;
using Rentier.Application.Parsing;
using Rentier.Infrastructure.Parsing;

namespace Rentier.Infrastructure.Tests.Parsers;

[Trait("Category", "Unit")]
public sealed class IbkrCsvParserTests
{
    private static Stream LoadFixture(string name)
    {
        var resourceName = $"Rentier.Infrastructure.Tests.Parsers.Fixtures.{name}";
        return typeof(IbkrCsvParserTests).Assembly
                   .GetManifestResourceStream(resourceName)
               ?? throw new InvalidOperationException($"Fixture not found: {resourceName}");
    }

    // ─── Contract clause: entity name extraction ──────────────────────────────
    // IStatementParser promises to extract the ticker from descriptions of the form
    // "TICKER(ISIN) Suffix…" and to return the full description when no ISIN is present.

    [Theory]
    [InlineData("AAPL(US0378331005) Cash Dividend USD 0.24 per Share", "AAPL")]
    [InlineData("MSFT(US5949181045) Dividend", "MSFT")]
    public async Task ParseAsync_DividendWithIsinInDescription_ExtractsTickerAsEntityName(
        string description, string expectedEntityName)
    {
        var csv = $"""
            Dividends,Header,Currency,Date,Description,Amount
            Dividends,Data,USD,2024-01-15,"{description}",24.00
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        IStatementParser parser = new IbkrCsvParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dividends[0].EntityName.Should().Be(expectedEntityName);
    }

    [Fact]
    public async Task ParseAsync_DividendWithoutIsinInDescription_UsesFullDescription()
    {
        var csv = """
            Dividends,Header,Currency,Date,Description,Amount
            Dividends,Data,USD,2024-01-15,No ISIN Here,24.00
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        IStatementParser parser = new IbkrCsvParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dividends[0].EntityName.Should().Be("No ISIN Here");
    }

    // ─── Contract clause: successful parse ───────────────────────────────────

    [Fact]
    public async Task ParseAsync_HappyPath_ReturnsAllSectionsClean()
    {
        IStatementParser parser = new IbkrCsvParser();
        await using var stream = LoadFixture("happy_path.csv");

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var value = result.Value;
        value.Dividends.Count.Should().Be(1);
        value.Withholdings.Count.Should().Be(1);
        value.Interest.Count.Should().Be(1);
        value.EmbeddedRates.Count.Should().Be(1);
        value.Errors.Should().BeEmpty();

        var div = value.Dividends[0];
        div.EntityName.Should().Be("AAPL");
        div.Currency.Should().Be("USD");
        div.Date.Should().Be(new DateOnly(2024, 1, 15));
        div.Amount.Should().Be(24.00m);

        var rate = value.EmbeddedRates[0];
        rate.FromCurrency.Should().Be("EUR");
        rate.ToCurrency.Should().Be("USD");
        rate.Rate.Should().Be(1.0943m);
    }

    // ─── Contract clause: dividend aggregation per (entity, date, currency) ──

    [Fact]
    public async Task ParseAsync_MultipleDividendsSameEntity_AggregatesAmounts()
    {
        IStatementParser parser = new IbkrCsvParser();
        await using var stream = LoadFixture("multiple_dividends_same_entity.csv");

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dividends.Count.Should().Be(1);
        result.Value.Dividends[0].Amount.Should().Be(24.00m);
    }

    [Fact]
    public async Task ParseAsync_MultipleDividendsDifferentDates_ReturnsTwoRecords()
    {
        IStatementParser parser = new IbkrCsvParser();
        await using var stream = LoadFixture("multiple_dividends_different_dates.csv");

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dividends.Count.Should().Be(2);
    }

    // ─── Contract clause: WHT matching and aggregation per (entity, date, currency)

    [Fact]
    public async Task ParseAsync_MultipleWhtSameEntity_AggregatesAmounts()
    {
        IStatementParser parser = new IbkrCsvParser();
        await using var stream = LoadFixture("multiple_wht_same_entity.csv");

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Withholdings.Count.Should().Be(1);
        result.Value.Withholdings[0].Amount.Should().Be(3.60m);
        result.Value.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// Real-world pattern: IBKR emits two dividend rows for the same entity on the same date
    /// (a "Payment in Lieu" row and a regular cash dividend row). It also emits a separate WHT
    /// row for each. Both dividend rows must be summed into one record; both WHT rows must be
    /// summed into one record and matched to that single dividend record.
    /// </summary>
    [Fact]
    public async Task ParseAsync_RealWorldMultiplePaymentTypesPerEntity_AggregatesBothDividendAndWht()
    {
        IStatementParser parser = new IbkrCsvParser();
        await using var stream = LoadFixture("real_world_multiple_payment_types.csv");

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Errors.Should().BeEmpty();

        result.Value.Dividends.Count.Should().Be(1);
        result.Value.Dividends[0].EntityName.Should().Be("ACME");
        result.Value.Dividends[0].Currency.Should().Be("USD");
        result.Value.Dividends[0].Amount.Should().Be(0.35m); // 0.09 + 0.26

        result.Value.Withholdings.Count.Should().Be(1);
        result.Value.Withholdings[0].EntityName.Should().Be("ACME");
        result.Value.Withholdings[0].Amount.Should().Be(0.11m); // 0.03 + 0.08
    }

    /// <summary>
    /// Full custom-statement format: Total/summary rows in Dividends and WHT sections,
    /// plus the 4-column Base Currency Exchange Rate format used in custom statements.
    /// Parser must produce zero errors and extract the correct aggregated records.
    /// </summary>
    [Fact]
    public async Task ParseAsync_RealWorldCustomStatementFormat_ParsesCleanlyWithNoErrors()
    {
        IStatementParser parser = new IbkrCsvParser();
        await using var stream = LoadFixture("real_world_custom_statement.csv");

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Errors.Should().BeEmpty();

        result.Value.Dividends.Count.Should().Be(1);
        result.Value.Dividends[0].Amount.Should().Be(0.35m);

        result.Value.Withholdings.Count.Should().Be(1);
        result.Value.Withholdings[0].Amount.Should().Be(0.11m);

        // Custom-statement FX rows are 4-column (no date) — silently skipped, not an error
        result.Value.EmbeddedRates.Should().BeEmpty();
    }

    // ─── Contract clause: interest classification (credit vs debit) ──────────

    [Fact]
    public async Task ParseAsync_InterestDebitCredit_ReturnsSeparateRecords()
    {
        IStatementParser parser = new IbkrCsvParser();
        await using var stream = LoadFixture("interest_debit_credit.csv");

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Interest.Count.Should().Be(2);
        result.Value.Interest.Should().Contain(r => r.Type == InterestType.Credit && r.Amount > 0);
        result.Value.Interest.Should().Contain(r => r.Type == InterestType.Debit && r.Amount > 0);
    }

    // ─── Contract clause: recoverable anomalies produce ParseError, not failure

    [Fact]
    public async Task ParseAsync_WhtCurrencyMismatch_AddsParseError()
    {
        IStatementParser parser = new IbkrCsvParser();
        await using var stream = LoadFixture("wht_currency_mismatch.csv");

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Withholdings.Count.Should().Be(0);
        result.Value.Errors.Count.Should().Be(1);
        result.Value.Errors[0].Code.Should().Be("WHT_CURRENCY_MISMATCH");
    }

    [Fact]
    public async Task ParseAsync_WhtUnmatched_AddsParseError()
    {
        IStatementParser parser = new IbkrCsvParser();
        await using var stream = LoadFixture("wht_unmatched.csv");

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Withholdings.Count.Should().Be(0);
        result.Value.Errors.Count.Should().Be(1);
        result.Value.Errors[0].Code.Should().Be("WHT_UNMATCHED");
    }

    [Fact]
    public async Task ParseAsync_MalformedRow_SkipsRowAndContinues()
    {
        IStatementParser parser = new IbkrCsvParser();
        await using var stream = LoadFixture("malformed_row.csv");

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dividends.Count.Should().Be(1);
        result.Value.Dividends[0].EntityName.Should().Be("MSFT");
        result.Value.Errors.Count.Should().Be(1);
        result.Value.Errors[0].Code.Should().Be("ROW_AMOUNT_INVALID");
    }

    [Fact]
    public async Task ParseAsync_EmptySections_ReturnsEmptyLists()
    {
        IStatementParser parser = new IbkrCsvParser();
        await using var stream = LoadFixture("empty_sections.csv");

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dividends.Should().BeEmpty();
        result.Value.Interest.Should().BeEmpty();
        result.Value.Withholdings.Should().BeEmpty();
        result.Value.EmbeddedRates.Should().BeEmpty();
        result.Value.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WhtAmountPositive_AddsWhtPositiveAmountError()
    {
        var csv = """
            Dividends,Header,Currency,Date,Description,Amount
            Dividends,Data,USD,2024-01-15,"AAPL(US0378331005) Cash Dividend",24.00
            Dividends,Total,USD,,Total,24.00
            Withholding Tax,Header,Currency,Date,Description,Amount
            Withholding Tax,Data,USD,2024-01-15,"AAPL(US0378331005) Cash Dividend - Tax",3.60
            Withholding Tax,Total,USD,,Total,3.60
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        IStatementParser parser = new IbkrCsvParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Withholdings.Should().BeEmpty();
        result.Value.Errors.Should().ContainSingle(e => e.Code == "WHT_POSITIVE_AMOUNT");
    }

    [Fact]
    public async Task ParseAsync_FxRateNonPositive_AddsRateError()
    {
        var csv = """
            Base Currency Exchange Rate,Header,FromCurrency,Date,Description,ToCurrency,ExchangeRate
            Base Currency Exchange Rate,Data,EUR,2024-01-15,EUR.USD 01/15/2024,USD,-1.0943
            Base Currency Exchange Rate,Total,,,Total,,
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        IStatementParser parser = new IbkrCsvParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.EmbeddedRates.Should().BeEmpty();
        result.Value.Errors.Should().ContainSingle(e => e.Code == "RATE_NON_POSITIVE");
    }

    // ─── Contract clause: fatal errors produce Result.Failure ────────────────

    [Fact]

    public async Task ParseAsync_CsvWithNoKnownSections_ReturnsInvalidFormatError()
    {
        var csv = """
            Trades,Header,Currency,Date,Symbol,Quantity,Price
            Trades,Data,USD,2024-01-15,AAPL,10,185.00
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        IStatementParser parser = new IbkrCsvParser();

        var result = await parser.ParseAsync(stream, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("INVALID_FORMAT");
    }
}
