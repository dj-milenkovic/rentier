using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;

namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Computed PP-OPO tax filing data for a single income event.
/// All amounts in RSD, all decimal per constitution.
/// </summary>
public sealed record FilingInfo
{
    public IncomeType IncomeType       { get; }
    public string     PayingEntity     { get; }
    public DateOnly   IncomeDate       { get; }
    public decimal    GrossIncomeRsd   { get; }
    public decimal    WhtPaidRsd       { get; }
    public decimal    GrossTaxPayableRsd { get; }
    public decimal    TaxPayableRsd    { get; }

    public FilingInfo(
        IncomeType incomeType,
        string     payingEntity,
        DateOnly   incomeDate,
        decimal    grossIncomeRsd,
        decimal    whtPaidRsd,
        decimal    grossTaxPayableRsd,
        decimal    taxPayableRsd)
    {
        if (string.IsNullOrWhiteSpace(payingEntity))
            throw new DomainException("PayingEntity must not be null or whitespace");
        if (grossIncomeRsd < 0)
            throw new DomainException("GrossIncomeRsd must not be negative");
        if (whtPaidRsd < 0)
            throw new DomainException("WhtPaidRsd must not be negative");
        if (grossTaxPayableRsd < 0)
            throw new DomainException("GrossTaxPayableRsd must not be negative");
        if (taxPayableRsd < 0)
            throw new DomainException("TaxPayableRsd must not be negative");

        IncomeType         = incomeType;
        PayingEntity       = payingEntity;
        IncomeDate         = incomeDate;
        GrossIncomeRsd     = grossIncomeRsd;
        WhtPaidRsd         = whtPaidRsd;
        GrossTaxPayableRsd = grossTaxPayableRsd;
        TaxPayableRsd      = taxPayableRsd;
    }
}
