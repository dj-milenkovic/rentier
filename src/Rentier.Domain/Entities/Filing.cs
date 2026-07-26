using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;

namespace Rentier.Domain.Entities;

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
    public string? PaymentNotes { get; private set; }

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
        FilingInfo info,
        Guid taxpayerProfileId,
        DateOnly filingDeadline,
        FilingProvenance provenance)
    {
        if (string.IsNullOrWhiteSpace(info.PayingEntity))
            throw new DomainException("PayingEntity must not be empty");
        if (info.GrossIncomeRsd < 0)
            throw new DomainException("GrossIncomeRsd must not be negative");
        if (info.WhtPaidRsd < 0)
            throw new DomainException("WhtPaidRsd must not be negative");
        if (info.GrossTaxPayableRsd < 0)
            throw new DomainException("GrossTaxPayableRsd must not be negative");
        if (info.TaxPayableRsd < 0)
            throw new DomainException("TaxPayableRsd must not be negative");

        // WHT paid by a foreign broker can legitimately exceed the computed Serbian gross tax
        // (over-withholding). The credit is capped at gross, so the net payable clamps to zero.
        var expectedTaxPayable = Math.Max(info.GrossTaxPayableRsd - info.WhtPaidRsd, 0m);
        if (info.TaxPayableRsd != expectedTaxPayable)
            throw new DomainException(
                $"TaxPayableRsd ({info.TaxPayableRsd}) must equal Math.Max(GrossTaxPayable - WhtPaid, 0) = {expectedTaxPayable}.");

        var trimmedEntity = info.PayingEntity.Trim();
        if (trimmedEntity.Length > 200)
            throw new DomainException("PayingEntity must not exceed 200 characters.");
        var trimmedTicker = provenance.Ticker?.Trim();
        if (string.IsNullOrEmpty(trimmedTicker))
            trimmedTicker = null;
        if (trimmedTicker?.Length > 20)
            throw new DomainException("Ticker must not exceed 20 characters.");
        var trimmedPaymentNotes = provenance.PaymentNotes?.Trim();
        if (string.IsNullOrEmpty(trimmedPaymentNotes))
            trimmedPaymentNotes = null;
        if (trimmedPaymentNotes?.Length > 4000)
            throw new DomainException("PaymentNotes must not exceed 4000 characters.");

        return new Filing
        {
            Id = Guid.NewGuid(),
            TaxpayerProfileId = taxpayerProfileId,
            TaxPeriod = info.IncomeDate,
            Status = FilingStatus.Init,
            IncomeType = info.IncomeType,
            PayingEntity = trimmedEntity,
            IncomeDate = info.IncomeDate,
            GrossIncomeRsd = info.GrossIncomeRsd,
            WhtPaidRsd = info.WhtPaidRsd,
            GrossTaxPayableRsd = info.GrossTaxPayableRsd,
            TaxPayableRsd = info.TaxPayableRsd,
            FilingDeadline = filingDeadline,
            ReportId = provenance.ReportId,
            ExchangeRateSourceDate = provenance.ExchangeRateSourceDate,
            ExchangeRateSourceType = provenance.ExchangeRateSourceType,
            Ticker = trimmedTicker,
            PaymentNotes = trimmedPaymentNotes,
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
