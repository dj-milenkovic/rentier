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
    public string? PaymentReference { get; private set; }
    public DateOnly? ExchangeRateSourceDate { get; private set; }
    public ExchangeRateSourceType? ExchangeRateSourceType { get; private set; }
    public string? Ticker { get; private set; }

    // EF Core parameterless constructor
    private Filing() { }

    // Internal constructor used for EF Core hydration and test seeding.
    // Does NOT enforce CreateFromIncome invariants by design — EF hydrates
    // persisted data that was already validated on creation.
    internal Filing(Guid id, Guid taxpayerProfileId, DateOnly taxPeriod, FilingStatus status = FilingStatus.Init)
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
        Guid? reportId = null,
        DateOnly? exchangeRateSourceDate = null,
        Rentier.Domain.Enums.ExchangeRateSourceType? exchangeRateSourceType = null,
        string? ticker = null)
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
        var trimmedTicker = ticker?.Trim();
        if (string.IsNullOrEmpty(trimmedTicker))
            trimmedTicker = null;
        if (trimmedTicker?.Length > 20)
            throw new DomainException("Ticker must not exceed 20 characters.");

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
            ReportId = reportId,
            ExchangeRateSourceDate = exchangeRateSourceDate,
            ExchangeRateSourceType = exchangeRateSourceType,
            Ticker = trimmedTicker,
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

    /// <summary>
    /// Sets the payment reference. Trims whitespace, stores null when empty after trim.
    /// Throws DomainException when the value exceeds 200 characters.
    /// </summary>
    public void SetPaymentReference(string? reference)
    {
        var trimmed = reference?.Trim();
        if (trimmed?.Length > 200)
            throw new DomainException("PaymentReference must not exceed 200 characters.");
        PaymentReference = string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}