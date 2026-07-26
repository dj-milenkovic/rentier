namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Carries the inputs to <see cref="Entities.Importer.UpdateDetails"/>.
/// Pure data carrier — all validation stays in <c>UpdateDetails</c>.
/// </summary>
public sealed record ImporterDetails(
    string DisplayName,
    Rentier.Domain.Enums.ReportType ReportType,
    Guid? TaxpayerProfileId,
    Guid? MailboxId,
    string FromFilter,
    string SubjectFilter,
    string AttachmentRegex,
    string PaymentNotes);
