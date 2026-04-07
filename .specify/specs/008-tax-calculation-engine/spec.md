# Feature Specification: Tax Calculation Engine

**Feature Branch**: `008-tax-calculation-engine`  
**Created**: 2026-04-07  
**Status**: Draft  
**Layer**: Domain only — no EF, no DI, no migrations

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Compute Tax for a Standard Foreign Income Event (Priority: P1)

A taxpayer in Serbia has received a foreign dividend or interest payment. The system computes the gross RSD-equivalent income, the RSD-equivalent withholding tax already paid, and the remaining Serbian tax payable — all rounded to **whole dinars** — ready for entry into the PP-OPO form.

**Why this priority**: This is the core computation the entire filing workflow depends on. All other stories are variations or failure paths of this fundamental calculation.

**Independent Test**: Fully tested by calling `CalculateAsync` with a known USD dividend amount, a mock NBS rate, and a known WHT amount, then asserting each field of the returned `FilingInfo`.

**Acceptance Scenarios**:

1. **Given** a valid dividend income of 100 USD on 2024-03-15, a 10% WHT of 10 USD, and a mock NBS USD→RSD rate of 108.5 RSD/USD, **When** `CalculateAsync` is called, **Then** `FilingInfo` is returned with `GrossIncomeRsd = 10850`, `WhtPaidRsd = 1085`, `GrossTaxPayableRsd = 1628` (15% of 10850, rounded to whole dinar), `TaxPayableRsd = 543` (1628 − 1085).

2. **Given** an interest income event with `IncomeType.Interest`, **When** `CalculateAsync` is called with valid parameters, **Then** `FilingInfo.IncomeType` equals `Interest` and all monetary fields are correctly computed to whole dinars.

3. **Given** valid inputs where WHT exactly equals gross tax payable, **When** `CalculateAsync` is called, **Then** `TaxPayableRsd = 0` (no additional tax owed).

---

### User Story 2 — Handle WHT Exceeding Serbian Tax Payable (Priority: P1)

When withholding tax already paid to a foreign jurisdiction exceeds the Serbian tax liability, the system returns zero additional tax payable rather than a negative value.

**Why this priority**: Without this clamp, the downstream filing form would contain an illegal negative tax value. This guard is a fundamental correctness requirement.

**Independent Test**: Call `CalculateAsync` with a WHT amount that converts to more RSD than 15% of gross income; assert `TaxPayableRsd = 0` and all other fields are non-negative.

**Acceptance Scenarios**:

1. **Given** gross income of 100 USD at 108 RSD/USD (`GrossIncomeRsd = 10800`), WHT of 20 USD at 108 (`WhtPaidRsd = 2160`), **When** `CalculateAsync` is called, **Then** `GrossTaxPayableRsd = 1620`, `TaxPayableRsd = 0` (clamped, not −540).

2. **Given** WHT in a different currency than income (e.g., income EUR, WHT USD), **When** `CalculateAsync` is called, **Then** each currency uses its own exchange rate via two separate delegate calls, and `TaxPayableRsd` is still clamped to zero if applicable.

---

### User Story 3 — Handle Zero Income (Priority: P2)

A fully reversed or cancelled income event (zero gross amount) still produces a valid, all-zero `FilingInfo` without throwing.

**Why this priority**: Serbia may require submission of a PP-OPO even for zero-value events (e.g., reversed dividends). Throwing an exception would block a legally required filing.

**Independent Test**: Call `CalculateAsync` with `incomeAmount = 0` and `whtAmount = 0`; assert all monetary fields in `FilingInfo` are zero and no exception is thrown.

**Acceptance Scenarios**:

1. **Given** `incomeAmount = 0` and `whtAmount = 0`, **When** `CalculateAsync` is called, **Then** a `FilingInfo` is returned with all decimal fields = 0, `IncomeType` and `PayingEntity` populated from inputs, rate delegate still called for income currency.

