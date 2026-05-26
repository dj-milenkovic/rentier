using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.DTOs;

/// <summary>Bundles the resolved exchange rate with provenance metadata.</summary>
public sealed record RateResolution(
    ExchangeRate Rate,
    DateOnly SourceDate,
    ExchangeRateSourceType SourceType);
