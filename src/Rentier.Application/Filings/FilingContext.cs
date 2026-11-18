using Rentier.Application.Parsing;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Filings;

/// <summary>Where an income event's filing belongs, and what is needed to compute it.</summary>
/// <param name="TaxpayerProfileId">The taxpayer the filing is for.</param>
/// <param name="Holidays">Serbian holiday calendar (deadline shift and rate walk-back).</param>
/// <param name="ReportId">The statement the event came from; null for manual entries.</param>
/// <param name="BrokerRates">The statement's FX rates, for currencies NBS does not publish.</param>
public sealed record FilingContext(
    Guid TaxpayerProfileId,
    HolidayConf Holidays,
    Guid? ReportId = null,
    IReadOnlyList<IbkrExchangeRate>? BrokerRates = null);
