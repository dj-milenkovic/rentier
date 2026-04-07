namespace Rentier.Application.Parsing;
public sealed record StatementParseResult(
    IReadOnlyList<DividendRecord>       Dividends,
    IReadOnlyList<InterestRecord>       Interest,
    IReadOnlyList<WithholdingTaxRecord> Withholdings,
    IReadOnlyList<IbkrExchangeRate>     EmbeddedRates,
    IReadOnlyList<ParseError>           Errors);
