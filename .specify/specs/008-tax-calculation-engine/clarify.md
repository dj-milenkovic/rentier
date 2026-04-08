# Feature 008 — Tax Calculation Engine: Clarifications

**Status**: Resolved  
**Date**: 2026-04-07  
**Feature**: TaxCalculationService — PP-OPO tax computation for a single income event  
**Method**: Autonomous resolution (questions provided by feature author; decisions made by analysis)

---

## Coverage Scan Summary

| Category | Status | Action |
|---|---|---|
| Functional Scope & Behavior | Partial → **Resolved** | Layer placement, error posture, zero-income edge case decided |
| Domain & Data Model | Missing → **Resolved** | FilingInfo layer, IncomeType enum, rounding policy, all field decisions made |
| Interaction & UX Flow | N/A | Headless domain service; no UI flow |
| Non-Functional Quality Attributes | Partial → **Resolved** | Rounding precision (0 dp, AwayFromZero) decided |
| Integration & External Dependencies | Partial → **Resolved** | Cross-rate responsibility assigned to caller; delegate contract pinned |
| Edge Cases & Failure Handling | Missing → **Resolved** | WHT clamp, zero income, cross-rate, rounding edge cases all decided |
| Constraints & Tradeoffs | Clear | decimal + DateOnly + DomainException pattern; no changes needed |
| Terminology & Consistency | Partial → **Resolved** | IncomeType canonical enum established; payingEntity confirmed as string |
| Completion Signals | Partial → **Resolved** | 100% domain test coverage target; acceptance scenarios documented |
| Misc / Placeholders | Partial → **Resolved** | Static vs instance, async suffix, method signature all pinned |

---

## Q1 — FilingInfo layer: Domain/ValueObjects or Application/DTOs?

**Decision**: **`Domain/ValueObjects/FilingInfo.cs`**

Rationale:
- `TaxCalculationService` lives in `Domain/Services/` (see Q2). A domain service cannot return a type from `Application`, since that would create an upward dependency violation (Domain → Application), which the constitution §I prohibits.
- `FilingInfo` carries computed tax figures that are pure domain facts: `grossIncomeRsd`, `whtPaidRsd`, `grossTaxPayableRsd`, `taxPayableRsd` — all of which enforce constitution §III (decimal, no double/float).
- `Application` already depends on `Domain`, so Application handlers can consume `FilingInfo` without any circular reference.
- Contrast with `StatementParseResult` (Feature 007): that type is a parse artefact from Infrastructure, so it lives in `Application/Parsing/`. `FilingInfo` is a domain computation result — different concern, different layer.

**Canonical shape**:
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

All `decimal` fields hold already-rounded whole-dinar values (see Q5). No negative values are possible in output (see Q4 clamp, Q9 zero-income).

---

## Q2 — TaxCalculationService layer: Domain/Services or Application/Services?

**Decision**: **`Domain/Services/TaxCalculationService.cs`**

Rationale:
- The calculation uses only domain types: `ExchangeRate` (Domain VO), `FilingInfo` (Domain VO), `IncomeType` (Domain enum), and a pure `Func<DateOnly, string, Task<ExchangeRate>>` delegate.
- The delegate introduces async I/O indirection without coupling `Domain` to any Application or Infrastructure interface. The `Func<>` type itself is a BCL type — no external references from `Domain`.
- Constitution §I: Domain MUST remain independent of external frameworks and I/O packages. A `Func<>` delegate satisfies this constraint.
- Placing the service in `Application` would be incorrect: the calculation contains no use-case orchestration, no repository access, no command/query routing. It is a pure computation over domain inputs.
- The Application layer creates a command handler (e.g., `CalculateTaxCommand` / `CalculateTaxCommandHandler`) that constructs the rate delegate, calls `TaxCalculationService.CalculateAsync(...)`, and wraps exceptions in `Result<FilingInfo, Error>`.

**Canonical location**: `src/Rentier.Domain/Services/TaxCalculationService.cs`

---

## Q3 — Cross-rate responsibility: service does it, or caller pre-composes the delegate?

**Decision**: **Caller pre-composes the delegate. The domain service calls the delegate exactly once per currency.**

