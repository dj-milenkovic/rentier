namespace Rentier.Application.Parsing;

/// <summary>A row-level problem found while parsing a statement.</summary>
/// <param name="Code">Machine-readable reason, e.g. <c>ROW_AMOUNT_INVALID</c> or <c>WHT_UNMATCHED</c>.</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="RowNumber">1-based row in the statement, when known.</param>
/// <param name="Severity">
/// <see cref="ParseSeverity.Error"/> when income data was lost (a dividend, interest or withholding
/// row could not be used), so the report must not be treated as fully processed;
/// <see cref="ParseSeverity.Warning"/> when nothing taxable was lost.
/// </param>
public sealed record ParseError(
    string Code, string Message, int? RowNumber = null, ParseSeverity Severity = ParseSeverity.Error);
