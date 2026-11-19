using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Rentier.Infrastructure.Parsing;
using Rentier.Tests.Common.Fakes;

namespace Rentier.Infrastructure.Tests.Parsers;

/// <summary>
/// Statements hold the user's income. The parser may log how much it parsed and why it gave up,
/// never what the statement contained.
/// </summary>
[Trait("Category", "Unit")]
public sealed class IbkrCsvParserLoggingTests
{
    private const string STATEMENT = """
        Dividends,Header,Currency,Date,Description,Amount
        Dividends,Data,USD,2024-01-15,"ZZTOP(US9999999999) Cash Dividend USD 4242.42 per Share",4242.42
        """;

    private static async Task<CapturingLogger<IbkrCsvParser>> ParseAsync(string csv)
    {
        var log = new CapturingLogger<IbkrCsvParser>();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        await new IbkrCsvParser(log).ParseAsync(stream, TestContext.Current.CancellationToken);
        return log;
    }

    [Fact]
    public async Task ParseAsync_ValidStatement_LogsNoTickerOrAmount()
    {
        var log = await ParseAsync(STATEMENT);

        log.AllMessages.Should().NotContain("ZZTOP").And.NotContain("4242").And.NotContain("US9999999999");
    }

    [Fact]
    public async Task ParseAsync_ValidStatement_LogsParsedCountsAtDebug()
    {
        var log = await ParseAsync(STATEMENT);

        log.Entries.Should().Contain(e => e.Level == LogLevel.Debug && e.Message.Contains("1 dividends"));
    }

    [Fact]
    public async Task ParseAsync_ValidStatement_LogsNothingAtWarningOrAbove()
    {
        var log = await ParseAsync(STATEMENT);

        log.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task ParseAsync_NoRecognisedSections_LogsWarningWithoutFileContent()
    {
        var log = await ParseAsync("Some,Other,Statement\nSECRETVALUE,1,2");

        log.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning);
        log.AllMessages.Should().NotContain("SECRETVALUE");
    }
}
