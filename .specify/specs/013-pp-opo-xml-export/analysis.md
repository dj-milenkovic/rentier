# Specification Analysis Report — 013-pp-opo-xml-export

**Generated**: 2026-04-07  
**Analyzer**: speckit.analyze  
**Artifacts reviewed**: spec.md, clarify.md, plan.md, data-model.md, contracts/application-contracts.md, tasks.md  
**Source files reviewed**: TaxpayerProfile.cs, Filing.cs, Importer.cs, ICommandHandler.cs, IFilingRepository.cs, IReportRepository.cs, IImporterRepository.cs, ITaxpayerProfileRepository.cs, FilingsViewModel.cs, FilingsView.axaml, FilingsView.axaml.cs, CompositionRoot.cs, InfrastructureServiceExtensions.cs, IncomeType.cs, Result.cs, Error.cs  
**Constitution**: `.specify/memory/constitution.md` v1.0.0

---

## Findings Table

| ID | Category | Severity | Location | Summary | Recommendation |
|----|----------|----------|----------|---------|----------------|
| H1 | Inconsistency | **HIGH** | spec.md:L118 | `ExportFilingCommand` Key Entities entry still documents the return type as `Result<byte[], Error>`. All downstream artifacts (plan.md §Revised return type, contracts §1.2, tasks.md T003/T005/T009/T011) consistently use `Result<ExportFilingResult, Error>`. A developer reading only spec.md would implement the wrong return type, causing a compile error. | Update spec.md Key Entities: replace `"returns Result<byte[], Error> containing the serialized XML bytes on success"` with `"returns Result<ExportFilingResult, Error> where ExportFilingResult contains Bytes (byte[]) and SuggestedFileName (string)"` |
| H2 | Inconsistency | **HIGH** | spec.md:L77, clarify.md:L33 | Clarifications § "Session 2026-04-07" answers Decision 2 with `Result<byte[], Error>`. clarify.md Decision 2 likewise records this as the final answer. Both documents are never amended after plan.md §Revised return type elevated the return type to `Result<ExportFilingResult, Error>`. | Add a correction note in both spec.md Clarifications and clarify.md Decision 2: *"Revised in plan.md §Revised return type to `Result<ExportFilingResult, Error>`. This supersedes the original clarification answer."* |
| H3 | Inconsistency | **HIGH** | spec.md:L117 | Key Entities `TaxpayerProfile` entry lists `JMBG` and `Phone` in backtick-formatted code (implying C# property names). The actual entity properties are `Jmbg` (capitalisation) and `PhoneNumber` (no such property `Phone` exists). A developer relying on spec.md alone would write `profile.JMBG` or `profile.Phone`, both of which would fail to compile. plan.md R-002 and data-model.md both document the correction, but spec.md was never patched. | Update spec.md Key Entities `TaxpayerProfile` fields: replace `` `JMBG` `` → `` `Jmbg` `` and `` `Phone` `` → `` `PhoneNumber` (nullable `string?`) `` |
| M1 | Inconsistency | **MEDIUM** | data-model.md:L199–201 | The annotated XML sample shows `<OsnovicaZaPorez>12345.50</OsnovicaZaPorez>` and `<ObracunatiPorez>1234.55</ObracunatiPorez>` — two different values. The mapping table (data-model.md §Section 3) and spec.md assumption explicitly state that **both** `OsnovicaZaPorez` and `ObracunatiPorez` map to the same field `filing.GrossTaxPayableRsd`. The sample contradicts the mapping, risking an implementor producing divergent values for these two elements. | Fix the annotated XML sample in data-model.md: set `ObracunatiPorez` to the same value as `OsnovicaZaPorez` (e.g., both `12345.50`) and optionally add a comment `<!-- Both map to GrossTaxPayableRsd -->` |
| M2 | Inconsistency | **MEDIUM** | plan.md:L288–311 | Phase 1 Design §`FilingsViewModel` export integration shows a **pre-revision** code block using `ICommandHandler<ExportFilingCommand, Result<byte[], Error>>` as the constructor parameter type and `Func<Guid, Task>` as the delegate type. The "Revised return type" note appears later at plan.md §Revised return type and corrects this to `Func<ExportFilingResult, Task>`. The stale block is never struck through or annotated, leaving two conflicting designs in the same document. tasks.md and contracts correctly use the revised types. | Mark the pre-revision code block in plan.md Phase 1 Design with `<!-- SUPERSEDED — see §Revised return type below -->` to make the read order unambiguous |
| M3 | Coverage Gap | **MEDIUM** | FR-008 / tasks.md:T011 | FR-008 states: *"no partial file must be left on disk if an error occurs during write"*. T011 delegates file writing to `await stream.WriteAsync(exportResult.Bytes)` without a try/finally or rollback. If `WriteAsync` throws partway through a large byte array (e.g. I/O error), a partial file may be left at the chosen path. While `byte[]` writes for small XML files are unlikely to be interrupted, FR-008 is a hard requirement and T011 does not address it. | Add a note to T011: if `stream.WriteAsync` throws, delete the partially-written `IStorageFile` (or wrap the write in a try/catch that calls `file.DeleteAsync()` on failure). Alternatively, write to a temp file and `File.Move` atomically on success — document the chosen approach. |
| M4 | Coverage Gap | **MEDIUM** | SC-001 / tasks.md:T014 | SC-001 requires the end-to-end export to complete *in under 3 seconds from click to file saved*. T014 smoke-test description mentions verifying the dialog appears and the file is valid XML, but does not include an explicit timing check. | Extend T014 manual smoke-test: add "Time the end-to-end flow (click Export → file saved) on a local SQLite with a typical filing; confirm < 3 s." |
| M5 | Inconsistency | **MEDIUM** | tasks.md:T010 vs plan.md:L332–357 + contracts:§3.3 | T010 directly specifies the code-behind fallback pattern (`Tag="{Binding Id}" Click="ExportButton_Click"`) without first attempting the preferred approach. plan.md §FilingsView and contracts §3.3 both label the AXAML `Command="{Binding DataContext.ExportCommand, RelativeSource=…}"` binding as *preferred* and the code-behind as a *fallback if DataContext binding proves unreliable*. T010 picks the fallback without justification, potentially adding unnecessary code-behind to a view that already has a constitutionally questionable amount of event handling. | Update T010 to first attempt the AXAML `Command` binding approach; only fall back to the code-behind pattern if confirmed unreliable for this Avalonia version. Add a note if the fallback is chosen: "AXAML DataContext binding verified unreliable for DataGridTemplateColumn in Avalonia 11.x.y — using code-behind pattern." |
| L1 | Inconsistency | **LOW** | clarify.md:Decision 2 | clarify.md records `Result<byte[], Error>` as the final answer to "What is the formal return type?" without any amendment note. Lower-priority than H2 (same root cause) since clarify.md is a historical session log, but it may still mislead a reader doing a quick search. | Append to clarify.md Decision 2: *"⚠ Superseded: plan.md §Revised return type elevated to `Result<ExportFilingResult, Error>`. The original `Result<byte[], Error>` is no longer the contract."* |
| L2 | Constitution | **LOW** | tasks.md:T012 | Constitution §Testing Requirements: *"Test naming MUST follow `MethodName_StateUnderTest_ExpectedBehavior`."* All 16 serializer test method names in T012 omit the `MethodName_` prefix (e.g., `Dividend_SifraVrstePrihoda_Is_111402000` instead of `Serialize_Dividend_SifraVrstePrihodaIs111402000`). This also causes a CI warning-free gate risk if a Roslyn analyser enforces the naming rule. | Prefix all T012 test names with `Serialize_` (e.g., `Serialize_Dividend_SifraVrstePrihodaIs111402000`, `Serialize_PhoneNumberNull_TelefonElementIsEmpty`) to comply with the constitution naming convention. |
| L3 | Inconsistency | **LOW** | tasks.md:T009 vs FilingsViewModel.cs | T009 adds a `ThrownExceptions` subscription for `ExportCommand` but the existing `DeleteCommand`, `AdvanceStatusCommand`, and `SavePaymentRefCommand` have no such subscriptions in the `WhenActivated` block. This creates an asymmetric defensive pattern: unhandled exceptions from Export are routed to `ErrorMessage`; unhandled exceptions from the other commands reach `RxApp.DefaultExceptionHandler`. | Note the asymmetry. Decide one of: (a) extend T009 to add `ThrownExceptions` for existing commands too; or (b) document that existing commands handle errors via `result.IsSuccess` checks internally and the pattern is intentionally asymmetric. |

---

## Coverage Summary Table

| Requirement Key | Has Task? | Task IDs | Notes |
|-----------------|-----------|----------|-------|
| FR-001 Export button in DataGrid | ✅ | T008, T010 | — |
| FR-002 StorageProvider.SaveFilePickerAsync | ✅ | T011 | — |
| FR-003 Repository loading chain | ✅ | T005 | All four repos; null-ReportId path in T005 step 3 |
| FR-004 Reject when no profile | ✅ | T005, T013 | Handler step 2; test in T013 |
| FR-005 Valid PP-OPO XML structure | ✅ | T006, T012 | All sections; 16 serializer tests |
| FR-006 SifraVrstePrihoda mapping | ✅ | T006, T012 | Interest→111401000, Dividend→111402000 |
| FR-007 Decimal formatting (F2 InvariantCulture) | ✅ | T006, T012 | Zero and non-zero cases covered |
| FR-008 No partial file on write error | ⚠️ Partial | T011 | No explicit rollback/cleanup — see M3 |
| FR-009 Return to normal UI state | ✅ | T009, T011 | Error banner + silent cancel |
| FR-010 IXmlFilingSerializer in Application; impl in Infrastructure | ✅ | T004, T006, T007 | Layer placement correct |
| FR-011 Fully async | ✅ | T005, T009, T011 | ReactiveCommand.CreateFromTask; await I/O |
| SC-001 < 3 s end-to-end | ⚠️ Partial | T014 | Manual smoke-test present; no timing checkpoint — see M4 |
| SC-002 100% schema-valid XML | ✅ | T012 | Unit tests assert all required elements |
| SC-003 Decimal precision preserved | ✅ | T012 | `MonetaryValues_FormattedAs_TwoDecimalPlaces_InvariantCulture` + zero test |
| SC-004 Dividend + Interest each tested | ✅ | T012, T013 | Both income types covered |
| SC-005 No crash / corrupt file on failure | ✅ | T013 | 7 handler tests cover all failure paths |

---

## Constitution Alignment Issues

| Principle | Status | Detail |
|-----------|--------|--------|
| I. Clean Architecture | ✅ Pass | `IXmlFilingSerializer` in Application; `PpOpoXmlSerializer` in Infrastructure; `ExportFilingCommandHandler` in Application/Handlers; Desktop injects only `ICommandHandler<>` interface. No repo or infra types leak into Desktop. |
| II. Local-First Security/Privacy | ✅ Pass | Zero network calls. File written only to user-selected local path via save dialog. |
| III. Financial and Temporal Correctness | ✅ Pass | All monetary fields use `decimal`; formatted with `"F2"` + `CultureInfo.InvariantCulture` exclusively in `PpOpoXmlSerializer`. `DateOnly` used for `IncomeDate` and `FilingDeadline`. No `float`/`double`/`DateTime` introduced. |
| IV. Async and UI Responsiveness | ✅ Pass | `ExportCommand = ReactiveCommand.CreateFromTask<Guid>(...)`. Handler is async. File write uses `await stream.WriteAsync`. UI thread never blocked. |
| V. Specification-Driven Quality Gates | ⚠️ Partial | Test naming in T012 violates `MethodName_StateUnderTest_ExpectedBehavior` convention (see L2). All other gates met: ≥ 90% handler test coverage (7 scenarios), serializer tests (16 scenarios), CI warning check in T014. |

**No CRITICAL constitution violations detected.**

---

## Unmapped Tasks

All 14 tasks (T001–T014) map to at least one functional requirement, user story, or cross-cutting concern. No orphaned tasks.

---

## Metrics

| Metric | Value |
|--------|-------|
| Total Functional Requirements | 11 (FR-001 → FR-011) |
| Total Success Criteria | 5 (SC-001 → SC-005) |
| Total Tasks | 14 (T001–T014) |
| Requirements with ≥ 1 task | 11 / 11 = **100%** (2 with partial gaps: FR-008, SC-001) |
| Ambiguity findings | 0 |
| Duplication findings | 0 |
| Inconsistency findings | 8 (H1, H2, H3, M1, M2, M5, L1, L3) |
| Coverage gap findings | 2 (M3, M4) |
| Constitution compliance | 1 partial (L2 — test naming) |
| **HIGH issues** | **3** (H1, H2, H3) |
| **MEDIUM issues** | **5** (M1, M2, M3, M4, M5) |
| **LOW issues** | **3** (L1, L2, L3) |
| **CRITICAL issues** | **0** |

---

## Detailed Notes on HIGH Findings

### H1 + H2 — Stale `Result<byte[], Error>` Return Type in spec.md and clarify.md

The feature went through a mid-planning revision captured in plan.md §Revised return type (lines 397–420). The revision was correctly propagated to:
- `data-model.md` §New Application Types
- `contracts/application-contracts.md` §1.2, §1.4, §3.1, §4
- `tasks.md` T003, T005 step 7, T009, T011

But was **not** back-propagated to:
- `spec.md` Key Entities line 118: still says `"returns Result<byte[], Error>"`
- `spec.md` Clarifications line 77: still says `"A: Result<byte[], Error>"`
- `clarify.md` Decision 2 line 33: still says `"Result<byte[], Error>"`

An implementor following spec.md alone would write the wrong type signature, causing a compile error when the Desktop layer attempts to pass `Result<byte[], Error>` to a `Func<ExportFilingResult, Task>`.

### H3 — Wrong C# Property Names in spec.md Key Entities

`spec.md:L117` uses backtick-formatted names suggesting C# identifiers:
> Key fields: `` `JMBG` ``, `FullName`, `Address`, `OpstinaCode`, `` `Phone` ``, `Email`

Actual `TaxpayerProfile.cs` properties (confirmed by source):
- `Jmbg` (not `JMBG`)
- `PhoneNumber` (nullable `string?`) (not `Phone` — no such property exists)

plan.md R-002 documents this correction and data-model.md §TaxpayerProfile notes it with a ⚠️ warning. However, spec.md — the authoritative spec artifact — was never corrected.

---

## Next Actions

### Before `/speckit.implement`

Resolve **H1**, **H2**, **H3** first. These are documentation inconsistencies that will cause compile errors or wrong implementations if followed literally from spec.md:

1. **Run `/speckit.specify` with refinement** to patch spec.md Key Entities: correct the return type of `ExportFilingCommand` to `Result<ExportFilingResult, Error>` and fix `TaxpayerProfile` field names (`Jmbg`, `PhoneNumber`).
2. **Manually add amendment note** to `clarify.md` Decision 2 marking `Result<byte[], Error>` as superseded.

### Can Proceed, but Address Soon

3. **Fix data-model.md annotated sample** (M1) — set `ObracunatiPorez` to the same value as `OsnovicaZaPorez` to prevent implementor confusion.
4. **Mark stale plan.md design block** (M2) — annotate pre-revision `FilingsViewModel` code as superseded.
5. **Address FR-008 write-error cleanup** (M3) — add explicit try/catch + `file.DeleteAsync()` in T011 delegate on write failure.
6. **Add timing checkpoint to T014** (M4) — extend smoke-test to include explicit < 3 s timing check.
7. **Reconsider T010 AXAML vs code-behind** (M5) — attempt preferred AXAML `Command` binding first.

### Low-Priority Improvements

8. Prefix T012 test names with `Serialize_` (L2) to comply with constitution naming convention.
9. Resolve ThrownExceptions asymmetry (L3) — document intentional or extend to all commands.
10. Annotate clarify.md Decision 2 as superseded (L1).

---

> **Would you like me to suggest concrete remediation edits for the top 3 HIGH findings (H1, H2, H3)?**
