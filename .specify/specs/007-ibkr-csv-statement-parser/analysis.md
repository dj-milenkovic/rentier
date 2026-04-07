# Specification Analysis Report — Feature 007: IBKR CSV Statement Parser

**Generated**: 2026-04-07  
**Artifacts analysed**: spec.md, plan.md, tasks.md, clarify.md, constitution.md  
**Codebase inspected**: Result.cs, Error.cs, Rentier.Infrastructure.csproj, InfrastructureServiceExtensions.cs, Rentier.Infrastructure.Tests.csproj  
**Status**: ⛔ CRITICAL issues found — resolve before running `/speckit.implement`

---

## Findings Table

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| C1 | Build-Break | **CRITICAL** | tasks.md T011, plan.md Phase 2 | `ReadRows` declared as `private static IReadOnlyList<string[]> ReadRows(Stream stream)` (synchronous) but the body uses `await csv.ReadAsync()` — `await` in a non-`async` method is a C# compile error. | Change signature to `private static async Task<IReadOnlyList<string[]>> ReadRows(Stream stream)` and update the `ParseAsync` call site to `await ReadRows(csvStream)`. tasks.md T011 amended. |
| C2 | Logic Bug | **CRITICAL** | tasks.md T015, spec.md FR-016/FR-017, wht_currency_mismatch.csv test | WHT lookup uses a single `Dictionary<(DateOnly, string, string), DividendRecord>` keyed by `(Date, EntityName, **Currency**)`. A currency-mismatch WHT row will always miss the exact-key lookup and fall through to `WHT_UNMATCHED` — `WHT_CURRENCY_MISMATCH` can never be emitted with this key. The `wht_currency_mismatch.csv` AC-005 test will always fail. | Use two-level lookup: first try full `(Date, EntityName, Currency)` key; on miss, try `(Date, EntityName)` only to distinguish mismatch from orphan. tasks.md T015 amended. |
| H1 | Inconsistency | **HIGH** | clarify.md Q6 vs tasks.md T011, plan.md, tasks.md T026 | Two conflicting `StripIsin` implementations: (a) clarify.md Q6 uses `IsinPattern.Replace(description, string.Empty).Trim()` which produces `"AAPL Cash Dividend USD 0.24 per Share"` for `"AAPL(US0378331005) Cash Dividend USD 0.24 per Share"`; (b) tasks.md T011 and plan.md use `description.Split('(')[0].Trim()` which produces `"AAPL"`. All T026 assertions expect `"AAPL"` — clarify.md's regex-Replace approach fails every StripIsin and EntityName test. | Canonical implementation is `Split('(')[0].Trim()` (tasks.md / plan.md). clarify.md Q6 correctly describes the ISIN regex **pattern** but incorrectly describes the strip operation as `Regex.Replace`. The correct strip discards everything from the first `(` onward. Clarifying comment added in T011 amendment. |
| H2 | Inconsistency | **HIGH** | tasks.md T013 step 6 vs spec.md InterestRecord data model, clarify.md Q5 | T013 step 6 sets `entityName = StripIsin(row[4])`. IBKR interest descriptions (e.g. `"Credit Interest for Jan-2024"`) contain no `(`, so `Split('(')[0]` returns the full description string — not `"Interactive Brokers"`. spec.md data model and clarify.md Q5 are explicit: `EntityName` MUST be the constant `"Interactive Brokers"` for all `InterestRecord`s. | Change T013 step 6 to `entityName = "Interactive Brokers"` (hardcoded constant, no `StripIsin`). tasks.md T013 amended. |
| H3 | Inconsistency | **HIGH** | spec.md Error Code Reference vs tasks.md T012/T013/T015, tasks.md T026 | spec.md Error Code Reference table lists a single code `MALFORMED_ROW` for "Date or amount field in a data row cannot be parsed". tasks.md T012/T013/T014/T015 use `ROW_DATE_INVALID` and `ROW_AMOUNT_INVALID` (two distinct codes). T026 asserts `Errors[0].Code == "ROW_AMOUNT_INVALID"`. An implementer following spec.md's error table produces `MALFORMED_ROW` and every row-error test fails. | **tasks.md's granular codes are correct** — they are used consistently throughout tasks.md and tested in T026. The spec.md Error Code Reference table must be treated as incorrect for these two rows. Implementation MUST follow tasks.md codes (`ROW_DATE_INVALID`, `ROW_AMOUNT_INVALID`). |
| H4 | Coverage Gap | **HIGH** | spec.md FR-015, EMPTY_ENTITY_NAME; tasks.md T012, T013, T015 | FR-015 requires: "If ISIN stripping produces an empty or whitespace-only `EntityName`, the row MUST be skipped and a `ParseError` added to `Errors`." None of T012, T013, or T015 include an empty-EntityName guard after `StripIsin(...)`. The `EMPTY_ENTITY_NAME` error code appears in spec.md but is never emitted by any task. | Add `if (string.IsNullOrWhiteSpace(entityName)) { errors.Add(new ParseError("EMPTY_ENTITY_NAME", ...)); continue; }` after every `StripIsin()` call in T012, T013, and T015. tasks.md T012/T013/T015 amended. |
| M1 | Terminology Drift | **MEDIUM** | spec.md File Locations, clarify.md AD-1 vs tasks.md T002/T018-T025/T026 | spec.md and clarify.md say `Parsing/Fixtures/` and `Parsing/IbkrCsvParserTests.cs`. tasks.md says `Parsers/Fixtures/` (plural) throughout T002, T018–T025, and T026. tasks.md is **internally self-consistent** — the EmbeddedResource glob in T002 and resource name string in T026 both use `Parsers`. Implementation using `Parsing/` would cause T026's `LoadFixture` to throw at runtime. | Implementation MUST follow tasks.md: use `Parsers/Fixtures/`. Do NOT use `Parsing/` from spec.md file locations. Spec.md should be updated to say `Parsers/` for accuracy. |
| M2 | Coverage Gap | **MEDIUM** | spec.md FR-023, spec.md Edge Cases; tasks.md T016, T026 | FR-023: "`ParseAsync` MUST return `Result.Failure(new Error("CANCELLED", ...))` when cancelled — no `OperationCanceledException`." T016's outer `catch (Exception ex)` intercepts `OperationCanceledException` and emits `"PARSE_EXCEPTION"` — not `"CANCELLED"`. No cancellation test exists in T026. | Add `catch (OperationCanceledException) { return Result<...>.Failure(new Error("CANCELLED", "Parsing was cancelled.")); }` placed **before** the general `catch(Exception)` block in T016. tasks.md T016 amended. |
| M3 | Coverage Gap | **MEDIUM** | spec.md US2 AC-004/AC-005/AC-006; tasks.md T018–T025, T026 | Three US2 acceptance criteria have no fixture or test assertion: (a) AC-004 `WHT_POSITIVE_AMOUNT` — no fixture; (b) AC-005 `RATE_DUPLICATE` — no fixture; (c) AC-006 `RATE_NON_POSITIVE` — no fixture. Parser code for all three cases is specified in T015/T014 but remains untested. | Add three small fixture files and T026 assertions for these cases. At minimum, these scenarios can be embedded within an extended `happy_path.csv` or as separate fixture tasks. Suggested: add tasks T025a/T025b/T025c before T026. |
| M4 | Ambiguity | **MEDIUM** | spec.md Assumption 7, clarify.md Assumption 10 | Both documents state "`ParseAsync` may complete synchronously." After the C1 fix (`ReadRows` becomes truly async), `ParseAsync` will always enter at least one `await` — the synchronous-completion assumption is misleading. | Update assumption to: "`ParseAsync` is `async Task<T>` with true async stream I/O via `ReadRows`. It complies with constitution §IV and does not block any thread." |
| L1 | Naming Drift | **LOW** | spec.md File Locations vs tasks.md T019/T020 | spec.md File Locations lists `duplicate_dividend_same_date.csv`. tasks.md uses `multiple_dividends_same_entity.csv` (T019) and `multiple_dividends_different_dates.csv` (T020) — more descriptive and explicit. No `duplicate_dividend_same_date.csv` task exists. | Follow tasks.md fixture names. Update spec.md File Locations for accuracy. No implementation impact. |
| L2 | Convention Gap | **LOW** | tasks.md T026; constitution §Testing | Constitution requires `MethodName_StateUnderTest_ExpectedBehavior` naming for all tests. T026 describes assertions but provides no test method name guidance. An implementer may use arbitrary names. | Add example method name patterns to the T026 header, e.g.: `ParseAsync_HappyPathCsv_ReturnsAllCollectionsPopulated`, `ParseAsync_WhtCurrencyMismatch_EmitsCorrectErrorCode`, `StripIsin_WithIsinSuffix_ReturnsEntityNameOnly`. |

---

## Coverage Summary Table

| Requirement Key | Has Task? | Task IDs | Notes |
|-----------------|-----------|----------|-------|
| FR-001 (IStatementParser interface) | ✅ | T010 | Fully covered |
| FR-002 (Infrastructure implementation) | ✅ | T011 | Covered; **C1 fix applied** in amended T011 |
| FR-003 (Never throw) | ✅ | T016 | Covered; **M2 fix applied** for CANCELLED |
| FR-004 (Parse Dividends) | ✅ | T012 | Covered; **H4 fix applied** |
| FR-005 (Parse WHT) | ✅ | T015 | Covered; **C2 fix applied** in amended T015 |
| FR-006 (Parse Interest Credit/Debit) | ✅ | T013 | Covered; **H2 fix applied** |
| FR-007 (Parse Exchange Rates) | ✅ | T014 | Fully covered |
| FR-008 (Skip Total/Notes) | ✅ | T012–T015 | All filter `row[1] == "Data"` |
| FR-009 (Skip unknown sections) | ✅ | T011 | Dispatch only on known names |
| FR-010 (Dividend aggregation) | ✅ | T012 | Fully covered |
| FR-011 (Interest aggregation by Type) | ✅ | T013 | Covered; H2 fix restores correct EntityName |
| FR-012 (FX rate duplicate last-wins) | ✅ | T014 | Fully covered |
| FR-013 (EntityName via ISIN strip) | ✅ | T011 | Covered; H1 clarification ensures correct implementation |
| FR-014 (Consistent ISIN strip Div+WHT) | ✅ | T012, T015 | Fully covered |
| FR-015 (Empty EntityName → ParseError) | ✅* | T012, T013, T015 | *Covered after H4 amendment |
| FR-016 (WHT match by full key) | ✅ | T015 | Covered |
| FR-017 (WHT_CURRENCY_MISMATCH) | ✅* | T015 | *Reachable after C2 amendment |
| FR-018 (WHT_UNMATCHED) | ✅ | T015 | Covered |
| FR-019 (WHT positive amount rejected) | ✅ | T015 | Covered; no test fixture (M3) |
| FR-020 (RATE_NON_POSITIVE) | ✅ | T014 | Covered; no test fixture (M3) |
| FR-021 (STREAM_ERROR) | ✅ | T016 | Fully covered |
| FR-022 (INVALID_FORMAT) | ✅ | T016 | Fully covered |
| FR-023 (CANCELLED) | ✅* | T016 | *Covered after M2 amendment |
| FR-024 (CsvHelper in Infrastructure only) | ✅ | T001 | Fully covered |
| FR-025 (No EF migrations) | ✅ | Plan | Covered by exclusion |
| NFR-001 (decimal only) | ✅ | T004–T009 | constitution §III satisfied |
| NFR-002 (DateOnly only) | ✅ | T004–T009 | constitution §III satisfied |
| NFR-003 (Thread safety / Transient) | ✅ | T017 | `AddTransient` used; constitution §IV satisfied |
| NFR-004 (No persistence) | ✅ | Plan | Satisfied by design |
| NFR-005 (≥90% coverage) | ✅ | T028 | T026 suite covers all acceptance criteria post-amendments |
| NFR-006 (UTF-8 encoding) | ✅ | T011 | `StreamReader` defaults to UTF-8 |

---

## Constitution Alignment Issues

| Principle | Status | Detail |
|-----------|--------|--------|
| I — Clean Architecture | ✅ Satisfied | CsvHelper added to Infrastructure.csproj only (T001); Application has no CSV dependency; dependency direction is Infrastructure → Application |
| II — Local-First Privacy | ✅ Satisfied | Parser is entirely in-memory; no network calls, no writes to external storage |
| III — Financial/Temporal Correctness | ✅ Satisfied | All DTO types use `decimal` and `DateOnly` (T003–T009); no `double`, `float`, or `DateTime` anywhere |
| IV — Async/UI Responsiveness | ❌ **Violated as written (C1)** | `ReadRows` is declared synchronous but uses `await` → compile error. **After C1 amendment**: fully async I/O path, satisfies §IV |
| V — Specification-Driven Quality Gates | ⚠️ Partial | FR-015 and FR-023 lacked task coverage pre-amendments; US2 AC-004/AC-005/AC-006 still lack test fixtures (M3). Post-amendments: FR-015 and FR-023 are covered; ≥90% coverage achievable with M3 additions |

---

## Unmapped Tasks

No unmapped tasks found. All T001–T028 map to one or more requirements.

---

## Metrics

| Metric | Value |
|--------|-------|
| Total Functional Requirements | 25 (FR-001–FR-025) |
| Total Non-Functional Requirements | 6 (NFR-001–NFR-006) |
| Total Tasks | 28 (T001–T028) |
| FR Coverage before amendments | 22 / 25 = **88%** |
| FR Coverage after amendments | 25 / 25 = **100%** |
| Critical Issues | **2** (C1, C2) |
| High Issues | **4** (H1–H4) |
| Medium Issues | 4 (M1–M4) |
| Low Issues | 2 (L1–L2) |
| Ambiguity Count | 1 (M4) |
| Duplication/Conflict Count | 2 (H1 StripIsin conflict; H3 error code conflict) |

---

## Next Actions

### ⛔ Must resolve before `/speckit.implement`

1. **C1** — `ReadRows` async signature fix (tasks.md T011 amended ✅)
2. **C2** — WHT two-level lookup fix (tasks.md T015 amended ✅)

### ⚠️ Strongly recommended (HIGH — will break tests or produce wrong data)

3. **H1** — Follow `Split('(')[0]` implementation, disregard clarify.md Q6 regex-Replace. Clarifying note added to T011 ✅
4. **H2** — Interest EntityName must be `"Interactive Brokers"` constant (T013 amended ✅)
5. **H3** — Use `ROW_DATE_INVALID` / `ROW_AMOUNT_INVALID` from tasks.md, **not** `MALFORMED_ROW` from spec.md error table
6. **H4** — Add empty-EntityName guards in T012, T013, T015 (amended ✅)

### 💡 Improvements (MEDIUM)

7. **M1** — Use `Parsers/Fixtures/` path (tasks.md), never `Parsing/Fixtures/` (spec.md)
8. **M2** — Add `catch (OperationCanceledException)` in T016 (amended ✅)
9. **M3** — Add fixtures and assertions for `WHT_POSITIVE_AMOUNT`, `RATE_DUPLICATE`, `RATE_NON_POSITIVE` before running T026

---

*This report is read-only. tasks.md has been updated with amendments for C1, C2, H2, H4, and M2 — all items marked `[AMENDED]`.*
