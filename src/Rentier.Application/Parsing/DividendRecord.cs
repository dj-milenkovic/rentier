namespace Rentier.Application.Parsing;

/// <summary>
/// One dividend income event (rows for the same entity, date and currency are summed).
/// <paramref name="Withholding"/> is the foreign tax withheld on it, already paired by the parser
/// (positive; 0 when none was withheld).
/// </summary>
public sealed record DividendRecord(
    DateOnly Date, string Currency, string EntityName, decimal Amount, decimal Withholding = 0m);
