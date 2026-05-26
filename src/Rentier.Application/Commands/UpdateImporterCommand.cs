using Rentier.Domain.Enums;

namespace Rentier.Application.Commands;

public sealed record UpdateImporterCommand(
    Guid Id,
    string DisplayName,
    ReportType ReportType,
    Guid? TaxpayerProfileId,
    Guid? MailboxId,
    string FromFilter,
    string SubjectFilter,
    string AttachmentRegex,
    string PaymentNotes);
