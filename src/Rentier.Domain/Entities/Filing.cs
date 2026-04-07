using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;

namespace Rentier.Domain.Entities;

/// <summary>
/// Filing lifecycle status.
/// </summary>
public enum FilingStatus
{
    Init = 0,
    Filed = 1,
    Paid = 2
}

/// <summary>
/// Represents a PP-OPO tax filing. Aggregate root.
/// Enforces the Init → Filed → Paid state machine.
/// TaxPeriod is DateOnly per constitution Principle III.
/// </summary>
public sealed class Filing
{
    public Guid Id { get; private set; }
    public Guid TaxpayerProfileId { get; private set; }
    public DateOnly TaxPeriod { get; private set; }
    public FilingStatus Status { get; private set; }
    public IncomeType IncomeType { get; private set; }
    public string PayingEntity { get; private set; } = string.Empty;
    public DateOnly IncomeDate { get; private set; }
    public decimal GrossIncomeRsd { get; private set; }
    public decimal WhtPaidRsd { get; private set; }
    public decimal GrossTaxPayableRsd { get; private set; }
    public decimal TaxPayableRsd { get; private set; }
    public DateOnly FilingDeadline { get; private set; }
    public Guid? ReportId { get; private set; }

    // EF Core parameterless constructor
    private Filing() { }

    public Filing(Guid id, Guid taxpayerProfileId, DateOnly taxPeriod, FilingStatus status = FilingStatus.Init)
    {
        Id = id;
        TaxpayerProfileId = taxpayerProfileId;
        TaxPeriod = taxPeriod;
        Status = status;
    }

    /// <summary>Creates a Filing from a computed income event.</summary>
    public static Filing CreateFromIncome(
        Guid taxpayerProfileId,
        IncomeType incomeType,
        string payingEntity,
        DateOnly incomeDate,
        decimal grossIncomeRsd,
        decimal whtPaidRsd,
        decimal grossTaxPayableRsd,
        decimal taxPayableRsd,
        DateOnly filingDeadline,
        Guid? reportId = null)
    {
        if (string.IsNullOrWhiteSpace(payingEntity))
            throw new DomainException("PayingEntity must not be empty");
        if (grossIncomeRsd < 0)
            throw new DomainException("GrossIncomeRsd must not be negative");
        if (whtPaidRsd < 0)
            throw new DomainException("WhtPaidRsd must not be negative");
        if (grossTaxPayableRsd < 0)
            throw new DomainException("GrossTaxPayableRsd must not be negative");
        if (taxPayableRsd < 0)
            throw new DomainException("TaxPayableRsd must not be negative");

        var trimmedEntity = payingEntity.Trim();

        return new Filing
        {
            Id = Guid.NewGuid(),
            TaxpayerProfileId = taxpayerProfileId,
            TaxPeriod = incomeDate,
            Status = FilingStatus.Init,
            IncomeType = incomeType,
            PayingEntity = trimmedEntity,
            IncomeDate = incomeDate,
            GrossIncomeRsd = grossIncomeRsd,
            WhtPaidRsd = whtPaidRsd,
            GrossTaxPayableRsd = grossTaxPayableRsd,
            TaxPayableRsd = taxPayableRsd,
            FilingDeadline = filingDeadline,
            ReportId = reportId
        };
    }

    /// <summary>
    /// Advance the filing status. Only Init→Filed and Filed→Paid are permitted.
    /// Any other transition throws DomainException.
    /// </summary>
    public void AdvanceStatus(FilingStatus newStatus)
    {
        var isValid = (Status, newStatus) switch
        {
            (FilingStatus.Init, FilingStatus.Filed) => true,
            (FilingStatus.Filed, FilingStatus.Paid) => true,
            _ => false
        };

        if (!isValid)
            throw new DomainException($"Invalid Filing status transition: {Status} → {newStatus}");

        Status = newStatus;
    }
}
