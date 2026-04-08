# Research: Dashboard with Deadline Alerts (016)

**Feature**: `016-dashboard-deadline-alerts`  
**Date**: 2025-07-18  
**Status**: All NEEDS CLARIFICATION items resolved via spec clarification and codebase analysis

---

## R-001 — Repository Strategy for Dashboard Queries

**Decision**: Add two purpose-built read methods to `IFilingRepository` (`GetUpcomingAsync`, `GetOverdueAsync`) plus one aggregate method (`GetFilingStatsAsync`).  
**Rationale**: Purpose-built methods push date-range filtering to SQL via EF Core LINQ, avoiding full table scans. `GetFilingStatsAsync` computes counts and sums at the database level rather than loading all entities via `GetAllAsync`.  
**Alternatives considered**: (1) Reuse `GetAllAsync` + in-memory filter/aggregation — rejected: unnecessary memory allocation and no query pushdown. (2) Single `GetDashboardDataAsync` returning all data — rejected: violates single-responsibility and makes testing harder.

---

## R-002 — LastSyncDate Resolution Path

**Decision**: Call `IMailboxRepository.GetAllAsync()` in the query handler, select max `Mailbox.Cursor.LastSyncDate` across all mailboxes via LINQ.  
**Rationale**: `MailboxCursor` is a simple `record(DateOnly? LastSyncDate, long? LastUid)` — not a discriminated union. `Cursor.LastSyncDate` is directly accessible. `GetAllAsync()` already exists and typically returns 0–2 mailboxes (single-user app).  
**Alternatives considered**: Add `GetLatestSyncDateAsync` to `IMailboxRepository` — rejected: over-engineering for 0–2 row result set; handler-side LINQ is simpler and more transparent.

**Confirmed from code**:
```csharp
// Rentier.Domain.ValueObjects.MailboxCursor
public record MailboxCursor(DateOnly? LastSyncDate, long? LastUid);

// Mailbox.Cursor is of type MailboxCursor (not nullable in domain, but LastSyncDate inside is nullable)
```

---

## R-003 — DashboardViewModel Injection Pattern

**Decision**: Construct `DashboardViewModel` via `ActivatorUtilities.CreateInstance<DashboardViewModel>(provider, navigateToFilings)` inside `MainWindowViewModel`, mirroring the `ReportsViewModel` pattern. **Not** registered in DI.  
**Rationale**: `DashboardViewModel` requires an `Action navigateToFilings` delegate that closes over `MainWindowViewModel.SelectedEntry`. This cannot be resolved from the DI container. The same pattern is already used for `ReportsViewModel` which takes `Action<Guid> navigateToFilings`.  
**Alternatives considered**: Register `DashboardViewModel` as `AddTransient` + register delegate in DI — rejected: delegate must close over `MainWindowViewModel` instance state.

---

## R-004 — NavigateToFilings Delegate Shape

**Decision**: `Action navigateToFilings` (no parameters — no `Guid`).  
**Rationale**: FR-020 specifies navigation to the Filings pane only — no filing selection or filtering. This is simpler than `Action<Guid>` and avoids coupling to `FilingsViewModel.ReportIdFilter`.  
**Alternatives considered**: `Action<Guid>` to select a specific filing — rejected per FR-020 ("No detail view or modal is opened").

---

## R-005 — Monetary Display Formatting

**Decision**: Use `CultureInfo.InvariantCulture` for formatting `TotalUnpaidRsd` as `$"{value:N2} RSD"`.  
**Rationale**: Constitution Principle III requires `decimal` for all monetary values. The user's architecture facts explicitly mandate `CultureInfo.InvariantCulture` for consistency.  
**Alternatives considered**: Use system locale (e.g., Serbian `sr-Latn-RS`) — rejected per explicit architecture constraint.

---

## R-006 — Dashboard Auto-Refresh on Re-Navigation

**Decision**: `DashboardViewModel` implements `IActivatableViewModel`. `WhenActivated` triggers `LoadCommand.Execute().Subscribe().DisposeWith(disposables)`, causing data reload every time the user navigates to the Dashboard.  
**Rationale**: Matches `FilingsViewModel` pattern exactly. Guarantees data freshness without manual refresh button. Spec edge case confirms "Users re-navigate to Dashboard to refresh."  
**Alternatives considered**: Manual refresh button — rejected: auto-load on activation is the established project pattern.

---

## R-007 — No Schema Changes Required

**Decision**: No EF Core migration needed. All dashboard data is computed from existing `Filings` and `Mailboxes` tables.  
**Rationale**: FR-018 explicitly states "No new database schema changes." All queries operate on existing columns: `Status`, `FilingDeadline`, `TaxPayableRsd`, `PayingEntity`, `IncomeType` on `Filings`; `Cursor` (JSON-serialised `MailboxCursor`) on `Mailboxes`.  
**Alternatives considered**: N/A — no schema changes were ever considered.
