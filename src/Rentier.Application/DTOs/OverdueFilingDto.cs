using Rentier.Domain.Enums;

namespace Rentier.Application.DTOs;

public sealed record OverdueFilingDto(
    Guid Id,
    string PayingEntity,
    DateOnly FilingDeadline,
    decimal TaxPayableRsd,
    FilingStatus Status);
