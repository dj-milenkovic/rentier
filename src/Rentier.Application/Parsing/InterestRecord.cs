namespace Rentier.Application.Parsing;
// EntityName is always "Interactive Brokers"; Amount always positive; Type conveys sign
public sealed record InterestRecord(
    DateOnly Date, string Currency, string EntityName, decimal Amount, InterestType Type);
