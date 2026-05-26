namespace Rentier.Application.Parsing;

public sealed record ParseError(string Code, string Message, int? RowNumber = null);
