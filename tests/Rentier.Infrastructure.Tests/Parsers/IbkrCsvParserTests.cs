using System.Text;
using FluentAssertions;
using Rentier.Application.Parsing;
using Rentier.Infrastructure.Parsing;
using Xunit;

namespace Rentier.Infrastructure.Tests.Parsers;

public sealed class IbkrCsvParserTests
{
    private static Stream LoadFixture(string name)
    {
        var resourceName = $"Rentier.Infrastructure.Tests.Parsers.Fixtures.{name}";
        return typeof(IbkrCsvParserTests).Assembly
                   .GetManifestResourceStream(resourceName)
               ?? throw new InvalidOperationException($"Fixture not found: {resourceName}");
    }

    // ─── StripIsin unit tests (no stream, no async) ───────────────────────────

    [Fact]
    public void StripIsin_WithIsin_ReturnsEntityName()
    {
        IbkrCsvParser.StripIsin("AAPL(US0378331005) Cash Dividend").Should().Be("AAPL");
        IbkrCsvParser.StripIsin("MSFT(US5949181045) Dividend").Should().Be("MSFT");
    }

    [Fact]
    public void StripIsin_WithoutParentheses_ReturnsFullTrimmedString()
    {
        IbkrCsvParser.StripIsin("No ISIN description").Should().Be("No ISIN description");
    }

    // ─── US1 — Happy path ─────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_HappyPath_ReturnsAllSectionsClean()
    {
        var parser = new IbkrCsvParser();
        await using var stream = LoadFixture("happy_path.csv");

        var result = await parser.ParseAsync(stream);

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

    [Fact]
    public async Task ParseAsync_MultipleDividendsSameEntity_AggregatesAmounts()
    {
        var parser = new IbkrCsvParser();
        await using var stream = LoadFixture("multiple_dividends_same_entity.csv");

        var result = await parser.ParseAsync(stream);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dividends.Count.Should().Be(1);
        result.Value.Dividends[0].Amount.Should().Be(24.00m);
    }

    [Fact]
    public async Task ParseAsync_MultipleDividendsDifferentDates_ReturnsTwoRecords()
    {
        var parser = new IbkrCsvParser();
        await using var stream = LoadFixture("multiple_dividends_different_dates.csv");

        var result = await parser.ParseAsync(stream);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dividends.Count.Should().Be(2);
    }

    [Fact]
    public async Task ParseAsync_InterestDebitCredit_ReturnsSeparateRecords()
    {
        var parser = new IbkrCsvParser();
        await using var stream = LoadFixture("interest_debit_credit.csv");

        var result = await parser.ParseAsync(stream);

        result.IsSuccess.Should().BeTrue();
        result.Value.Interest.Count.Should().Be(2);
        result.Value.Interest.Should().Contain(r => r.Type == InterestType.Credit && r.Amount > 0);
        result.Value.Interest.Should().Contain(r => r.Type == InterestType.Debit && r.Amount > 0);
    }

    // ─── US2 — Recoverable anomalies ─────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_WhtCurrencyMismatch_AddsParseError()
    {
        var parser = new IbkrCsvParser();
        await using var stream = LoadFixture("wht_currency_mismatch.csv");

        var result = await parser.ParseAsync(stream);

        result.IsSuccess.Should().BeTrue();
        result.Value.Withholdings.Count.Should().Be(0);
        result.Value.Errors.Count.Should().Be(1);
        result.Value.Errors[0].Code.Should().Be("WHT_CURRENCY_MISMATCH");
    }

    [Fact]
    public async Task ParseAsync_WhtUnmatched_AddsParseError()
    {
        var parser = new IbkrCsvParser();
        await using var stream = LoadFixture("wht_unmatched.csv");

        var result = await parser.ParseAsync(stream);

        result.IsSuccess.Should().BeTrue();
        result.Value.Withholdings.Count.Should().Be(0);
        result.Value.Errors.Count.Should().Be(1);
        result.Value.Errors[0].Code.Should().Be("WHT_UNMATCHED");
    }

    [Fact]
    public async Task ParseAsync_MalformedRow_SkipsRowAndContinues()
    {
        var parser = new IbkrCsvParser();
        await using var stream = LoadFixture("malformed_row.csv");

        var result = await parser.ParseAsync(stream);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dividends.Count.Should().Be(1);
        result.Value.Dividends[0].EntityName.Should().Be("MSFT");
        result.Value.Errors.Count.Should().Be(1);
        result.Value.Errors[0].Code.Should().Be("ROW_AMOUNT_INVALID");
    }

    [Fact]
    public async Task ParseAsync_EmptySections_ReturnsEmptyLists()
    {
        var parser = new IbkrCsvParser();
        await using var stream = LoadFixture("empty_sections.csv");

        var result = await parser.ParseAsync(stream);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dividends.Should().BeEmpty();
        result.Value.Interest.Should().BeEmpty();
        result.Value.Withholdings.Should().BeEmpty();
        result.Value.EmbeddedRates.Should().BeEmpty();
        result.Value.Errors.Should().BeEmpty();
    }

    // ─── US3 — Fatal / unrecoverable ─────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_NullStream_ReturnsStreamError()
    {
        var parser = new IbkrCsvParser();

        var result = await parser.ParseAsync(null!);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("STREAM_ERROR");
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
        var parser = new IbkrCsvParser();

        var result = await parser.ParseAsync(stream);

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
        var parser = new IbkrCsvParser();

        var result = await parser.ParseAsync(stream);

        result.IsSuccess.Should().BeTrue();
        result.Value.EmbeddedRates.Should().BeEmpty();
        result.Value.Errors.Should().ContainSingle(e => e.Code == "RATE_NON_POSITIVE");
    }
}
