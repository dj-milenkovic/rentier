# Feature 009 — Filing Deadline Calculator: Clarifications

**Status**: Resolved  
**Date**: 2026-05-30  
**Feature**: Filing Deadline Calculator — pure domain logic, `CalculateDeadline(DateOnly incomeDate, HolidayConf holidays) → DateOnly`  
**Method**: Autonomous resolution (all decisions pre-provided by author)

---

## Coverage Scan Summary

| Category | Status | Action |
|---|---|---|
| Functional Scope & Behavior | Partial → **Resolved** | Loop semantics, no-double-skip rule, and Friday-before-holiday behaviour all pinned |
| Domain & Data Model | Missing → **Resolved** | Class placement, method signature, safety guard, and holiday comparison all decided |
| Interaction & UX Flow | N/A | Pure function; no UI flow |
| Non-Functional Quality Attributes | Partial → **Resolved** | Safety guard (14-iteration cap + DomainException) and caller-trust posture decided |
| Integration & External Dependencies | N/A | No external dependencies; `HolidayConf` is the sole input beyond `DateOnly` |
| Edge Cases & Failure Handling | Partial → **Resolved** | All six edge cases from feature request addressed; safety guard handles pathological configs |
| Constraints & Tradeoffs | Clear | `DateOnly` + no I/O + `Rentier.Domain` only; no changes needed |
| Terminology & Consistency | Partial → **Resolved** | `FilingDeadlineCalculator` established as canonical class name |
| Completion Signals | Partial → **Resolved** | 100 % coverage via xUnit Theory `[InlineData]`, one test per rule, confirmed |
| Misc / Placeholders | Clear | No TODOs or vague adjectives; feature request is self-contained |

---

## Q1 — Where should `CalculateDeadline` live: method on `HolidayConf` or separate domain service?

**Decision**: **Separate static class `FilingDeadlineCalculator` in `Rentier.Domain/Services/`.**

Rationale:
- `HolidayConf` is a data-carrying value object whose only responsibility is to hold a validated, non-null `IReadOnlyList<DateOnly>`. Adding a calculation method would inflate it into a mixed value-object-plus-service type, violating SRP.
- The method signature `CalculateDeadline(DateOnly incomeDate, HolidayConf holidays)` treats `HolidayConf` as an input argument — a natural fit for a free-standing domain service, not a method receiver.
- Domain services in `Domain/Services/` are the idiomatic location for calculations that operate across value objects without I/O. The constitution places "domain rules, including deadline calculations, in domain entities/value objects" — a static domain service class fulfils this intent without polluting `HolidayConf`.
- `HolidayConf` remains unchanged.

---

## Q2 — Loop semantics: re-check weekends before checking holidays on each iteration?

**Decision**: **Yes — single combined loop re-checks both conditions on every iteration.**

Canonical algorithm:
```
candidate ← incomeDate.AddDays(30)
iterations ← 0
while IsWeekend(candidate) OR holidays.Holidays.Contains(candidate):
    iterations ← iterations + 1
    if iterations > 14: throw DomainException(...)
    candidate ← candidate.AddDays(1)
return candidate
```

`IsWeekend(d)` returns `true` when `d.DayOfWeek` is `Saturday` or `Sunday`.

Advancing by one day at a time (not jumping directly to Monday) ensures a holiday that immediately follows a weekend is caught in the same pass without any special case branching. The loop terminates as soon as the candidate is simultaneously a weekday and not a configured holiday.

---

## Q3 — Holiday on Saturday: confirm "no double-skip" semantics

**Decision**: **The loop evaluates only the current candidate date on each iteration. A holiday that coincidentally falls on a date that was already skipped (e.g., a Saturday entry in `HolidayConf`) has no additional effect.**

Concrete walkthrough — raw deadline lands on Saturday 1 Jan (New Year, also a holiday):
1. Iteration 1: candidate = Sat 1 Jan → `IsWeekend` = true → advance to Sun 2 Jan.
2. Iteration 2: candidate = Sun 2 Jan (also a holiday in HolidayConf) → `IsWeekend` = true → advance to Mon 3 Jan.
3. Iteration 3: candidate = Mon 3 Jan → `IsWeekend` = false, `Contains` = false → **stop. Return Mon 3 Jan.**

