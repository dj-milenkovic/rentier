using Rentier.Domain.Enums;

namespace Rentier.Application.DTOs;

public sealed record ManualFilingPreviewDto(
    decimal GrossIncomeRsd,
    decimal WhtPaidRsd,
    decimal GrossTaxPayableRsd,
    decimal TaxPayableRsd,
    DateOnly FilingDeadline,
    decimal ExchangeRateValue,
    DateOnly ExchangeRateSourceDate,
    ExchangeRateSourceType ExchangeRateSourceType);
