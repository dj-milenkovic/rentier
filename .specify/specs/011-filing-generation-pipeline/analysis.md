# Specification Analysis Report — 011 Filing Generation Pipeline

**Generated**: 2026-04-07 | **Scope**: Read-only cross-artifact consistency analysis  
**Artifacts examined**: `clarify.md`, `spec.md`, `plan.md`, `research.md`, `data-model.md`,
`tasks.md`, `contracts/ProcessReportsContract.md`, `quickstart.md`, `constitution.md`

---

## Findings Table

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| H1 | Inconsistency | **HIGH** | `tasks.md` Notes + T006 + T014 case 14 | Notes section says *"For interest, pass `i.Currency` as whtCurrency (whtAmount=0 so the equality check is skipped)"* — but T006 performs an actual WHT lookup (`wht?.Amount ?? 0m`) and T014 case 14 explicitly tests a non-zero interest WHT scenario. The parenthetical reasoning is false when a `WithholdingTaxRecord` is found. The implementation is **correct** (always passes `i.Currency` as whtCurrency, satisfying the constraint), but the note's justification will mislead the implementer into thinking whtAmount can never be non-zero for interest. | Reword the Notes entry to: *"For interest, always pass `i.Currency` as whtCurrency regardless of whether a WHT record is found — this guarantees whtCurrency == incomeCurrency and satisfies the TaxCalculationService constraint even when whtAmount > 0."* |
| H2 | Coverage Gap | **HIGH** | `research.md` Decision 8; `tasks.md` (no task) | `InterestType.Debit` records (margin interest, bank charges) are **not filtered** before the interest loop. Research.md explicitly flags this: *"Future work should add a pre-filter `where interest.Type == InterestType.Credit`."* No task exists for this guard. The pipeline will create Tax Filings for expense records (negative-income events), producing incorrect PP-OPO data. | Add task T016 (or a note in T006) to add `where i.Type == InterestType.Credit` pre-filter to the interest loop, or capture it as a spec-level known limitation with a TODO comment in the handler. |
| M1 | Inconsistency | **MEDIUM** | `clarify.md` #3 vs `research.md` Decision 5, `tasks.md` T006, `contracts/ProcessReportsContract.md` | `clarify.md` #3 names the holiday method `IHolidayRepository.GetAllAsync()`. All later artifacts consistently use `GetHolidayConfAsync(ct)` (research, tasks, contract). These are different method names returning different shapes. If an implementer reads only clarify.md and not later artifacts, they will call the wrong method. | Acknowledge clarify.md is superseded. Add a cross-reference in clarify.md #3 noting it was refined to `GetHolidayConfAsync` in research.md Decision 5. No code change needed — tasks.md is authoritative. |
| M2 | Inconsistency | **MEDIUM** | `research.md` Decision 6 vs `clarify.md` #13, `tasks.md` T006, `plan.md` pipeline flow | Research.md Decision 6 states: *"The handler passes `whtAmount = 0` for **all** interest records."* This is contradicted by: (a) `clarify.md` #13 which says match by (date, entity), use 0 *if none matched*; (b) `tasks.md` T006 which does a live `parsed.Withholdings.FirstOrDefault(...)` lookup; (c) T014 case 14 which tests non-zero interest WHT. Research.md is overly absolute and is an outlier. | Research.md Decision 6 is stale and should be updated to match clarify.md #13: *"WHT lookup attempted by (Date, EntityName); whtAmount = wht?.Amount ?? 0m."* |
| M3 | Ambiguity | **MEDIUM** | `tasks.md` T006 interest loop; `data-model.md` WHT matching section | The interest WHT lookup uses a **2-field key** `(Date, EntityName)` with no currency match. If a `WithholdingTaxRecord` is found with `Currency != i.Currency` and the amount is non-zero, `TaxCalculationService.CalculateAsync` is called with `whtCurrency = i.Currency` (not the WHT record's actual currency). This satisfies the `whtCurrency == incomeCurrency` constraint, but the WHT amount is implicitly treated as if it is in the income currency — which may be semantically wrong. This edge case is neither documented in the spec nor in data-model.md. | Document this behavior explicitly in `data-model.md` under the "WHT matching key" entry: *"For interest records, WHT is matched by (Date, EntityName) only. The WHT amount is interpreted as being in the income record's currency regardless of `WithholdingTaxRecord.Currency`."* |
| L1 | Inaccuracy | **LOW** | `clarify.md` #12 | Clarify decision #12 states `GetByReportIdAsync` is *"used in dedup check too."* The dedup check uses `ExistsByIncomeAsync` (4-field key). `GetByReportIdAsync` is not called anywhere in the handler pipeline. The comment is incorrect. | Remove *"(used in dedup check too)"* from clarify.md #12. `GetByReportIdAsync` is added for completeness/future query use only. |
| L2 | Underspecification | **LOW** | `tasks.md` T013 test 9; `plan.md` test table | Both `T013` test 9 (`UpdateAsync_StatusChange_PersistsUpdate`) and the plan.md test table call `filing.AdvanceStatus(FilingStatus.Filed)`. The signature of the **existing** `AdvanceStatus` method is not defined in this feature's spec or data-model.md (it pre-dates feature 011). If the existing implementation is parameterless (advances to next state in sequence), passing `FilingStatus.Filed` would fail to compile. | Look up the existing `AdvanceStatus` signature in `src/Rentier.Domain/Entities/Filing.cs` before writing the test. Document the method signature in `data-model.md` Status Transitions section. |
| L3 | Ambiguity | **LOW** | `data-model.md` WHT matching section; `tasks.md` T006 | WHT matching uses a **3-field key** `(Date, EntityName, Currency)` for dividends but a **2-field key** `(Date, EntityName)` for interest. The asymmetry is intentional (per clarify.md #13) but is only documented in clarify and tasks — not in `data-model.md` or `spec.md`. A reader of the data model alone would not understand why the matching strategy differs. | Add a note to `data-model.md` under "WHT matching key": *"Dividend WHT: matched by (Date, EntityName, Currency). Interest WHT: matched by (Date, EntityName) only — currency omitted because interest WHT records in IBKR statements are not currency-specific."* |

---

## Coverage Summary Table

| Requirement Key | Has Task? | Task IDs | Notes |
|-----------------|-----------|----------|-------|
| Filing entity: 8 new fields | ✅ | T001 | All properties explicit with private setters |
| Filing.CreateFromIncome factory + invariants | ✅ | T002 | All 4 guard conditions documented |
| ProcessReportsCommand record | ✅ | T004 | |
| ProcessReportsResult DTO | ✅ | T003 | |
| IFilingRepository: ExistsByIncomeAsync | ✅ | T005, T009 | Interface + implementation |
| IFilingRepository: GetByReportIdAsync | ✅ | T005, T009 | Interface + implementation |
| ProcessReportsCommandHandler (full pipeline) | ✅ | T006 | All 6 injected deps, all loops, all error paths |
| FilingConfiguration EF mapping | ✅ | T007 | Precision(18,2), FK cascade/setnull, indexes |
| AppDbContext.Filings DbSet | ✅ | T008 | |
| EF Migration 0008 | ✅ | T010 | With verification step |
| DI registration (FilingRepository + handler) | ✅ | T011 | AddTransient only |
| Domain tests (11 cases) | ✅ | T012 | Exact case count matches plan.md |
| Infrastructure repository tests (10 cases) | ✅ | T013 | Exact case count matches plan.md |
| Application handler tests (14 cases) | ✅ | T014 | Exact case count matches plan.md |
| End-to-end polish + CI gate | ✅ | T015 | Includes DiRegistrationSmokeTests check |
| InterestType.Debit pre-filter | ❌ | — | **GAP** — research.md Decision 8 flags this but no task created |

---

## Constitution Alignment Issues

**None.** All five constitution principles are satisfied:

| Principle | Status | Evidence |
|-----------|--------|---------|
| I. Clean Architecture | ✅ PASS | Handler in Application; Filing in Domain; FilingRepository in Infrastructure; DI in Desktop. No boundary violations. |
| II. Local-First Security | ✅ PASS | No new external network calls. NBS HTTP already approved; IBKR rates from embedded CSV. |
| III. Financial & Temporal Correctness | ✅ PASS | All monetary fields `decimal` with `HasPrecision(18,2)`. All dates `DateOnly`. `MidpointRounding.AwayFromZero` documented. No `double`/`float`. |
| IV. Async & UI Responsiveness | ✅ PASS | All I/O async; CancellationToken propagated throughout; no `.Result`/`.Wait()`; `OperationCanceledException` never caught. |
| V. Specification-Driven Quality Gates | ✅ PASS | 35 tests specified across 3 test files; Domain 100% invariant coverage; Application 14-path coverage (≥90%); traced to approved spec tasks. |

---

## Unmapped Tasks

None. All 15 tasks (T001–T015) map directly to functional requirements, test coverage, or
infrastructure gates documented in spec.md and plan.md.

---

## Critical Constraint Verification

**Constraint**: `TaxCalculationService.CalculateAsync` throws when `whtCurrency != incomeCurrency`
(when `whtAmount > 0`). This must hold for both dividend and interest paths.

### Dividends (T006 dividend loop)
- Income currency: `d.Currency`
- WHT lookup: matched by `(Date, EntityName, Currency == d.Currency)` — 3-field, currency-constrained
- Call: `CalculateAsync(..., d.Currency, wht?.Amount ?? 0m, d.Currency, ...)` — `whtCurrency == incomeCurrency` always ✅

### Interest (T006 interest loop)
- Income currency: `i.Currency`
- WHT lookup: matched by `(Date, EntityName)` — 2-field, **no currency constraint**
- Call: `CalculateAsync(..., i.Currency, wht?.Amount ?? 0m, i.Currency, ...)` — `whtCurrency == incomeCurrency` always ✅

**Constraint is correctly modeled.** Both paths always pass `incomeCurrency` as `whtCurrency`,
so the equality constraint is satisfied regardless of what `whtAmount` resolves to.

**However** (see Finding H1): The tasks.md Notes section incorrectly explains the interest path
as *"whtAmount=0 so equality check is skipped"* — the constraint is satisfied not because
`whtAmount` is always 0, but because `whtCurrency` is unconditionally set to `i.Currency`.
The implementation is safe; the explanation is wrong and could mislead the implementer.

---

## Feature 010 Dependency Verification

Feature 011 declares a hard dependency on feature 010 in `tasks.md` Prerequisite section.

| Dependency | Required | Documented in tasks.md | Status |
|-----------|---------|------------------------|--------|
| `Report.Status` (ReportStatus enum) | ✅ | ✅ | Verified |
| `Report.AttachmentContent` (byte[]?) | ✅ | ✅ | Verified |
| `Report.SetStatus(ReportStatus)` method | ✅ | ✅ | Verified |
| `IReportRepository.GetByStatusAsync(ReportStatus, CT)` | ✅ | ✅ | Verified |
| `IReportRepository.UpdateAsync(Report, CT)` | ✅ | ✅ (clarify.md #11) | Verified |
| `AppDbContext.Reports` DbSet | ✅ | ✅ | Verified |
| Migration 0007 (Reports table) | ✅ | ✅ | Verified |

Feature 010 cross-feature dependency is **correctly and completely modeled** in tasks.md.

---

## Metrics

| Metric | Value |
|--------|-------|
| Total functional requirements tracked | 16 |
| Total tasks | 15 (T001–T015) |
| Requirement coverage (≥1 task) | 15/16 = **93.75%** |
| Uncovered requirement | InterestType.Debit pre-filter |
| Total test cases specified | 35 (11 domain + 14 application + 10 infrastructure) |
| Ambiguity findings | 2 (M3, L3) |
| Inconsistency findings | 3 (H1, M1, M2) |
| Coverage gap findings | 1 (H2) |
| Inaccuracy / underspecification findings | 2 (L1, L2) |
| Constitution CRITICAL violations | 0 |
| CRITICAL severity total | 0 |
| HIGH severity total | 2 |
| MEDIUM severity total | 3 |
| LOW severity total | 2 |

---

## Next Actions

### No blockers for `/speckit.implement`

There are **zero CRITICAL issues** and **zero constitution violations**. Implementation may proceed
immediately. The two HIGH findings should be addressed before or during implementation:

1. **[H1 — Before T006]** Fix the misleading Note about interest `whtAmount` being always 0.
   The implementation logic in T006 is correct; only the explanatory note in the Notes section
   needs rewording. Update before the implementer reads it.

2. **[H2 — During T006]** Add `InterestType.Debit` pre-filter to the interest loop in T006,
   or add a `// TODO: filter Debit records (feature 011 research.md Decision 8)` comment
   so the known gap is visible in code review.

3. **[L2 — Before T013]** Verify `AdvanceStatus` method signature in the existing `Filing.cs`
   before writing test 9 in T013. Do not assume a parameterized overload exists.

### Suggested remediation commands (if desired)
- **tasks.md Notes section** → reword H1 entry (manual edit, ~2 lines)
- **tasks.md T006** → add Debit filter or TODO comment (H2)
- **research.md Decision 6** → update "all interest records → whtAmount=0" to "whtAmount=wht?.Amount??0" (M2)
- **clarify.md #12** → remove "(used in dedup check too)" (L1)
- **data-model.md WHT matching** → document 2-vs-3-field asymmetry (L3)
- **data-model.md Status Transitions** → document `AdvanceStatus` signature (L2)

---

*This report is read-only. No files were modified. Re-running this analysis without changes
should produce the same findings and IDs.*
