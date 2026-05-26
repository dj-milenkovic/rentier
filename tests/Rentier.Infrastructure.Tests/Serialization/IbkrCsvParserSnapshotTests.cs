using Rentier.Infrastructure.Parsing;

namespace Rentier.Infrastructure.Tests.Serialization;

/// <summary>
/// Snapshot tests for IbkrCsvParser output stability.
/// Locks the StatementParseResult structure (dividends, interest, withholding tax,
/// exchange rates, parse errors) against future parser regressions.
/// Depends on: tests/Rentier.Infrastructure.Tests/Parsers/Fixtures/ibkr-sample.csv
/// </summary>
public class IbkrCsvParserSnapshotTests
{
    private static Stream LoadFixture(string name)
    {
        var resourceName = $"Rentier.Infrastructure.Tests.Parsers.Fixtures.{name}";
        return typeof(IbkrCsvParserSnapshotTests).Assembly
                   .GetManifestResourceStream(resourceName)
               ?? throw new InvalidOperationException($"Fixture not found: {resourceName}");
    }

    // ── T012: IBKR CSV parser output stability ────────────────────────────────

    /// <summary>
    /// Parses the representative ibkr-sample.csv fixture and verifies the complete
    /// StatementParseResult structure (dividends, interest, withholding tax,
    /// exchange rates) matches the committed snapshot baseline.
    /// </summary>
    [Fact]
    public async Task ParseAsync_KnownIbkrCsvFixture_MatchesSnapshot()
    {
        var parser = new IbkrCsvParser();
        await using var stream = LoadFixture("ibkr-sample.csv");

        var result = await parser.ParseAsync(stream);

        // Verify the parsed result matches the snapshot
        await Verify(result.Value);
    }
}
