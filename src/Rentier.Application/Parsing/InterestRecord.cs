namespace Rentier.Application.Parsing;

/// <summary>
/// One interest entry. EntityName is always "Interactive Brokers"; Amount is always positive and
/// <paramref name="Type"/> conveys the sign. <paramref name="Withholding"/> is the tax withheld on
/// credit interest, already paired by the parser (positive; 0 when none was withheld).
/// </summary>
public sealed record InterestRecord(
    DateOnly Date, string Currency, string EntityName, decimal Amount, InterestType Type, decimal Withholding = 0m);
