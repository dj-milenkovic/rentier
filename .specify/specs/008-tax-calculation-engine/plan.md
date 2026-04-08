# Feature 008 — Tax Calculation Engine: Implementation Plan

## Files to create
| File | Notes |
|---|---|
| `src/Rentier.Domain/Enums/IncomeType.cs` | New enum |
| `src/Rentier.Domain/ValueObjects/FilingInfo.cs` | New record |
| `src/Rentier.Domain/Services/TaxCalculationService.cs` | Static service |
| `tests/Rentier.Domain.Tests/Services/TaxCalculationServiceTests.cs` | 10+ tests |

## Implementation

### IncomeType.cs
```csharp
namespace Rentier.Domain.Enums;
public enum IncomeType { Dividend, Interest }
```

### FilingInfo.cs
```csharp
namespace Rentier.Domain.ValueObjects;
public sealed record FilingInfo(
    IncomeType IncomeType,
    string     PayingEntity,
    DateOnly   IncomeDate,
    decimal    GrossIncomeRsd,
    decimal    WhtPaidRsd,
    decimal    GrossTaxPayableRsd,
    decimal    TaxPayableRsd);
```

### TaxCalculationService.cs
```csharp
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;

namespace Rentier.Domain.Services;

public static class TaxCalculationService
{
    private const decimal TaxRate = 0.15m;

    public static async Task<FilingInfo> CalculateAsync(
        IncomeType incomeType,
        string payingEntity,
        DateOnly incomeDate,
        decimal incomeAmount,
        string incomeCurrency,
        decimal whtAmount,
        string whtCurrency,
        Func<DateOnly, string, Task<ExchangeRate>> rateProvider,
        CancellationToken ct = default)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(payingEntity))
            throw new DomainException("PayingEntity must not be empty");
        if (string.IsNullOrWhiteSpace(incomeCurrency))
            throw new DomainException("IncomeCurrency must not be empty");
        if (string.IsNullOrWhiteSpace(whtCurrency))
            throw new DomainException("WhtCurrency must not be empty");
        if (incomeAmount < 0)
            throw new DomainException("Income amount must not be negative");
        if (whtAmount < 0)
            throw new DomainException("WHT amount must not be negative");
        if (rateProvider is null)
            throw new DomainException("Rate provider must not be null");

        var upperIncome = incomeCurrency.ToUpperInvariant();
        var upperWht    = whtCurrency.ToUpperInvariant();

        if (whtAmount > 0 && upperWht != upperIncome)
            throw new DomainException("WHT currency must match income currency");

        // Rate lookups
        var incomeRate = await rateProvider(incomeDate, upperIncome);
        ct.ThrowIfCancellationRequested();

        decimal whtPaidRsd = 0m;
        if (whtAmount > 0)
        {
            var whtRate = await rateProvider(incomeDate, upperWht);
            ct.ThrowIfCancellationRequested();
            whtPaidRsd = Round(whtAmount * whtRate.RateToRsd);
        }

        // Calculation
        var grossIncomeRsd     = Round(incomeAmount * incomeRate.RateToRsd);
        var grossTaxPayableRsd = Round(grossIncomeRsd * TaxRate);
        var taxPayableRsd      = Math.Max(grossTaxPayableRsd - whtPaidRsd, 0m);

        return new FilingInfo(
            incomeType, payingEntity, incomeDate,
            grossIncomeRsd, whtPaidRsd, grossTaxPayableRsd, taxPayableRsd);
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
```

## Test cases
1. `CalculateAsync_DividendWithWht_ComputesCorrectTax` — USD 100, rate 117.21, WHT 15 → verify all 4 amounts
2. `CalculateAsync_WhtExceedsGrossTax_ClampsToZero` — high WHT → TaxPayable=0
3. `CalculateAsync_ZeroIncome_ReturnsAllZeros`
4. `CalculateAsync_ZeroWht_SkipsWhtRateCall` — verify rateProvider only called once
5. `CalculateAsync_NegativeIncome_ThrowsDomainException`
6. `CalculateAsync_NegativeWht_ThrowsDomainException`
7. `CalculateAsync_WhtCurrencyMismatch_ThrowsDomainException`
8. `CalculateAsync_NullPayingEntity_ThrowsDomainException`
9. `CalculateAsync_NullRateProvider_ThrowsDomainException`
10. `CalculateAsync_InterestType_PassesThroughToFilingInfo`
11. `CalculateAsync_CrossRate_CallerProvidesCompositeRate` — verify service just uses what rateProvider returns
