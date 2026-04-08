using Rentier.Domain.Enums;

namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Computed PP-OPO tax filing data for a single income event.
/// All amounts in RSD, all decimal per constitution.
/// </summary>
public sealed record FilingInfo(
    IncomeType IncomeType,
    string     PayingEntity,
    DateOnly   IncomeDate,
    decimal    GrossIncomeRsd,
    decimal    WhtPaidRsd,
    decimal    GrossTaxPayableRsd,
    decimal    TaxPayableRsd);
