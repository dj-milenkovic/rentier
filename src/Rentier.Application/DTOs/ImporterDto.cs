using Rentier.Domain.Enums;

namespace Rentier.Application.DTOs;

public sealed record ImporterDto(
    Guid Id,
    string DisplayName,
    ReportType ReportType,
    Guid? TaxpayerProfileId,
    Guid? MailboxId,
    string FromFilter,
    string SubjectFilter,
    string AttachmentRegex,
    string PaymentNotes);
