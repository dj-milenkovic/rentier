namespace Rentier.Application.DTOs;

/// <summary>Structured per-event error recorded when a filing could not be created for an income event.</summary>
public sealed record FilingCreationError(
    string EntityName,
    DateOnly IncomeDate,
    string Currency,
    decimal Amount,
    string ErrorCode,
    string Message);