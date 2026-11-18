namespace Rentier.Domain.ValueObjects;

/// <summary>
/// Carries the inputs to <see cref="Services.TaxCalculationService.Calculate"/>.
/// Pure data carrier — all validation stays in <c>Calculate</c>.
/// </summary>
public sealed record IncomeTaxInput(
    Rentier.Domain.Enums.IncomeType IncomeType,
    string PayingEntity,
    DateOnly IncomeDate,
    decimal IncomeAmount,
    string IncomeCurrency,
    decimal WhtAmount,
    string WhtCurrency);
