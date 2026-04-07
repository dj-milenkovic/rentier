# Feature 008 — Tasks

## T001 — IncomeType enum
**File**: `src/Rentier.Domain/Enums/IncomeType.cs`
```csharp
namespace Rentier.Domain.Enums;
public enum IncomeType { Dividend, Interest }
```

## T002 — FilingInfo value object
**File**: `src/Rentier.Domain/ValueObjects/FilingInfo.cs`
```csharp
namespace Rentier.Domain.ValueObjects;
using Rentier.Domain.Enums;

public sealed record FilingInfo(
    IncomeType IncomeType,
    string     PayingEntity,
    DateOnly   IncomeDate,
    decimal    GrossIncomeRsd,
    decimal    WhtPaidRsd,
    decimal    GrossTaxPayableRsd,
    decimal    TaxPayableRsd);
```

## T003 — TaxCalculationService
**File**: `src/Rentier.Domain/Services/TaxCalculationService.cs`

Key implementation notes:
- CRITICAL: `rateProvider` called with `currency.ToUpperInvariant()` before calling
- CRITICAL: If `whtAmount == 0`, skip the WHT rate provider call entirely
- WHT currency check: only when `whtAmount > 0`
- `private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero)`
- `ct.ThrowIfCancellationRequested()` after each await

Full implementation in plan.md.

## T004 — TaxCalculationServiceTests
**File**: `tests/Rentier.Domain.Tests/Services/TaxCalculationServiceTests.cs`

Tests (all use `new ExchangeRate(date, currency, rate)` directly — no mocks needed):

```csharp
private static Func<DateOnly, string, Task<ExchangeRate>> RateProvider(decimal rateToRsd)
    => (date, currency) => Task.FromResult(new ExchangeRate(date, currency, rateToRsd));

private static Func<DateOnly, string, Task<ExchangeRate>> MultiRateProvider(
    Dictionary<string, decimal> rates)
    => (date, currency) => Task.FromResult(new ExchangeRate(date, currency, rates[currency]));
```

Test methods:
1. `CalculateAsync_Dividend_WithWht_ComputesAllAmountsCorrectly`
   - income=100, rate=117.21, wht=15, whtRate=117.21
   - GrossIncomeRsd = Round(100*117.21,2) = 11721.00
   - WhtPaidRsd = Round(15*117.21,2) = 1758.15
   - GrossTaxPayableRsd = Round(11721*0.15,2) = 1758.15
   - TaxPayableRsd = max(1758.15-1758.15,0) = 0.00

2. `CalculateAsync_WhtExceedsGrossTax_ClampsToZero`
   - income=100, rate=100, wht=20 (20% > 15%)
   - GrossTaxPayable = 1500, WhtPaid = 2000 → TaxPayable = 0

3. `CalculateAsync_ZeroIncome_ReturnsAllZeros`
   - incomeAmount=0 → all decimal fields = 0

4. `CalculateAsync_ZeroWht_DoesNotCallRateProviderTwice`
   - whtAmount=0 → rate provider called exactly once

5. `CalculateAsync_NegativeIncome_ThrowsDomainException`

6. `CalculateAsync_NegativeWht_ThrowsDomainException`

7. `CalculateAsync_WhtCurrencyMismatch_ThrowsDomainException`
   - incomeCurrency="USD", whtCurrency="EUR", whtAmount=5 → throws

8. `CalculateAsync_NullOrWhitespacePayingEntity_ThrowsDomainException` (Theory with null and "  ")

9. `CalculateAsync_NullRateProvider_ThrowsDomainException`

10. `CalculateAsync_PassesThroughIncomeTypeAndEntity`
    - IncomeType.Interest, "IBKR" → FilingInfo.IncomeType=Interest, .PayingEntity="IBKR"

11. `CalculateAsync_RoundingAwayFromZero_HalfPennyRoundsUp`
    - craft values producing exactly X.005 → rounds to X.01

## T005 — Build + test
```
dotnet build Rentier.slnx -warnaserror --no-incremental
dotnet test Rentier.slnx --filter "Category!=Integration" --no-build
```
Target: ≥186 tests, 0 errors, 0 warnings.