The fact that Sat 1 Jan was also in `HolidayConf` did **not** trigger an extra advance; the loop already moved past it via the weekend check. The `Contains` check only has an observable effect when the candidate is a **weekday** that appears in `HolidayConf`.

---

## Q4 — Safety guard: maximum iteration cap?

**Decision**: **Yes — cap at 14 iterations. Exceeding the cap throws `DomainException`.**

```csharp
throw new DomainException(
    "Filing deadline calculation exceeded the 14-day safety limit. " +
    "Check HolidayConf for a pathological holiday configuration.");
```

Rationale: 14 days covers the maximum realistic skip chain documented in the feature request (5 consecutive holidays + weekend = 7 days) with a 2× safety margin. An infinite loop from a misconfigured all-holidays `HolidayConf` would be a silent, hard-to-diagnose bug. `DomainException` surfaces the issue loudly, consistent with the constitution's invariant-violation pattern.

---

## Q5 — Input validation: throw on `DateOnly.MinValue` or trust the caller?

**Decision**: **Trust the caller. No input validation on `incomeDate`.**

Rationale:
- `DateOnly` is a value type; any `DateOnly` value is structurally valid. The domain has no semantic definition of an "invalid" date for this calculation.
- `DateOnly.MinValue.AddDays(30)` is a well-defined operation (`0001-01-31`) — it does not throw. No defensive guard is warranted.
- Adding a guard would require the domain to define what constitutes a "meaningful" date, which is an application-layer concern. Callers (Application handlers) are responsible for passing a real income date.
- `HolidayConf`'s own constructor already guards against a `null` holidays list; no additional guard is needed inside the calculator.

---

## Q6 — Holiday comparison: use `DateOnly` value equality?

**Decision**: **Yes — `holidays.Holidays.Contains(candidate)` uses `DateOnly`'s built-in value equality.**

`DateOnly` is a struct with value semantics. `IReadOnlyList<DateOnly>.Contains()` compares by value. No custom equality comparer is needed. This is consistent with how `HolidayConf` is already used throughout the domain (constitution §III: "All business dates MUST use `DateOnly`").

---

## Q7 — Multi-day holidays: separate entries vs. range?

**Decision**: **Each calendar day of a multi-day holiday period is a separate `DateOnly` entry in `HolidayConf.Holidays`. No range-based representation.**

Examples:
- New Year (1–2 Jan) → two entries: `2026-01-01`, `2026-01-02`
- Labour Day (1–2 May) → two entries: `2026-05-01`, `2026-05-02`

`FilingDeadlineCalculator` requires no special handling — it checks one date at a time via `.Contains()`. This is consistent with the existing `HolidayConf` design and the A-018 seed data defined in the Feature 003 clarifications.

---

## Q8 — Is the returned date inclusive (deadline = last filing day)?

**Decision**: **Yes — the returned `DateOnly` is the last valid filing day (inclusive).**

The deadline is the PP-OPO submission due date itself; filing on that day is valid. The calculator returns the adjusted date and does not subtract one. Downstream display logic (e.g., "Due: 2026-03-02") shows it without further adjustment.

---

## Q9 — Test parametrization approach

**Decision**: **xUnit `Theory` + `[InlineData]` in `tests/Rentier.Domain.Tests/Services/FilingDeadlineCalculatorTests.cs`.**

Required test cases (minimum one per rule, covering all six specified edge cases):

