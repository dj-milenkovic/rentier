# Specification Analysis Report — Feature 005: Importer Configuration

**Generated**: 2026-04-06  
**Artifacts analysed**: `spec.md`, `plan.md`, `data-model.md`, `tasks.md`, `clarify.md`, `contracts/IImporterRepository.cs`  
**Constitution**: `.specify/memory/constitution.md` (v1.0.0)  
**Source baseline**: Features 001–004 complete (SettingsViewModel has 3 params; MailboxSettingsViewModel registered)  
**Mode**: READ-ONLY — no source code touched

---

## Findings Table

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| C1 | Inconsistency | CRITICAL | `contracts/IImporterRepository.cs:57-59` vs `tasks.md:T020` | `GetByIdAsync` implementation note uses `FirstOrDefaultAsync` but T020 explicitly says **use `FindAsync`** and **"do NOT use `FirstOrDefaultAsync`"**. An implementer following the contracts file produces a subtly different repository. | Fix contracts note to use `FindAsync([id], ct)` |
| C2 | Inconsistency | CRITICAL | `contracts/IImporterRepository.cs:77-82` vs `tasks.md` pre-flight + `T020` | `DeleteAsync` implementation note uses `ExecuteDeleteAsync` — the exact pattern the pre-flight warning and T020 **explicitly prohibit** ("DO NOT use `ExecuteDeleteAsync` anywhere — the EF Core SQLite in-memory provider used in infrastructure tests does not support it"). | Fix contracts note to use `FindAsync + Remove + SaveChangesAsync` |
| C3 | Inconsistency | CRITICAL | `plan.md:162-166` vs `tasks.md` pre-flight + `T020` | Plan project-structure section says `DeleteAsync: ExecuteDeleteAsync(i => i.Id == id) — no-op if not found`. This contradicts the tasks prohibition on `ExecuteDeleteAsync`. | Fix plan.md project-structure line to use `FindAsync + Remove + SaveChangesAsync` |
| C4 | Inconsistency | CRITICAL | `clarify.md:AD-4:110-114` vs `data-model.md:§4` + `tasks.md:T017` | clarify.md AD-4 calls the FK pattern "Shadow FK approach" and uses the string overload `HasForeignKey("MailboxId")` / `HasForeignKey("TaxpayerProfileId")`. The entity in data-model.md defines `Guid? MailboxId` and `Guid? TaxpayerProfileId` as **real CLR properties** — the correct and consistent approach in data-model.md and T017 is the **expression overload** `HasForeignKey(i => i.MailboxId)`. An implementer reading clarify.md would use the string overload, creating an actual EF shadow property instead of binding to the real property. | Fix clarify.md AD-4 to use expression-based overload and label it "Real-property FK (no navigation property)" |
| H1 | Underspecification | HIGH | `tasks.md:T023` vs `tasks.md:T024` + `T033` | `ImporterItemViewModel` (T023) stores only `Id`, `DisplayName`, `ReportTypeDisplay` — 3 display fields. T024 requires "populate all form fields from the corresponding `ImporterDto` (resolved via `ImporterItems`)" when `SelectedImporter` changes, including `FromFilter`, `TaxpayerProfileId`, `MailboxId`, etc. There is no mechanism to get these fields from the 3-field `ImporterItemViewModel`. T033's test `SelectImporter_PopulatesFormFields` explicitly asserts `FromFilter` and other fields — which cannot pass without the full DTO accessible. | Extend T023 to store the backing `ImporterDto` and expose it. Update T024 form-population logic to read from `SelectedImporter.Dto`. |
| H2 | Missing Coverage | HIGH | `tasks.md:T031` vs `plan.md:§ Strings.resx` + `constitution §Coding Standards` | `Importers_Saved_Confirmation` = `"Importer saved."` appears in plan.md's list of 14 resx keys but is **absent from T031**. T024 sets `SuccessMessage` after a save — if the key is never added to Strings.resx the implementer will hardcode the string, violating constitution rule: *"User-visible strings MUST be in `Resources/Strings.resx`."* | Add `Importers_Saved_Confirmation = "Importer saved."` to T031; add note in T024 to assign `SuccessMessage = Strings.Importers_Saved_Confirmation` |
| H3 | Underspecification | HIGH | `tasks.md:T025` vs `spec.md:FR-007` + `data-model.md:§2` | T025 says `ComboBox ItemsSource="{Binding AvailableReportTypes}"` with "(display via `ReportTypeDisplay` converter or `ToDisplayString`)" as a vague parenthetical. Without a concrete ItemTemplate or FuncValueConverter, Avalonia renders the raw enum name `"IbkrCsv"` — violating FR-007 ("The initial version MUST include exactly one option: IBKR CSV"). No ItemTemplate or converter AXAML is specified. | Specify concrete implementation: use an `ItemTemplate` with a `TextBlock` whose `Text` is set via a declared `FuncValueConverter<ReportType, string>` that calls `ToDisplayString()`. |
| M1 | Inconsistency | MEDIUM | `tasks.md:T024` vs `data-model.md:§7` + `plan.md:DesktopVM` | `DeleteCommand` `canExecute` in T024 uses only `this.WhenAnyValue(x => x.SelectedImporter).Select(i => i != null)`. data-model.md §7 and plan.md DesktopVM section both specify `canExecute: SelectedImporter != null && IsEditMode`. Functionally equivalent in normal flow (the two conditions are always co-true in normal flow), but inconsistency could confuse a reviewer or future maintainer. | Update T024 to use `this.WhenAnyValue(x => x.SelectedImporter, x => x.IsEditMode, (s, e) => s != null && e)` to match data-model.md |
| M2 | Inconsistency | MEDIUM | `spec.md:§Schema Notes:280-292` | spec.md Schema Notes pseudo-code (marked "planning reference only") uses the string overload `HasForeignKey("MailboxId")` — same issue as C4. While marked as pseudo-code, it contradicts the canonical data-model.md and may mislead reviewers. | Update to expression overload and rename section to "Real-property FK" |
| M3 | Inconsistency | MEDIUM | `plan.md:162` | Plan project-structure says `GetByIdAsync: FirstOrDefaultAsync(i => i.Id == id)` — tasks.md T020 says use `FindAsync`. | Fix plan.md line to `GetByIdAsync: FindAsync([id], ct)` |
| M4 | Terminology Drift | MEDIUM | `plan.md:§Strings.resx` vs `tasks.md:T031` | plan.md lists `Importers_NoneOption_Label` (single key for both dropdowns). tasks.md T031 uses two separate keys `Importers_NoProfile_Placeholder` and `Importers_NoMailbox_Placeholder`, which T025 references. tasks.md version is correct and more specific; plan.md is outdated. | Note: tasks.md T031/T025 are consistent with each other and should be authoritative. No fix needed in tasks.md; plan.md terminology is informational only. |
| M5 | Terminology Drift | MEDIUM | `plan.md:§Strings.resx` vs `tasks.md:T031` | `Importers_AttachmentRegex_Label` value: plan.md says `"Attachment Pattern (Regex)"`, T031 says `"Attachment Regex"`. tasks.md T025 references this key for the AXAML label. The tasks.md value is authoritative; plan.md is stale. | No fix needed in tasks.md; plan.md is informational. |
| L1 | Ambiguity | LOW | `tasks.md:T021` | T021 setup note says "or use `Guid.NewGuid()` database name" for test isolation alongside the `:memory:` approach. In-memory SQLite connections are per-connection isolated; the `Guid.NewGuid()` note refers to the `Cache=Shared` variant (`Data Source={guid};Mode=Memory;Cache=Shared`). The note is technically correct but may confuse since the specified approach is plain `:memory:`. | Clarify or remove the "Guid.NewGuid()" alternative note in T021; plain `:memory:` per DbContext instance is sufficient. |
| L2 | Ambiguity | LOW | `tasks.md:T025` label text | T025 AXAML uses `{x:Static res:Strings.Importers_AttachmentRegex_Label}` whose value is `"Attachment Regex"` (from T031). The spec FR-010 description says "AttachmentRegex" not "Attachment Regex" — minor label word choice inconsistency, no functional impact. | Low priority; can be left as "Attachment Regex" or changed to "Attachment Pattern" — just align spec and T031 if desired. |

