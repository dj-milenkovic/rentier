# Clarification Summary — Feature 014: Reports List & Manual Import

**Date**: 2026-04-07  
**Spec**: `.specify/specs/014-reports-list-manual-import/spec.md`  
**Status**: Complete — 5 decisions encoded; ready to proceed to `/speckit.plan`

---

## Decisions Made

### 1. `GetReportsQuery` DTO Shape & Filing Count Strategy

**Decision**: `ReportRowDto(Guid Id, string ReportName, DateOnly ImportDate, string ImporterName, ReportStatus Status, int FilingCount)`.

**Mechanism**:
- Add `IReportRepository.GetAllWithFilingCountAsync(CancellationToken ct)` → `IReadOnlyList<(Report Report, int FilingCount)>`
- Infrastructure: single EF `GroupJoin` query against `Reports` ⟶ `Filings` table; returns count per report. Uses `AsNoTracking`.
- Handler resolves `ImporterName` via one `IImporterRepository.GetAllAsync()` call, builds a `Dictionary<Guid, string>` keyed by importer Id.
- Avoids N+1 queries; satisfies SC-001 (2-second load for 500 reports).

**Spec sections updated**: Key Entities (added `ReportRowDto`), Repository Extensions Required (new subsection), FR-018, Assumptions.

---

### 2. `ImportReportCommand` Contract & File I/O Boundary

**Decision**: `ImportReportCommand(Guid ImporterId, string FileName, byte[] CsvContent)`.

**Mechanism**:
- Desktop reads file bytes via Avalonia 11 `window.StorageProvider.OpenFilePickerAsync(...)` **before** dispatching the command.
- Handler sequence:
  1. `IStatementParser.ParseAsync(new MemoryStream(CsvContent), ct)` — validate format (FR-007)
  2. `IReportRepository.ExistsByImporterAndNameAsync(ImporterId, FileName, ct)` — reject duplicate (FR-008)
  3. `IReportRepository.AddAsync(Report.Create(..., status: Init, AttachmentContent: CsvContent), ct)` (FR-009)
  4. `ProcessReportsCommandHandler.HandleAsync(new ProcessReportsCommand(), ct)` (FR-010)
  5. Return `Result<Guid, Error>.Success(newReport.Id)`
- On any step failure → return `Result.Failure(...)`, no partial state left.

**Spec sections updated**: FR-018 (expanded command list), FR-019 (added), Assumptions (added import dispatch note).

---

### 3. "View Filings" Navigation Mechanism

**Decision**: Callback delegate pattern — no `INavigationService` abstraction introduced.

**Mechanism**:
- `ReportsViewModel` constructor accepts `Action<Guid> navigateToFilings`.
- `MainWindowViewModel` wires the delegate inline:
  ```csharp
  Action<Guid> navigateToFilings = reportId => {
      filingsVm.ReportIdFilter = reportId;
      SelectedEntry = NavigationEntries[0]; // Filings entry
  };
  ```
- `FilingsViewModel` gains `Guid? ReportIdFilter` reactive property (`RaiseAndSetIfChanged`). When set, `_currentPage = 1` and `LoadPageCommand.Execute().Subscribe()`.
- `GetFilingsQuery` gains `Guid? ReportIdFilter = null` parameter. `GetFilingsQueryHandler` branches: when set, calls `IFilingRepository.GetByReportIdAsync(ReportIdFilter.Value, ct)` (no paging), wraps in a single-page result.

**Rationale**: Consistent with existing `MainWindowViewModel` pattern. Avoids introducing a new abstraction; keeps all navigation wiring in one place.

**Spec sections updated**: FR-012, Assumptions (navigation mechanism).

---

### 4. Delete Cascade Strategy

**Decision**: Application-layer two-step delete (not DB FK cascade).

**New artifacts**:
- `DeleteReportCommand(Guid ReportId)` + `DeleteReportCommandHandler` (Application layer, `AddTransient`)
- `IFilingRepository.DeleteByReportIdAsync(Guid reportId, CancellationToken ct)` — bulk delete via EF 7+ `ExecuteDeleteAsync`
- `FilingRepository.DeleteByReportIdAsync(...)` implementation

**Handler sequence**:
1. `IFilingRepository.DeleteByReportIdAsync(command.ReportId, ct)` — deletes 0..N filings
2. `IReportRepository.DeleteAsync(command.ReportId, ct)` — deletes report
3. Return `Result<VoidResult, Error>.Success(...)`
- Entire body wrapped in try/catch; any exception → `Result.Failure(new Error("DELETE_FAILED", ex.Message))`

**Rationale**: Explicit control over deletion order; surfaceable errors; no reliance on SQLite FK enforcement which requires explicit PRAGMA in EF Core.

**Spec sections updated**: FR-014 (cascade detail), FR-018 (added `DeleteReportCommand`), Assumptions (delete cascade strategy), Repository Extensions Required.

---

### 5. `ReportsViewModel` Activation Pattern

**Decision**: `ReportsViewModel` MUST implement `IActivatableViewModel`.

**Mechanism**:
```csharp
public sealed class ReportsViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    this.WhenActivated(disposables =>
    {
        LoadReportsCommand.Execute().Subscribe().DisposeWith(disposables);
    });
}
```
- Matches the existing `FilingsViewModel` pattern exactly.
- `ReportsView.axaml.cs` must call `this.WhenActivated(...)` via `IViewFor<ReportsViewModel>` or use Avalonia's `WhenActivated` on the `UserControl`, consistent with other views.
- Satisfies FR-002 (auto-refresh on pane activation) and CA-005 (all WhenActivated subscriptions `.DisposeWith(disposables)`).

**Spec sections updated**: FR-019 (new requirement added).

---

## Coverage Summary

| Category | Status |
|---|---|
| Functional Scope & Behavior | ✅ Resolved |
| Domain & Data Model | ✅ Resolved — `ReportRowDto` defined, repository extensions specified |
| Interaction & UX Flow | ✅ Resolved — navigation callback pattern, activation pattern |
| Non-Functional Quality Attributes | ✅ Clear — SC-001/002/003 measurable; async confirmed |
| Integration & External Dependencies | ✅ Resolved — `ProcessReportsCommand` reuse confirmed, file I/O boundary defined |
| Edge Cases & Failure Handling | ✅ Clear — delete cascade error handling, import failure handling present |
| Constraints & Tradeoffs | ✅ Resolved — application-layer cascade rationale documented |
| Terminology & Consistency | ✅ Clear — `ReportRowDto`, `DeleteReportCommand` canonical names defined |
| Completion Signals | ✅ Clear — acceptance criteria testable |
| Misc / Placeholders | ✅ Resolved — no remaining TODOs or vague adjectives blocking implementation |

---

## Next Step

Proceed to `/speckit.plan` — all 5 high-impact ambiguities resolved; no Outstanding items remain.