| Test case | incomeDate | holidayDates | Expected deadline | Rule covered |
|---|---|---|---|---|
| Basic — no adjustment needed | 2026-01-01 | (empty) | 2026-01-31 (Saturday) → Sun → Mon → **2026-02-02** | Weekend skip |
| +30 lands on Saturday → Monday | 2025-12-03 | (empty) | +30 = 2026-01-02 (Friday) → no adjust → **2026-01-02** | No-op |
| +30 lands on Saturday | 2026-01-02 | (empty) | +30 = 2026-02-01 (Sun) → **2026-02-02** (Mon) | Sunday skip |
| +30 lands on holiday (weekday) | 2026-01-06 | [2026-02-05] | +30 = 2026-02-05 (Thu, holiday) → **2026-02-06** | Holiday skip |
| Consecutive holidays | 2025-12-02 | [2026-01-01, 2026-01-02] | +30 = 2026-01-01 (Thu, holiday) → 2026-01-02 (Fri, holiday) → **2026-01-03** | Consecutive |
| Holiday on Saturday (no double-skip) | 2025-12-06 | [2026-01-03] | +30 = 2026-01-05 (Mon) → **2026-01-05** (holiday is Sat; not on Mon) | No double-skip |
| Leap year Feb 28 → Mar 29 | 2024-01-29 | (empty) | +30 = 2024-02-28 (Wed) → **2024-02-28** | Leap year |
| Leap year Feb 29 | 2024-01-30 | (empty) | +30 = 2024-02-29 (Thu) → **2024-02-29** | Leap year |
| Empty holiday list | 2026-02-01 | (empty) | +30 = 2026-03-03 (Tue) → **2026-03-03** | Empty conf |
| Friday before holiday Monday (OK) | 2026-01-09 | [2026-02-09] | +30 = 2026-02-08 (Sun) → 2026-02-09 (Mon, holiday) → **2026-02-10** (Tue) | Friday-before-holiday |
| Max skip chain (5 holidays + weekend) | chosen | [h1..h5] | advances 7 days max | Safety guard boundary |

Naming convention: `CalculateDeadline_<StateUnderTest>_<ExpectedBehavior>` per constitution testing standards.

---

## Q10 — Class name: `DeadlineCalculator` or `FilingDeadlineCalculator`?

**Decision**: **`FilingDeadlineCalculator`.**

Rationale:
- The domain glossary already defines "Filing Deadline" as the canonical term ("Payment date + 30 days, adjusted for weekends/holidays").
- `FilingDeadlineCalculator` is unambiguous in scope — it calculates PP-OPO filing deadlines, not generic business day offsets. A future `TaxReturnDeadlineCalculator` or similar would be clearly distinct.
- The ROADMAP uses "DeadlineCalculator" informally, but the feature title "009 · Filing Deadline Calculator" makes the canonical name clear.

---

## Architecture Decisions

### AD-1: File locations

```
src/Rentier.Domain/
  Services/FilingDeadlineCalculator.cs           NEW — public static class

tests/Rentier.Domain.Tests/
  Services/FilingDeadlineCalculatorTests.cs      NEW — xUnit Theory + [InlineData]
```

No other files added or modified. `HolidayConf.cs` and `PublicHoliday.cs` are untouched.

### AD-2: Canonical class and method signature

```csharp
// src/Rentier.Domain/Services/FilingDeadlineCalculator.cs
namespace Rentier.Domain.Services;

public static class FilingDeadlineCalculator
{
    private const int MaxIterations = 14;

    public static DateOnly CalculateDeadline(DateOnly incomeDate, HolidayConf holidays)
    {
        var candidate = incomeDate.AddDays(30);
        var iterations = 0;

        while (IsWeekend(candidate) || holidays.Holidays.Contains(candidate))
        {
            if (++iterations > MaxIterations)
                throw new DomainException(
                    "Filing deadline calculation exceeded the 14-day safety limit. " +
                    "Check HolidayConf for a pathological holiday configuration.");

            candidate = candidate.AddDays(1);
        }

        return candidate;
    }

    private static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
```

### AD-3: No new NuGet packages

`FilingDeadlineCalculator` uses only `System` BCL types (`DateOnly`, `DayOfWeek`) and the existing `DomainException`. `Rentier.Domain` remains free of external package references.

### AD-4: No EF migration needed

Pure domain logic; no persistence layer changes.

### AD-5: `HolidayConf` is unchanged

The value object is consumed as-is. No new properties, constructors, or methods are added to `HolidayConf`. This decision preserves the Feature 003 contract.

