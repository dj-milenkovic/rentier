namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Optional provenance metadata for <see cref="Entities.Filing.CreateFromIncome"/>:
/// the source report and exchange-rate/ticker/notes context, when known.
/// </summary>
public sealed record FilingProvenance(
    Guid? ReportId = null,
    DateOnly? ExchangeRateSourceDate = null,
    Rentier.Domain.Enums.ExchangeRateSourceType? ExchangeRateSourceType = null,
    string? Ticker = null,
    string? PaymentNotes = null);
