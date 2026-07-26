using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;

namespace Rentier.Domain.Entities;

/// <summary>
/// Represents a named import configuration for filtering and processing activity statements.
/// </summary>
public sealed class Importer
{
    public Guid Id { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public ReportType ReportType { get; private set; }
    public Guid? TaxpayerProfileId { get; private set; }
    public Guid? MailboxId { get; private set; }
    public string FromFilter { get; private set; } = string.Empty;
    public string SubjectFilter { get; private set; } = string.Empty;
    public string AttachmentRegex { get; private set; } = string.Empty;
    public string PaymentNotes { get; private set; } = string.Empty;

    private Importer() { }

    public static Importer Create(string displayName, ReportType reportType = ReportType.IbkrCsv)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("DisplayName must not be null or whitespace");
        displayName = displayName.Trim();
        if (displayName.Length > 200)
            throw new DomainException("DisplayName must not exceed 200 characters");
        return new Importer
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            ReportType = reportType
        };
    }

    public void UpdateDetails(ImporterDetails details)
    {
        var displayName = details.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("DisplayName must not be null or whitespace");
        displayName = displayName.Trim();
        if (displayName.Length > 200)
            throw new DomainException("DisplayName must not exceed 200 characters");
        if ((details.PaymentNotes ?? string.Empty).Length > 4000)
            throw new DomainException("PaymentNotes must not exceed 4000 characters");

        DisplayName = displayName;
        ReportType = details.ReportType;
        TaxpayerProfileId = details.TaxpayerProfileId;
        MailboxId = details.MailboxId;
        FromFilter = string.IsNullOrWhiteSpace(details.FromFilter) ? string.Empty : details.FromFilter.Trim();
        SubjectFilter = details.SubjectFilter ?? string.Empty;
        AttachmentRegex = details.AttachmentRegex ?? string.Empty;
        PaymentNotes = details.PaymentNotes ?? string.Empty;
    }
}