---

## Explicit Out-of-Scope Decisions

| Item | Decision |
|---|---|
| Holiday name / description access | Out of scope — `HolidayConf.Holidays` is `IReadOnlyList<DateOnly>`; names are a persistence concern (Feature 003) |
| Fetching or seeding holiday data | Out of scope — caller provides a populated `HolidayConf` |
| Caching computed deadlines | Out of scope — stateless pure function; caller caches if needed |
| Validation that `incomeDate` is "recent" | Out of scope — caller responsibility |
| Async variant of `CalculateDeadline` | Out of scope — no I/O; sync is correct |
| Desktop UI for displaying the deadline | Out of scope — Feature 011 (Filing Pipeline) |

---

## Encoded Assumptions

| ID | Assumption |
|----|-----------|
| A-001 | `FilingDeadlineCalculator` is a `public static class` in namespace `Rentier.Domain.Services`. |
| A-002 | Method signature: `public static DateOnly CalculateDeadline(DateOnly incomeDate, HolidayConf holidays)`. |
| A-003 | `holidays` parameter assumed non-null; `HolidayConf` constructor already enforces this. No null check inside calculator. |
| A-004 | `HolidayConf.Holidays` may be empty (zero entries). This is valid; the calculator returns a weekend-adjusted deadline with no holiday skips. |
| A-005 | Loop condition: `IsWeekend(candidate) \|\| holidays.Holidays.Contains(candidate)`. Both conditions re-evaluated on every iteration. |
| A-006 | Safety cap: 14 iterations. Overflow throws `DomainException`. |
| A-007 | `DateOnly` value equality is used for holiday lookup via `.Contains()`. No custom `IEqualityComparer` needed. |
| A-008 | "No double-skip" = the loop evaluates only the current candidate. A holiday entry in `HolidayConf` that coincides with a previously-skipped weekend date does NOT trigger an additional advance. |
| A-009 | Returned `DateOnly` is inclusive — it is the last valid PP-OPO filing day. |
| A-010 | Tests located at `tests/Rentier.Domain.Tests/Services/FilingDeadlineCalculatorTests.cs`. xUnit `Theory` + `[InlineData]`. 100 % branch coverage required (constitution §V: "Domain code MUST maintain 100% rule/state coverage"). |
| A-011 | No async, no I/O, no network calls. Pure synchronous function. |
| A-012 | `HolidayConf` value object is unchanged — no new properties or methods. |
| A-013 | Canonical class name: `FilingDeadlineCalculator`. Term "DeadlineCalculator" appearing in the ROADMAP spec-kit prompt is an informal shorthand; the canonical name is derived from the domain glossary entry "Filing Deadline". |
| A-014 | Each day in a multi-day holiday period is a separate `DateOnly` entry in `HolidayConf.Holidays` (established by Feature 003 A-018). |

---

## Functional Requirements Preview

| ID | Requirement |
|----|------------|
| FR-001 | `FilingDeadlineCalculator.CalculateDeadline(DateOnly incomeDate, HolidayConf holidays)` returns a `DateOnly` PP-OPO filing deadline. |
| FR-002 | The base candidate is `incomeDate.AddDays(30)`. |
| FR-003 | If the candidate is Saturday or Sunday, advance by one day and repeat the check. |
| FR-004 | If the candidate is a date in `holidays.Holidays`, advance by one day and repeat the check. |
| FR-005 | The loop terminates when the candidate is simultaneously a weekday and not in `holidays.Holidays`. |
| FR-006 | If the loop advances more than 14 times, `DomainException` is thrown. |
| FR-007 | A holiday entry whose `DateOnly` value coincides with a skipped weekend date has no additional effect on the candidate. |
| FR-008 | An empty `HolidayConf.Holidays` list is valid input; the method returns a weekend-adjusted deadline only. |
| FR-009 | The returned date is the last valid filing day (inclusive). |
| FR-010 | The implementation is covered by xUnit `Theory` `[InlineData]` tests at 100 % branch coverage. No I/O. No network. No async. |
