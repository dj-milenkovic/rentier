namespace Rentier.Application.Commands;

public sealed record UpdatePaymentReferenceCommand(Guid FilingId, string? PaymentReference);
