# Clarification Summary — 013-pp-opo-xml-export

**Session date**: 2026-04-07  
**Spec file**: `.specify/specs/013-pp-opo-xml-export/spec.md`  
**Questions asked**: 5 / 5 (all auto-resolved from project context — no interactive loop required)

---

## Decisions Made

### Decision 1 — Repository Loading Chain & Null ReportId Handling

**Gap**: FR-003 said "load the filing, taxpayer profile, and linked importer's payment notes" without naming the repositories or specifying the null-ReportId path.

**Decision**: The `ExportFilingCommand` handler uses **four repositories** in the following chain:

```
IFilingRepository
  → IReportRepository       (via Filing.ReportId)
  → IImporterRepository     (via Report.ImporterId)
  → ITaxpayerProfileRepository
```

If `Filing.ReportId` is `null`, `PaymentNotes` is treated as an empty string and serialization continues normally (no error is raised for a missing importer link).

**Sections updated**: FR-003, Key Entities (ExportFilingCommand), Edge Cases, Assumptions.

---

### Decision 2 — ExportFilingCommand Return Type

**Gap**: Key Entities described ExportFilingCommand as "returns the serialized XML as a byte array" without using the project's Result pattern.

**Decision**: `ExportFilingCommand` returns `Result<byte[], Error>`.  
The handler is responsible only for loading entities and serializing to bytes. The Desktop layer (FilingsViewModel) owns the file dialog and disk write.

**Sections updated**: Key Entities (ExportFilingCommand), CA-001.

---

### Decision 3 — Avalonia 11 Save Dialog API

**Gap**: FR-002 said "native OS save dialog" without naming the Avalonia 11 API; using the legacy `SaveFileDialog` would break in Avalonia 11+.

**Decision**: Use **`window.StorageProvider.SaveFilePickerAsync(...)`**.  
`Avalonia.Controls.SaveFileDialog` is explicitly prohibited in this feature.

**Sections updated**: FR-002, CA-001, Assumptions.

---

### Decision 4 — Export Button Location & ViewModel Command Signature

**Gap**: FR-001 said "export action from a filing row in the filings list" without specifying the DataGrid column placement or the ReactiveCommand signature needed for binding.

**Decision**:
- A dedicated **"Export" column** is added to the **FilingsView DataGrid**.
- `FilingsViewModel` exposes **`ExportCommand`** typed as `ReactiveCommand<Guid, Unit>`.
- The Guid is the filing `Id` from the row; the command handles loading → serializing → dialog → write → error notification.

**Sections updated**: FR-001, Key Entities (FilingsViewModel added), CA-001.

---

### Decision 5 — No New EF Core Migration

**Gap**: The spec was silent on whether new database schema changes are required, creating potential confusion during planning/task decomposition.

**Decision**: **No new EF Core migration is needed.** All required data (`Filing`, `Report`, `Importer`, `TaxpayerProfile`) is already persisted by existing migrations. This feature is read-only with respect to the database.

**Sections updated**: Assumptions.

---

## Coverage Summary

| Taxonomy Category | Pre-Clarify Status | Post-Clarify Status |
|---|---|---|
| Functional Scope & Behavior | Partial | Resolved |
| Domain & Data Model | Partial | Resolved |
| Interaction & UX Flow | Partial | Resolved |
| Non-Functional Quality Attributes | Clear | Clear |
| Integration & External Dependencies | Partial | Resolved |
| Edge Cases & Failure Handling | Partial | Resolved |
| Constraints & Tradeoffs | Missing | Resolved |
| Terminology & Consistency | Clear | Clear |
| Completion Signals | Clear | Clear |
| Misc / Placeholders | Clear | Clear |

**All 5 questions resolved. No Outstanding or Deferred items.**

---

## Recommended Next Step

```
/speckit.plan
```

The spec is now unambiguous and ready for implementation planning. All repository wiring, return types, UI placement, and infrastructure constraints are explicit.
