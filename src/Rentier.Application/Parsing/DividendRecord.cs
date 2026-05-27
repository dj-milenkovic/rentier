namespace Rentier.Application.Parsing;

public sealed record DividendRecord(DateOnly Date, string Currency, string EntityName, decimal Amount);
