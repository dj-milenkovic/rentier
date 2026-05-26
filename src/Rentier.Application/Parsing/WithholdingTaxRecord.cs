namespace Rentier.Application.Parsing;
// Amount always positive (CSV negative values converted via Math.Abs)
public sealed record WithholdingTaxRecord(DateOnly Date, string Currency, string EntityName, decimal Amount);
