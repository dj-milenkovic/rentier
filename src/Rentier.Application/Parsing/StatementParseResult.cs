namespace Rentier.Application.Parsing;

/// <summary>
/// A parsed statement: its income events (withholding already paired onto each one), the
/// broker's embedded FX rates (cross-rate fallback only) and row-level diagnostics.
/// </summary>
public sealed record StatementParseResult(
    IReadOnlyList<DividendRecord> Dividends,
    IReadOnlyList<InterestRecord> Interest,
    IReadOnlyList<IbkrExchangeRate> EmbeddedRates,
    IReadOnlyList<ParseError> Errors);