2. **Given** `incomeAmount = 0` and `whtAmount > 0`, **When** `CalculateAsync` is called, **Then** `GrossIncomeRsd = 0`, `WhtPaidRsd > 0`, `GrossTaxPayableRsd = 0`, `TaxPayableRsd = 0` (clamped).

---

### User Story 4 — Reject Invalid Domain Inputs (Priority: P1)

The service rejects inputs that violate domain invariants (negative amounts, empty strings) with a descriptive `DomainException` before any rate lookup is performed.

**Why this priority**: Domain invariant enforcement must be unconditional. A service that silently produces incorrect output on invalid input is more dangerous than one that fails fast.

**Independent Test**: Call `CalculateAsync` with each invalid input in isolation; assert `DomainException` is thrown before the rate delegate is ever invoked.

**Acceptance Scenarios**:

1. **Given** `incomeAmount = -1`, **When** `CalculateAsync` is called, **Then** `DomainException` is thrown with message `"Income amount must be non-negative"`.
2. **Given** `whtAmount = -0.01m`, **When** `CalculateAsync` is called, **Then** `DomainException` is thrown with message `"WHT amount must be non-negative"`.
3. **Given** `incomeCurrency = ""`, **When** `CalculateAsync` is called, **Then** `DomainException` is thrown with message `"Income currency must be non-empty"`.
4. **Given** `whtCurrency = null`, **When** `CalculateAsync` is called, **Then** `DomainException` is thrown with message `"WHT currency must be non-empty"`.
5. **Given** `payingEntity = ""`, **When** `CalculateAsync` is called, **Then** `DomainException` is thrown with message `"Paying entity must be non-empty"`.

---

### Edge Cases

