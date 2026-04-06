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
    public Guid Id { get; }
    public Guid TaxpayerProfileId { get; }
    public DateOnly TaxPeriod { get; }
    public FilingStatus Status { get; private set; }

    public Filing(Guid id, Guid taxpayerProfileId, DateOnly taxPeriod, FilingStatus status = FilingStatus.Init)
    {
        Id = id;
        TaxpayerProfileId = taxpayerProfileId;
        TaxPeriod = taxPeriod;
        Status = status;
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
