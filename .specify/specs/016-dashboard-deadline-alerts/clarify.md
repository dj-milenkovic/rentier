# Clarification Log — 016-dashboard-deadline-alerts

**Date**: 2025-07-18  
**Questions asked**: 5 / 5  
**Spec updated**: `.specify/specs/016-dashboard-deadline-alerts/spec.md`

---

## Q1 — IFilingRepository Extension Strategy

**Category**: Domain & Data Model / Constraints & Tradeoffs  
**Impact**: Blocks infrastructure design, query handler implementation, and infrastructure test scope.

**Question**: Should `IFilingRepository` use the existing `GetAllAsync` for dashboard queries, or should purpose-built methods be added?

**Answer**: Add two purpose-built read methods to `IFilingRepository`:
- `GetUpcomingAsync(DateOnly today, int days, CancellationToken ct)` — returns filings with status Init or Filed and `FilingDeadline` in `[today, today + days]`, sorted by `FilingDeadline` ascending.
- `GetOverdueAsync(DateOnly today, CancellationToken ct)` — returns filings with status Init or Filed and `FilingDeadline < today`.

Summary aggregates (`InitCount`, `FiledCount`, `PaidCount`, `TotalUnpaidRsd`) are computed in the query handler from a separate `IFilingRepository.GetAllAsync()` call to keep aggregate logic in the Application layer.

**Rationale**: Purpose-built repository methods allow the EF Core implementation to push date-range filtering to SQL (avoiding full table scans), while keeping aggregate computation in the handler aligns with the existing pattern (no raw SQL aggregates in repositories).

**Sections updated**: Key Entities (Filing note removed hedge), Functional Requirements (FR-019), Assumptions (line 6 of Assumptions), CA-001, CA-006.

---

## Q2 — LastSyncDate Source and Multi-Mailbox Resolution

**Category**: Integration & External Dependencies  
**Impact**: Blocks `GetDashboardQueryHandler` implementation — field path must be concrete before handler can be written.

**Question**: How is the last sync date resolved, given `Mailbox.Cursor.LastSyncDate` is a nested value object, and multiple mailboxes may exist?

**Answer**: 
1. Call `IMailboxRepository.GetAllAsync()` in the query handler.
2. Select the **maximum** `Mailbox.Cursor.LastSyncDate` across all returned mailboxes (LINQ: `.Select(m => m.Cursor.LastSyncDate).Where(d => d.HasValue).Select(d => d!.Value).MaxOrDefault()`).
3. If no mailbox exists, or all `Cursor.LastSyncDate` values are null, set `DashboardDto.LastSyncDate = null` → UI shows "no sync performed" message (FR-011).

**Field path confirmed from code**: `Mailbox.Cursor` is of type `MailboxCursor` (a `record`). `MailboxCursor.LastSyncDate` is `DateOnly?`. `IMailboxRepository.GetAllAsync()` already exists.

**Sections updated**: Key Entities (Mailbox bullet), Assumptions (LastSyncDate bullet).

---

## Q3 — Exact DTO Shapes

**Category**: Domain & Data Model  
**Impact**: Blocks DTO definitions, query handler mapping, ViewModel property bindings, and test data construction.

**Question**: What are the exact property names and types for `DashboardDto`, `UpcomingDeadlineDto`, and `OverdueFilingDto`?