---

## Coverage Summary Table

| Requirement Key | Has Task? | Task IDs | Notes |
|-----------------|-----------|----------|-------|
| FR-001 — Importers tab | ✅ | T029, T030, T031 | Full coverage |
| FR-002 — Two-panel layout | ✅ | T025 | Full coverage |
| FR-003 — List entry display name + subtitle | ✅ | T023, T025 | Full coverage |
| FR-004 — Form field population on select | ⚠️ | T024 | **HIGH H1**: `ImporterItemViewModel` missing full DTO; form fields inaccessible |
| FR-005 — Add New / Save / Delete toolbar | ✅ | T024, T025 | Full coverage |
| FR-006 — DisplayName validation (required, ≤200) | ✅ | T002, T003, T024, T025 | Full coverage |
| FR-007 — ReportType ComboBox (shows "IBKR CSV") | ⚠️ | T024, T025 | **HIGH H3**: Display approach unspecified; will show "IbkrCsv" without fix |
| FR-008 — TaxpayerProfile ComboBox (optional) | ✅ | T024, T025 | Full coverage |
| FR-009 — Mailbox ComboBox (optional) | ✅ | T024, T025 | Full coverage |
| FR-010 — AttachmentRegex validation | ✅ | T010, T011, T014, T015 | Full coverage |
| FR-011 — FromFilter / SubjectFilter plain text | ✅ | T002, T010, T011 | Full coverage |
| FR-012 — PaymentNotes multiline ≤4000 | ✅ | T002, T025 | Full coverage |
| FR-013 — Add importer → new row + Guid | ✅ | T006, T010, T014 | Full coverage |
| FR-014 — Update importer via UpdateDetails | ✅ | T007, T011, T015 | Full coverage |
| FR-015 — Delete importer | ✅ | T008, T012, T016, T020 | Full coverage |
| FR-016 — SetNull on TaxpayerProfile/Mailbox delete | ✅ | T017, T021 | Full coverage (see also CRITICAL C1–C4 for consistency) |
| SC-002 — List < 1s for ≤50 importers | ⚠️ | T009, T021 | No dedicated performance/load test task; covered informally |
| SC-003 — Regex error before DB 100% | ✅ | T010, T014 | Full coverage |
| SC-004 — SetNull behaviour | ✅ | T017, T021 | Infrastructure integration test present |
| SC-006 — 100% domain / ≥90% app test coverage | ✅ | T003, T013–T016 | Coverage gates defined |