Rationale:
- The delegate signature `Func<DateOnly, string, Task<ExchangeRate>>` returns an `ExchangeRate` with `RateToRsd`. This is a semantically complete rate; the domain service only needs one call per currency to obtain the multiplier for conversion to RSD.
- If the service itself did cross-rate math, it would need a second delegate for IBKR rates or a second parameter — breaking the clean single-delegate contract and leaking IBKR-specific knowledge into the domain.
- For NBS-traded currencies (e.g., USD, EUR): the Application handler provides a delegate backed by `IExchangeRateFetcher.FetchRateAsync`, which returns the NBS middle rate directly.
- For non-NBS currencies traded only on IBKR (e.g., a currency where NBS doesn't publish a rate): the Application handler provides a delegate that:
  1. Looks up `IbkrExchangeRate` for `(date, currency → USD)` from the parsed statement.
  2. Fetches USD→RSD from `IExchangeRateFetcher`.
  3. Computes `composite_rate_to_rsd = ibkr_rate × usd_to_rsd`.
  4. Returns a synthesised `ExchangeRate(date, currency, composite_rate_to_rsd)`.
- The domain service is unaware of this composition. It is the Application handler's responsibility to wire the correct delegate before calling `CalculateAsync`.

**Implication for inputs**: Two separate delegate calls are made internally — one for `incomeCurrency` and one for `whtCurrency`. If they are the same currency, both calls use the same currency string; the delegate implementation may cache.

---

## Q4 — Error posture: Result<FilingInfo, Error> or throw DomainException?

**Decision**: **Throw `DomainException` from the domain service; `Result<FilingInfo, Error>` at the Application handler boundary.**

Rationale:
- `Error` is defined in `Rentier.Application.Common`. If `TaxCalculationService` (Domain) returned `Result<FilingInfo, Error>`, it would require a Domain → Application reference, violating constitution §I.
- The established domain pattern is: domain entities and services throw `DomainException` for invariant violations; the Application layer catches these and maps to `Result.Failure(Error.Domain(...))`.
- Infrastructure exceptions from the rate delegate (e.g., network failure, rate not cached) are NOT domain exceptions. They propagate as-is and are caught at the Application handler boundary and mapped to `Result.Failure(Error.Infrastructure(...))`.

**Domain service error conditions** (each throws `DomainException`):
| Condition | Exception message |
|---|---|
| `incomeAmount < 0` | `"Income amount must be non-negative"` |
| `whtAmount < 0` | `"WHT amount must be non-negative"` |
| `incomeCurrency` is null/empty | `"Income currency must be non-empty"` |
| `whtCurrency` is null/empty | `"WHT currency must be non-empty"` |
| `payingEntity` is null/empty | `"Paying entity must be non-empty"` |

**Application handler maps to**:
- `DomainException` → `Result.Failure(Error.Domain(ex.Message))`
- Rate-fetch exception → `Result.Failure(Error.Infrastructure("Rate unavailable: " + ex.Message))`

---

## Q5 — Rounding: 0 or 2 decimal places for FilingInfo output?

**Decision**: **0 decimal places (whole dinars) using `MidpointRounding.AwayFromZero`. Intermediate calculations use full `decimal` precision.**

Rationale:
- PP-OPO form fields are whole-dinar integers. No fractional dinar is accepted or displayed.
- `MidpointRounding.AwayFromZero` is the standard banking/tax rounding convention and is consistent with how NBS publishes rates (4 dp source data rounded to a whole-dinar result).
- Intermediate values (raw conversion products) are NOT rounded mid-calculation to avoid compounding rounding errors.

**Rounding sequence**:
```csharp
// Full precision intermediate:
decimal grossIncomeRaw = incomeAmount * incomeRate.RateToRsd;
decimal whtRaw         = whtAmount   * whtRate.RateToRsd;

// Round each output field independently to 0 dp:
decimal grossIncomeRsd      = Math.Round(grossIncomeRaw, 0, MidpointRounding.AwayFromZero);
decimal whtPaidRsd          = Math.Round(whtRaw,         0, MidpointRounding.AwayFromZero);
decimal grossTaxPayableRsd  = Math.Round(grossIncomeRsd * 0.15m, 0, MidpointRounding.AwayFromZero);
decimal taxPayableRsd       = Math.Max(grossTaxPayableRsd - whtPaidRsd, 0m);
// taxPayableRsd is already a whole dinar (difference of two whole dinars, clamped ≥ 0)
```

`taxPayableRsd` needs no additional rounding since it is derived from two already-rounded whole-dinar values.

---

## Q6 — incomeType: new IncomeType enum or string?

**Decision**: **New `IncomeType` enum in `Domain/Enums/IncomeType.cs`.**

Rationale:
- Existing `ReportType` (IbkrCsv) classifies the import source format — not the tax income category. It is not reusable here.
- PP-OPO distinguishes at minimum `Dividend` (dividende) and `Interest` (kamata). An enum makes this distinction type-safe and prevents magic-string mismatches in tests and future UI display logic.
- New values can be added without changing the `FilingInfo` record shape.

**Canonical shape**:
```csharp
// src/Rentier.Domain/Enums/IncomeType.cs
public enum IncomeType
{
    Dividend = 0,
    Interest = 1
}
```

`IncomeType` is a required pass-through input to `CalculateAsync` and is stored verbatim in `FilingInfo.IncomeType`.

---

## Q7 — Static class or instance class for TaxCalculationService?

**Decision**: **Static class with static methods.**

Rationale:
- The service holds no state, no injected dependencies, and no configuration fields. An instance class would carry unnecessary lifecycle overhead and mislead readers into thinking state is managed.
- The delegate is passed per-call, not injected at construction time, making instance DI registration unnecessary.
- Consistent with idiomatic C# pure-computation helpers (e.g., `Math`, `DateOnly.Parse`).
- 100% testability is maintained: tests pass mock delegates directly to `CalculateAsync(...)` without any DI container setup.

**Declaration**:
```csharp
public static class TaxCalculationService
{
    public static async Task<FilingInfo> CalculateAsync(...) { ... }
}
```

---

## Q8 — Method signature: CalculateAsync with async lambda or synchronous core + async wrapper?

**Decision**: **`public static async Task<FilingInfo> CalculateAsync(...)` — the method is natively async.**

Rationale:
- The rate delegate is `Func<DateOnly, string, Task<ExchangeRate>>`. Awaiting it directly with `await` inside `CalculateAsync` is the correct pattern (constitution §IV: all async I/O MUST use `async Task`).
- A synchronous core with an async wrapper (Task.FromResult pattern) would require pre-awaiting the delegate outside the calculation and passing results in — needlessly complicating the call site.
- `Async` suffix is required by constitution §IV coding standard.

**Full method signature**:
```csharp
public static async Task<FilingInfo> CalculateAsync(
    decimal                                   incomeAmount,
    string                                    incomeCurrency,
    DateOnly                                  incomeDate,
    decimal                                   whtAmount,
    string                                    whtCurrency,
    IncomeType                                incomeType,
    string                                    payingEntity,
    Func<DateOnly, string, Task<ExchangeRate>> getRateAsync)
```

`CancellationToken` is intentionally omitted from the domain service signature: the delegate itself may accept a `CancellationToken` via closure if the Application handler captures one. Passing `ct` through the domain service would leak Application/Infrastructure concerns into the domain.

---

## Q9 — Zero income edge case: valid FilingInfo or throw?

**Decision**: **Return a valid `FilingInfo` with all monetary fields = 0 (zero decimal). Do NOT throw.**

Rationale:
- Zero income (e.g., a stock dividend that was fully reversed) is a legally valid filing event in Serbia. The PP-OPO form may legitimately report zero gross income and zero tax payable. Throwing would prevent a valid workflow.
- The rate delegate is still called (to obtain the RSD rate for the income currency), ensuring the output includes a meaningful `IncomeDate` and currency context even when amounts are zero.
- Validation rules (Q4): `incomeAmount >= 0` is the invariant, not `incomeAmount > 0`.

**Behaviour matrix**:
| Scenario | Result |
|---|---|
| `incomeAmount = 0`, `whtAmount = 0` | `FilingInfo` with all decimals = 0; rate delegate called |
| `incomeAmount = 0`, `whtAmount > 0` | `whtPaidRsd > 0`; `taxPayableRsd = 0` (clamped) |
| `incomeAmount < 0` | `DomainException` (Q4) |

---

## Q10 — payingEntity and incomeType as required inputs?

**Decision**: **Yes — both are required pass-through parameters to `CalculateAsync`.**

Rationale:
- `payingEntity` (e.g., "Apple Inc.", "Interactive Brokers") identifies the dividend/interest payer for the PP-OPO form. It is not derivable from the rate calculation and must be supplied by the caller from the parsed statement record.
- `incomeType` (Dividend or Interest, see Q6) is similarly a caller-supplied classification that flows verbatim into `FilingInfo.IncomeType`.
- Neither is defaultable; both are validated for non-null/non-empty (Q4).
- The domain service makes no attempt to infer either from other inputs.

---

## Architecture Decisions

### AD-1: File locations

```
src/Rentier.Domain/
  Enums/IncomeType.cs                        NEW
  ValueObjects/FilingInfo.cs                 NEW
  Services/TaxCalculationService.cs          NEW

tests/Rentier.Domain.Tests/
  Services/TaxCalculationServiceTests.cs     NEW
```

No Application layer files are added by this feature. The Application handler that calls the domain service is Feature 009 or a later use-case feature.

### AD-2: No Result<T> in Domain

`Result<T, Error>` MUST NOT appear in `Rentier.Domain`. `Error` is an Application type. Domain communicates failures exclusively via `DomainException`. Application handlers translate to `Result.Failure(Error.Domain(...))`.

### AD-3: Tax rate constant

The PP-OPO tax rate of **15%** (`0.15m`) is a domain constant encoded inline in `TaxCalculationService`. It is NOT injected, configurable, or overridable. If the Serbian tax rate changes, a code change with a spec update is required — this is intentional for auditability.

### AD-4: No EF migration needed

This feature is a pure domain computation service. No entity, repository, or persistence artefact is added.

### AD-5: Test coverage target

Constitution §V: Domain code MUST maintain **100%** rule/state coverage.

Required test cases:
| Test | Scenario |
|---|---|
| `CalculateAsync_HappyPath_ReturnsCorrectFilingInfo` | Normal dividend, USD direct NBS rate |
| `CalculateAsync_WhtExceedsGrossTax_ClampsTaxPayableToZero` | WHT > gross_tax → taxPayableRsd = 0 |
| `CalculateAsync_ZeroIncome_ReturnsAllZeros` | incomeAmount = 0, whtAmount = 0 |
| `CalculateAsync_CrossRate_ComputesCorrectRsd` | Non-NBS currency; delegate provides composite rate |
| `CalculateAsync_RoundingHalfUp_AppliesAwayFromZero` | Amount that triggers 0.5 rounding boundary |
| `CalculateAsync_NegativeIncomeAmount_ThrowsDomainException` | incomeAmount < 0 |
| `CalculateAsync_NegativeWhtAmount_ThrowsDomainException` | whtAmount < 0 |
| `CalculateAsync_EmptyPayingEntity_ThrowsDomainException` | payingEntity = "" |
| `CalculateAsync_EmptyIncomeCurrency_ThrowsDomainException` | incomeCurrency = "" |
| `CalculateAsync_WhtCurrencyDifferentFromIncomeCurrency_UsesCorrectRates` | whtCurrency ≠ incomeCurrency; two delegate calls |

---

## Explicit Out-of-Scope Decisions

| Item | Decision |
|---|---|
| Application command/query wrapper | Feature 009 or later use-case feature |
| Filing persistence / `IFilingRepository` | Future feature; this service only computes |
| PP-OPO XML generation | Separate feature |
| Multi-income-event aggregation | Caller aggregates multiple `FilingInfo` results |
| Deadline calculation | Separate domain service |
| Validation that delegate returns a rate for the correct currency | Caller responsibility; domain trusts the delegate |

---

## Assumptions

1. The rate delegate always returns an `ExchangeRate` with a **positive** `RateToRsd`; the `ExchangeRate` constructor already enforces this invariant, so `TaxCalculationService` need not re-validate.
2. `whtCurrency` may differ from `incomeCurrency` (e.g., income in EUR, WHT in USD at source). Two delegate calls are made in this case.
3. The decimal constant `0.15m` is used for the 15% PP-OPO rate. No floating-point literals.
4. `TaxCalculationService` is placed in `Rentier.Domain` — it MUST NOT reference any NuGet package (constitution §I).
5. Tests live in `Rentier.Domain.Tests` project. NSubstitute is used to mock the async rate delegate.
6. `MidpointRounding.AwayFromZero` is used consistently. `Math.Round` with two-argument overload is NOT used (it defaults to `ToEven`/banker's rounding, which is wrong for tax).