**Answer** (all as C# positional records):

```csharp
public record DashboardDto(
    IReadOnlyList<UpcomingDeadlineDto> UpcomingDeadlines,
    IReadOnlyList<OverdueFilingDto> OverdueFilings,
    int InitCount,
    int FiledCount,
    int PaidCount,
    decimal TotalUnpaidRsd,
    DateOnly? LastSyncDate);

public record UpcomingDeadlineDto(
    Guid Id,
    string PayingEntity,
    DateOnly FilingDeadline,
    decimal TaxPayableRsd,
    FilingStatus Status,
    IncomeType IncomeType);

public record OverdueFilingDto(
    Guid Id,
    string PayingEntity,
    DateOnly FilingDeadline,
    decimal TaxPayableRsd);
```

**Notes**:
- `OverdueFilingDto` does **not** include `Status` (always Init or Filed by definition — redundant in UI).
- `UpcomingDeadlineDto` includes `IncomeType` to satisfy FR-004 (income type column in DataGrid).
- All money fields are `decimal`; all date fields are `DateOnly` (CA-002).

**Sections updated**: Key Entities (DashboardDto, UpcomingDeadlineDto, OverdueFilingDto bullets).

---

## Q4 — DashboardViewModel Navigation Wiring

**Category**: Interaction & UX Flow / Integration  
**Impact**: Blocks `MainWindowViewModel` update, `CompositionRoot` changes, and desktop layer tests.

**Question**: How is `DashboardViewModel` injected into `MainWindowViewModel`, given the current constructor signature only takes `FilingsViewModel`, `IServiceProvider`, and `SettingsViewModel`?

**Answer**:
- Add `DashboardViewModel dashboardVm` as the **first** constructor parameter of `MainWindowViewModel`.
- `NavigationEntries` list becomes: `[Dashboard, Filings, Reports, Settings]` (Dashboard at index 0).
- `_selectedEntry` and `_currentViewModel` default to `dashboardVm` at construction (replacing current default of `filingsVm`).
- Register `services.AddTransient<DashboardViewModel>()` in `CompositionRoot.AddDesktopServices()`.
- `MainWindowViewModel` remains `AddSingleton` (unchanged).
- Add `Strings.Nav_Dashboard` resource key to the `.resx` localization file.

**Confirmed from code**: `MainWindowViewModel` currently constructs `NavigationEntries` as a `new List<NavigationEntry>` inline. `NavigationEntry` is a `record(string Label, ReactiveObject ViewModel)`. Pattern for cross-VM navigation (e.g., Reports→Filings) uses `SelectedEntry = filingsEntry` — the same pattern applies for row-click dashboard→filings.

**Sections updated**: Functional Requirements (FR-021), CA-001, Assumptions (navigation wiring bullet).

---

## Q5 — Row-Click Behaviour in Upcoming Deadlines DataGrid

**Category**: Functional Scope & Behavior / Interaction & UX Flow  
**Impact**: Blocks ViewModel command design and View XAML interaction wiring; affects test scenarios.

**Question**: What happens when a user clicks a row in the upcoming deadlines DataGrid — navigate to filing detail, navigate to Filings pane, or do nothing?

**Answer**: Clicking a row navigates to the **Filings pane** by setting `MainWindowViewModel.SelectedEntry` to the `FilingsEntry` `NavigationEntry` object, using the same pattern already used by `ReportsViewModel` (which calls the `navigateToFilings` delegate). No detail flyout, modal, or new view is opened.

**Implementation note**: `DashboardViewModel` should expose a `ReactiveCommand<Guid, Unit> NavigateToFilingsCommand` (or equivalent); the command is wired in `MainWindowViewModel` via a delegate parameter — mirroring how `ReportsViewModel.navigateToFilings` is currently wired.

**Sections updated**: User Story 1 (acceptance scenario 6 added), Functional Requirements (FR-020).

---

## Coverage Summary

| Taxonomy Category | Pre-Clarify Status | Post-Clarify Status |
|---|---|---|
| Functional Scope & Behavior | Partial (row-click missing) | **Resolved** (Q5) |
| Domain & Data Model | Partial (DTO shapes vague, repo hedge) | **Resolved** (Q1, Q3) |
| Interaction & UX Flow | Partial (nav wiring missing) | **Resolved** (Q4) |
| Non-Functional Quality Attributes | Clear | Clear |
| Integration & External Dependencies | Partial (LastSyncDate path/multi-mailbox) | **Resolved** (Q2) |
| Edge Cases & Failure Handling | Clear | Clear |
| Constraints & Tradeoffs | Partial (repo strategy hedge) | **Resolved** (Q1) |
| Terminology & Consistency | Clear | Clear |
| Completion Signals | Clear | Clear |
| Misc / Placeholders | Partial (assumption hedge) | **Resolved** (Q1) |

All 5 questions resolved. No outstanding or deferred items.

---

## Recommended Next Step

All critical ambiguities resolved. Proceed to:

```
/speckit.plan
```
