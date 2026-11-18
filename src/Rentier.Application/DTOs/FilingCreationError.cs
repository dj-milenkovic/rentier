using Rentier.Application.Parsing;

namespace Rentier.Application.DTOs;

/// <summary>
/// Structured per-event error recorded when a filing could not be created for an income event,
/// or when a statement row could not be read (<paramref name="RowNumber"/> set), so its income
/// never became an event.
/// </summary>
public sealed record FilingCreationError(
    string EntityName,
    DateOnly IncomeDate,
    string Currency,
    decimal Amount,
    string ErrorCode,
    string Message,
    int? RowNumber = null)
{
    /// <summary>An error for a statement row whose income data was lost while parsing.</summary>
    public static FilingCreationError ForStatementRow(ParseError error)
        => new(string.Empty, default, string.Empty, 0m, error.Code, error.Message, error.RowNumber);

    /// <summary>One-line message for the sync log.</summary>
    public string ToLogMessage()
        => string.IsNullOrEmpty(EntityName)
            ? $"Statement row {RowNumber?.ToString() ?? "?"}: {ErrorCode} - {Message}"
            : $"{EntityName} {IncomeDate:yyyy-MM-dd}: {ErrorCode} - {Message}";
}
