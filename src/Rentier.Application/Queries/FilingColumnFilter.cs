using Rentier.Domain.Enums;

namespace Rentier.Application.Queries;

/// <summary>Optional per-column filter values for GetFilingsQuery. Null field = no filter on that column.</summary>
public sealed record FilingColumnFilter(
    FilingStatus? Status = null,
    IncomeType? IncomeType = null,
    string? PayingEntity = null,
    DateOnly? FilingDeadline = null,
    string? PaymentReference = null);