- What happens when `whtAmount = 0`? → `WhtPaidRsd = 0`; the rate delegate is **not** called for the WHT currency (skip delegate call for zero WHT).
- What happens when `incomeCurrency` equals `whtCurrency`? → Two delegate calls are still made with the same string; the delegate may cache.
- What happens when `incomeCurrency ≠ whtCurrency`? → Two separate delegate calls are made, one for each currency string; both are valid.
- What happens when the rate delegate throws? → Exception propagates as-is (not a `DomainException`). Caller maps it to an infrastructure error at the Application boundary.
- What happens when a conversion product lands exactly on 0.5? → `MidpointRounding.AwayFromZero` rounds up (e.g., 0.5 → 1, 1.5 → 2).
- What happens when `taxPayableRsd` would be negative after subtraction? → `Math.Max(..., 0m)` clamps to zero; no additional rounding needed.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The domain service MUST accept `incomeType`, `payingEntity`, `incomeDate`, `incomeAmount`, `incomeCurrency`, `whtAmount`, `whtCurrency`, `rateProvider`, and `ct` as inputs (see method signature in Data Model).
- **FR-002**: The domain service MUST throw `DomainException("Income amount must be non-negative")` when `incomeAmount < 0`.
- **FR-003**: The domain service MUST throw `DomainException("WHT amount must be non-negative")` when `whtAmount < 0`.
- **FR-004**: The domain service MUST throw `DomainException("Income currency must be non-empty")` when `incomeCurrency` is null or empty.
- **FR-005**: The domain service MUST throw `DomainException("WHT currency must be non-empty")` when `whtCurrency` is null or empty.
- **FR-006**: The domain service MUST throw `DomainException("Paying entity must be non-empty")` when `payingEntity` is null or empty.
- **FR-007**: All input validation MUST occur before any rate delegate call is made.
- **FR-008**: The domain service MUST invoke `rateProvider(incomeDate, incomeCurrency)` to obtain the income exchange rate.
- **FR-009**: The domain service MUST invoke `rateProvider(incomeDate, whtCurrency)` to obtain the WHT exchange rate, **unless** `whtAmount = 0` (skip delegate call when WHT is zero).
- **FR-010**: `GrossIncomeRsd` MUST equal `Math.Round(incomeAmount × incomeRate.RateToRsd, 0, MidpointRounding.AwayFromZero)`.
- **FR-011**: `WhtPaidRsd` MUST equal `Math.Round(whtAmount × whtRate.RateToRsd, 0, MidpointRounding.AwayFromZero)`, or `0m` when `whtAmount = 0`.
- **FR-012**: `GrossTaxPayableRsd` MUST equal `Math.Round(GrossIncomeRsd × 0.15m, 0, MidpointRounding.AwayFromZero)`.
- **FR-013**: `TaxPayableRsd` MUST equal `Math.Max(GrossTaxPayableRsd − WhtPaidRsd, 0m)`. Negative results MUST be clamped to zero; no further rounding is applied.
- **FR-014**: The domain service MUST return a `FilingInfo` value object containing all four computed monetary fields plus the pass-through inputs (`IncomeType`, `PayingEntity`, `IncomeDate`).
- **FR-015**: When `incomeAmount = 0`, the domain service MUST return a valid `FilingInfo` with all monetary fields equal to zero; no exception is thrown.
- **FR-016**: The domain service MUST be a static class with a single public async method (`CalculateAsync`); no instance construction and no dependency injection.
- **FR-017**: The Serbian PP-OPO tax rate of 15% (`0.15m`) MUST be a domain constant encoded inline; it MUST NOT be injected, configurable, or overridable at runtime.
- **FR-018**: All intermediate multiplication results MUST retain full `decimal` precision; rounding is applied only to the four final output fields.
- **FR-019**: `whtCurrency` MAY differ from `incomeCurrency`; both currencies are valid as separate delegate calls; no currency-equality constraint is enforced.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: New types added exclusively to `Rentier.Domain`. No Application or Infrastructure layer files are created by this feature. `TaxCalculationService` uses a `Func<>` delegate (BCL type only) so `Rentier.Domain` acquires zero new external package references. Clean Architecture boundary (Domain not referencing Application or Infrastructure) is preserved.
- **CA-002 (Money and Dates)**: All monetary fields in `FilingInfo` use `decimal`. `IncomeDate` uses `DateOnly`. No `double`, `float`, or `DateTime` types anywhere. The tax rate constant is `0.15m` (decimal literal). The two-argument `Math.Round(x, 0)` overload (banker's rounding) is explicitly forbidden; `Math.Round(x, 0, MidpointRounding.AwayFromZero)` is mandatory.
- **CA-003 (Privacy and Security)**: No personal data is persisted by this feature. `FilingInfo` is a computed value object returned in-memory only. Local-first principle is unaffected.
- **CA-004 (Network Scope)**: The domain service makes no outbound calls. Network I/O is entirely delegated to the `Func<DateOnly, string, Task<ExchangeRate>>` provided by the caller. Domain remains free of direct I/O dependencies.
- **CA-005 (Async and UI)**: `CalculateAsync` is natively async and awaits the rate delegate inline. No blocking `.Result` or `.Wait()` calls. No UI interaction.
- **CA-006 (Testing Impact)**: New test file `tests/Rentier.Domain.Tests/Services/TaxCalculationServiceTests.cs`. Must achieve 100% branch/state coverage of `TaxCalculationService`, `FilingInfo`, and `IncomeType` per constitution §V. NSubstitute mock delegates (or inline lambda mocks) replace real rate providers in all unit tests.

### Key Entities

- **`FilingInfo`** (Domain Value Object — NEW): Immutable result of a single tax calculation. Carries pass-through identity fields (`IncomeType`, `PayingEntity`, `IncomeDate`) and four computed whole-dinar monetary fields.
- **`IncomeType`** (Domain Enum — NEW): Type-safe classification of income category for PP-OPO. Values: `Dividend = 0`, `Interest = 1`.
- **`TaxCalculationService`** (Domain Service — NEW): Stateless static class. Contains no mutable state, no stored dependencies.
- **`ExchangeRate`** (Domain Value Object — EXISTING): `(DateOnly Date, string Currency, decimal RateToRsd)`. Provided by the rate delegate. `RateToRsd > 0` enforced by its constructor.
- **`DomainException`** (Domain Exception — EXISTING): Thrown for all invariant violations within the domain service.

---

## Data Model

### New Files

| File | Type | Notes |
|------|------|-------|
| `src/Rentier.Domain/Enums/IncomeType.cs` | Enum | `Dividend = 0`, `Interest = 1` |
| `src/Rentier.Domain/ValueObjects/FilingInfo.cs` | Sealed record | Seven-field immutable value object |
| `src/Rentier.Domain/Services/TaxCalculationService.cs` | Static class | Single public method `CalculateAsync` |
| `tests/Rentier.Domain.Tests/Services/TaxCalculationServiceTests.cs` | xUnit test class | 100% branch coverage required |

### No changes to existing Domain files

### `FilingInfo` Shape

```csharp
// src/Rentier.Domain/ValueObjects/FilingInfo.cs
public sealed record FilingInfo(
    IncomeType  IncomeType,
    string      PayingEntity,
    DateOnly    IncomeDate,
    decimal     GrossIncomeRsd,
    decimal     WhtPaidRsd,
    decimal     GrossTaxPayableRsd,
    decimal     TaxPayableRsd);
```

All four decimal fields hold already-rounded whole-dinar values (≥ 0). No negative values can appear in output.

### `IncomeType` Shape

```csharp
// src/Rentier.Domain/Enums/IncomeType.cs
public enum IncomeType
{
    Dividend = 0,
    Interest = 1
}
```

### `TaxCalculationService` Method Signature

```csharp
// src/Rentier.Domain/Services/TaxCalculationService.cs
public static class TaxCalculationService
{
    public static async Task<FilingInfo> CalculateAsync(
        IncomeType                                incomeType,
        string                                    payingEntity,
        DateOnly                                  incomeDate,
        decimal                                   incomeAmount,
        string                                    incomeCurrency,
        decimal                                   whtAmount,
        string                                    whtCurrency,
        Func<DateOnly, string, Task<ExchangeRate>> rateProvider,
        CancellationToken                         ct = default)
}
```

---

## Algorithm

### Pre-conditions (validate before any delegate call — throw `DomainException` if violated)

| Check | Exception Message |
|-------|-------------------|
| `incomeAmount < 0` | `"Income amount must be non-negative"` |
| `whtAmount < 0` | `"WHT amount must be non-negative"` |
| `string.IsNullOrEmpty(incomeCurrency)` | `"Income currency must be non-empty"` |
| `string.IsNullOrEmpty(whtCurrency)` | `"WHT currency must be non-empty"` |
| `string.IsNullOrEmpty(payingEntity)` | `"Paying entity must be non-empty"` |

### Computation Steps

```
Step 1 — Fetch income rate
  incomeRate ← await rateProvider(incomeDate, incomeCurrency)

Step 2 — Compute GrossIncomeRsd  (whole dinars)
  grossIncomeRaw = incomeAmount × incomeRate.RateToRsd       // full decimal precision
  GrossIncomeRsd = Math.Round(grossIncomeRaw, 0, MidpointRounding.AwayFromZero)

Step 3 — Compute WhtPaidRsd  (whole dinars, skip delegate when whtAmount = 0)
  if whtAmount = 0:
      WhtPaidRsd ← 0m
  else:
      whtRate ← await rateProvider(incomeDate, whtCurrency)
      whtRaw     = whtAmount × whtRate.RateToRsd             // full decimal precision
      WhtPaidRsd = Math.Round(whtRaw, 0, MidpointRounding.AwayFromZero)

Step 4 — Compute GrossTaxPayableRsd  (whole dinars)
  GrossTaxPayableRsd = Math.Round(GrossIncomeRsd × 0.15m, 0, MidpointRounding.AwayFromZero)

Step 5 — Compute TaxPayableRsd  (clamped whole dinars, no further rounding needed)
  TaxPayableRsd = Math.Max(GrossTaxPayableRsd − WhtPaidRsd, 0m)

Step 6 — Return
  return new FilingInfo(incomeType, payingEntity, incomeDate,
                        GrossIncomeRsd, WhtPaidRsd, GrossTaxPayableRsd, TaxPayableRsd)
```

**Critical rounding rule**: Always use `Math.Round(x, 0, MidpointRounding.AwayFromZero)`. The two-argument overload `Math.Round(x, 0)` defaults to banker's rounding (`ToEven`) and MUST NOT be used.

**Tax rate**: `0.15m` — Serbian PP-OPO capital income rate. Inline constant; not configurable.

**`TaxPayableRsd`**: No additional `Math.Round` call is needed because it is derived from the difference of two already-rounded whole-dinar values.

---

## Error Table

| Condition | Thrown By | Type | Message |
|-----------|-----------|------|---------|
| `incomeAmount < 0` | `TaxCalculationService` | `DomainException` | `"Income amount must be non-negative"` |
| `whtAmount < 0` | `TaxCalculationService` | `DomainException` | `"WHT amount must be non-negative"` |
| `incomeCurrency` null or empty | `TaxCalculationService` | `DomainException` | `"Income currency must be non-empty"` |
| `whtCurrency` null or empty | `TaxCalculationService` | `DomainException` | `"WHT currency must be non-empty"` |
| `payingEntity` null or empty | `TaxCalculationService` | `DomainException` | `"Paying entity must be non-empty"` |
| Rate delegate throws (network failure, cache miss, etc.) | Caller-provided delegate | Any (not `DomainException`) | Propagates as-is; mapped to `Error.Infrastructure(...)` at Application boundary |
| `ExchangeRate` constructed with `RateToRsd ≤ 0` | `ExchangeRate` constructor | `DomainException` | `"RateToRsd must be positive, got {value}"` |

---

## Acceptance Criteria

| ID | Scenario | Expected Result |
|----|----------|-----------------|
| AC-001 | USD income 100, NBS rate 117.00 RSD/USD, WHT 10 USD same rate | `GrossIncomeRsd=11700`, `WhtPaidRsd=1170`, `GrossTaxPayableRsd=1755`, `TaxPayableRsd=585` |
| AC-002 | WHT (20 USD, rate 108) exceeds gross tax (100 USD, rate 108 → gross tax 1620) | `TaxPayableRsd=0` (clamped from −540) |
| AC-003 | `incomeAmount=0`, `whtAmount=0` | All decimal fields = 0; no exception; `FilingInfo` returned |
| AC-004 | `incomeAmount=0`, `whtAmount=5`, rate 100 | `GrossIncomeRsd=0`, `WhtPaidRsd=500`, `GrossTaxPayableRsd=0`, `TaxPayableRsd=0` |
| AC-005 | `whtAmount=0` (skip WHT rate call) | Delegate called exactly once (income currency only) |
| AC-006 | `incomeCurrency="USD"`, `whtCurrency="EUR"` with different rates | Two separate delegate calls; each currency converted at its own rate |
| AC-007 | Composite rate delegate (non-NBS currency, synthetic `RateToRsd`) | Correct `GrossIncomeRsd` using composite rate |
| AC-008 | Rounding boundary: conversion product = 0.5 | Rounds to 1 (AwayFromZero); not 0 (ToEven) |
| AC-009 | `incomeAmount = -1` | `DomainException("Income amount must be non-negative")` thrown; delegate not called |
| AC-010 | `whtAmount = -0.01m` | `DomainException("WHT amount must be non-negative")` thrown; delegate not called |
| AC-011 | `incomeCurrency = ""` | `DomainException("Income currency must be non-empty")` thrown |
| AC-012 | `whtCurrency = null` | `DomainException("WHT currency must be non-empty")` thrown |
| AC-013 | `payingEntity = ""` | `DomainException("Paying entity must be non-empty")` thrown |
| AC-014 | Valid call with `IncomeType.Interest`, specific `PayingEntity` and `IncomeDate` | `FilingInfo.IncomeType`, `.PayingEntity`, `.IncomeDate` match inputs verbatim |

---

## Test Cases

All tests in `tests/Rentier.Domain.Tests/Services/TaxCalculationServiceTests.cs`.  
**Target: 100% branch/state coverage** of `TaxCalculationService`, `FilingInfo`, `IncomeType`.

| Test Name | Scenario | Expected Outcome |
|-----------|----------|-----------------|
| `CalculateAsync_HappyPath_Dividend_ReturnsCorrectFilingInfo` | Normal dividend, USD, direct NBS rate, partial WHT | All four monetary fields computed correctly; `IncomeType = Dividend` |
| `CalculateAsync_HappyPath_Interest_ReturnsCorrectFilingInfo` | Normal interest, EUR, direct NBS rate, no WHT | `WhtPaidRsd = 0`; `TaxPayableRsd = GrossTaxPayableRsd`; `IncomeType = Interest` |
| `CalculateAsync_WhtExceedsGrossTax_ClampsTaxPayableToZero` | WHT RSD > gross tax payable | `TaxPayableRsd = 0` (not negative) |
| `CalculateAsync_WhtEqualsGrossTax_TaxPayableIsZero` | WHT RSD = gross tax payable exactly | `TaxPayableRsd = 0` |
| `CalculateAsync_ZeroIncome_ZeroWht_ReturnsAllZeroMonetaryFields` | `incomeAmount = 0`, `whtAmount = 0` | All decimal fields = 0; no exception; income rate delegate called |
| `CalculateAsync_ZeroIncome_NonZeroWht_TaxPayableIsZero` | `incomeAmount = 0`, `whtAmount > 0` | `GrossIncomeRsd = 0`, `WhtPaidRsd > 0`, `TaxPayableRsd = 0` |
| `CalculateAsync_ZeroWht_SkipsWhtRateDelegate` | `whtAmount = 0` | Delegate called exactly once (income currency only) |
| `CalculateAsync_DifferentIncomeCurrencyAndWhtCurrency_TwoDelegateCalls` | `incomeCurrency = "USD"`, `whtCurrency = "EUR"` | Delegate called twice; correct rates applied per currency |
| `CalculateAsync_CrossRate_CompositeRateToRsd` | Non-NBS currency; delegate returns synthetic composite rate | Correct `GrossIncomeRsd` using composite `RateToRsd` |
| `CalculateAsync_RoundingHalfUp_AwayFromZero` | Conversion product exactly at 0.5 boundary | Output rounded away from zero (0.5 → 1, not 0) |
| `CalculateAsync_NegativeIncomeAmount_ThrowsDomainException` | `incomeAmount = -0.01m` | `DomainException("Income amount must be non-negative")`; delegate not called |
| `CalculateAsync_NegativeWhtAmount_ThrowsDomainException` | `whtAmount = -1m` | `DomainException("WHT amount must be non-negative")`; delegate not called |
| `CalculateAsync_EmptyIncomeCurrency_ThrowsDomainException` | `incomeCurrency = ""` | `DomainException("Income currency must be non-empty")`; delegate not called |
| `CalculateAsync_NullIncomeCurrency_ThrowsDomainException` | `incomeCurrency = null` | `DomainException("Income currency must be non-empty")`; delegate not called |
| `CalculateAsync_EmptyWhtCurrency_ThrowsDomainException` | `whtCurrency = ""` | `DomainException("WHT currency must be non-empty")`; delegate not called |
| `CalculateAsync_EmptyPayingEntity_ThrowsDomainException` | `payingEntity = ""` | `DomainException("Paying entity must be non-empty")`; delegate not called |
| `CalculateAsync_ValidationThrowsBeforeRateDelegate_NoDelegateCalls` | Any invalid input | Rate delegate is never invoked when validation throws |
| `CalculateAsync_FilingInfoPassThroughFields_ArePreserved` | Valid call with specific `IncomeType`, `PayingEntity`, `IncomeDate` | `FilingInfo.IncomeType`, `.PayingEntity`, `.IncomeDate` match inputs verbatim |

---

## Out of Scope

| Item | Disposition |
|------|-------------|
| Application command/query handler (`CalculateTaxCommand`) | Feature 009 or later |
| Filing persistence / `IFilingRepository` | Future feature |
| PP-OPO XML document generation | Separate feature |
| Multi-income-event aggregation | Caller aggregates multiple `FilingInfo` results externally |
| Filing deadline calculation | Separate domain service |
| Rate delegate caching or cross-rate composition logic | Application layer responsibility |
| NBS rate fetching from external API | Infrastructure layer (Feature 006 or earlier) |
| Validation that delegate returns rate for the correct currency | Caller responsibility; domain trusts the delegate contract |
| EF Core migrations or database persistence | Not applicable — pure domain computation |
| Dependency injection container registration | Not applicable — static class |
| UI display of `FilingInfo` | Desktop layer feature |

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Given any valid single income event, the four monetary RSD fields in `FilingInfo` are whole-dinar values that exactly match hand-computed PP-OPO values using `MidpointRounding.AwayFromZero`.
- **SC-002**: All 18 specified test cases pass with a green build on the first run after implementation.
- **SC-003**: Domain test project achieves 100% branch coverage of `TaxCalculationService`, `FilingInfo`, and `IncomeType` as reported by the project's coverage tool.
- **SC-004**: No `DomainException` is thrown for the zero-income edge case; a valid all-zero `FilingInfo` is returned.
- **SC-005**: For every invalid-input test case, `DomainException` is thrown before the rate delegate is invoked (verified by asserting zero delegate invocations in those tests).
- **SC-006**: `Rentier.Domain` project continues to build with zero new external NuGet references after this feature is merged.
- **SC-007**: No `FilingInfo` ever contains a negative `TaxPayableRsd` regardless of input combination (WHT clamp always applied).

---

## Assumptions

1. The rate delegate always returns an `ExchangeRate` with `RateToRsd > 0`; the `ExchangeRate` constructor enforces this invariant, so `TaxCalculationService` need not re-validate the rate value.
2. `whtCurrency` may be the same as or different from `incomeCurrency`; both scenarios are valid and produce correct results via independent delegate calls.
3. The decimal constant `0.15m` represents the current Serbian PP-OPO capital income tax rate. If the rate changes by law, a code change with a spec update is required — intentional for tax-law auditability.
4. `TaxCalculationService` must not reference any NuGet package; it depends only on BCL types (`Task`, `Func<>`, `Math`, `decimal`, `DateOnly`, `string`) and other `Rentier.Domain` types.
5. Tests use NSubstitute (or equivalent inline lambda mocks) to provide the rate delegate; no real HTTP calls are made in unit tests.
6. `MidpointRounding.AwayFromZero` is the correct and mandatory rounding mode for Serbian PP-OPO tax forms. The two-argument `Math.Round(x, 0)` overload (banker's rounding `ToEven`) MUST NOT be used.
7. The Application layer command handler that wires `TaxCalculationService` to the IBKR statement import pipeline is out of scope for this feature (Feature 009+).
8. Callers that need cancellation propagation into the rate delegate may capture a `CancellationToken` in the delegate closure; the `ct` parameter is passed through to allow this pattern.
