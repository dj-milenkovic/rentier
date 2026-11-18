using Rentier.Application.DTOs;
using Rentier.Domain.ValueObjects;

namespace Rentier.Application.Filings;

/// <summary>What a filing for an income event would contain: computed tax, applied rate, deadline.</summary>
public sealed record IncomeEventAssessment(FilingInfo TaxInfo, RateResolution Rate, DateOnly Deadline);
