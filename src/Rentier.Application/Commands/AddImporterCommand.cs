using Rentier.Domain.Enums;

namespace Rentier.Application.Commands;

public sealed record AddImporterCommand(
    string DisplayName,
    ReportType ReportType,
    Guid? TaxpayerProfileId,
    Guid? MailboxId,
    string FromFilter,
    string SubjectFilter,
    string AttachmentRegex,
    string PaymentNotes);
