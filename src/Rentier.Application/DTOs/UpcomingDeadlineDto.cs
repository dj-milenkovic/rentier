using Rentier.Domain.Enums;

namespace Rentier.Application.DTOs;

public sealed record UpcomingDeadlineDto(
    Guid Id,
    string PayingEntity,
    DateOnly FilingDeadline,
    decimal TaxPayableRsd,
    FilingStatus Status,
    IncomeType IncomeType);