---

## Constitution Alignment

All 5 constitution principles verified — **no violations** in spec/plan/tasks design intent:

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Clean Architecture | ✅ PASS | Desktop → Application → Domain boundaries respected |
| II. Local-First Security | ✅ PASS | No network calls; SQLite local only |
| III. Financial/Temporal Correctness | ✅ PASS | No monetary or date fields |
| IV. Async and UI Responsiveness | ✅ PASS | `ReactiveCommand.CreateFromTask`, `RxApp.MainThreadScheduler`, no `.Result`/`.Wait()` |
| V. Specification-Driven Quality Gates | ⚠️ RISK | **HIGH H2**: `Importers_Saved_Confirmation` missing from T031 risks hardcoded string (violates Coding Standards). Resolved by H2 fix. |

---

## Unmapped Tasks

All 33 tasks (T001–T033) map to at least one requirement or story. No orphan tasks.

---

## Metrics

| Metric | Value |
|--------|-------|
| Total Functional Requirements | 16 (FR-001 – FR-016) |
| Total Success Criteria (buildable) | 4 (SC-002–SC-004, SC-006) |
| Total Tasks | 33 (T001–T033) |
| Requirements with ≥1 task | 18/20 = **90%** (FR-004, FR-007 flagged HIGH) |
| Ambiguity Count | 3 (H3, L1, L2) |
| Inconsistency Count | 6 (C1–C4, M1, M3) |
| Duplication Count | 0 |
| Underspecification Count | 2 (H1, H2) |
| **Critical Issues** | **4** (C1–C4) |
| **High Issues** | **3** (H1–H3) |
| Medium Issues | 5 (M1–M5) |
| Low Issues | 2 (L1–L2) |

---

## Next Actions

### ⛔ CRITICAL — Resolve Before `/speckit.implement`

The 4 CRITICAL findings are **implementation-time land mines** — following the contracts file or plan.md literally produces code that breaks the in-memory SQLite infrastructure tests:

1. **C1, C2** — Fix `contracts/IImporterRepository.cs`: replace `FirstOrDefaultAsync` with `FindAsync`; replace `ExecuteDeleteAsync` with `FindAsync + Remove + SaveChangesAsync`.
2. **C3** — Fix `plan.md` project structure: replace `DeleteAsync: ExecuteDeleteAsync` line.
3. **C4** — Fix `clarify.md` AD-4: replace string-overload FK snippet with expression-overload; rename "Shadow FK approach" → "Real-property FK (no navigation property)".

### ⚠️ HIGH — Resolve Before Desktop Phase

4. **H1** — Fix `tasks.md` T023: store full `ImporterDto` on `ImporterItemViewModel`. Update T024 form-population and T033 test accordingly.
5. **H2** — Fix `tasks.md` T031: add `Importers_Saved_Confirmation = "Importer saved."`. Update T024 SuccessMessage assignment to use the resx key.
6. **H3** — Fix `tasks.md` T025: add concrete `ItemTemplate`/converter AXAML specification for ReportType ComboBox display.

### ✅ Proceed With Caution (MEDIUM/LOW)

After fixing CRITICAL and HIGH issues, implementation can proceed. The MEDIUM issues (M1–M5) are documentation inconsistencies between plan.md and tasks.md. Since tasks.md is the implementation source of truth and is internally consistent, M4/M5 require no change to tasks.md. M1 and M3 are recommended fixes to tasks.md for correctness.

---

## Remediation Applied

> All CRITICAL and HIGH issues listed above have been remediated in-place.
> See individual fixes in `contracts/IImporterRepository.cs`, `plan.md`, `clarify.md`, and `tasks.md`.
> `spec.md` Schema Notes pseudo-code was also corrected (M2) for reader clarity.
