using Rentier.Domain.Enums;

namespace Rentier.Application.DTOs;

/// <summary>Read-model projection of a Filing for list display.</summary>
public sealed record FilingRowDto(
    Guid Id,
    FilingStatus Status,
    IncomeType IncomeType,
    string PayingEntity,
    DateOnly FilingDeadline,
    decimal TaxPayable,
    string? PaymentReference);
