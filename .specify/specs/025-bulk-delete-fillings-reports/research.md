# Research: Bulk Delete for Filings and Reports

**Feature**: 025-bulk-delete-fillings-reports  
**Date**: 2025-07-15

## Research Topics

### 1. Bulk Delete Pattern in CQRS — Single Command vs Loop of Single Deletes

**Decision**: Introduce dedicated `BulkDeleteFilingsCommand(IReadOnlyList<Guid> FilingIds)` and `BulkDeleteReportsCommand(IReadOnlyList<Guid> ReportIds)` commands with corresponding handlers.

**Rationale**:
- Looping single `DeleteFilingCommand` per ID would create N separate DbContext instances (transient lifetime), N `SaveChangesAsync` round-trips, and N separate transactions. This is both slow and non-atomic.
- A single command with a list of IDs allows the handler to load all entities in one query, call `RemoveRange`, and issue one `SaveChangesAsync` — a single transaction for atomicity.
- Matches the existing `DeleteByReportIdAsync` pattern in `FilingRepository` which already uses `RemoveRange` for batch deletion.
- The CQRS interface `ICommandHandler<TCommand, TResult>` accepts any `TCommand` record — no framework changes needed.

**Alternatives Considered**:
- **Loop single deletes**: Rejected — N round-trips to SQLite, no atomicity, worse performance at scale.
- **EF Core `ExecuteDeleteAsync`**: Considered — used in `HolidayRepository` — but requires raw SQL semantics, skips change tracker, and doesn't compose well with the "load-then-remove" pattern mandated in the `IFilingRepository.DeleteByReportIdAsync` doc comment. Consistent pattern preferred.
- **MediatR/pipeline dispatch**: Rejected — project doesn't use MediatR; handlers are registered directly. Adding it for one feature is over-engineering.

---

### 2. Repository Interface Design — New Method vs Overloading `DeleteAsync`

**Decision**: Add a new `DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct)` method to both `IFilingRepository` and `IReportRepository`.

**Rationale**:
- Overloading `DeleteAsync(Guid id)` to also accept a collection would be a breaking semantic change that conflates single and batch intent.
- A distinct method name communicates batch semantics clearly and allows implementations to optimise (single `WHERE IN` query + `RemoveRange`).
- Consistent with the existing `DeleteByReportIdAsync` pattern that already handles batch deletion by a different criterion.

**Alternatives Considered**:
- **Overload `DeleteAsync(IEnumerable<Guid>)`**: Rejected — ambiguous intent, less discoverable, harder to reason about in tests.
- **No repo change; handler loops `DeleteAsync`**: Rejected — same N-round-trip problem described above.

---

### 3. Report Cascade Strategy for Bulk Delete

**Decision**: The `BulkDeleteReportsCommandHandler` will call `_filingRepository.DeleteByReportIdAsync(reportId)` in a loop for each selected report ID, then call `_reportRepository.DeleteManyAsync(reportIds)`. This reuses the existing cascade pattern from `DeleteReportCommandHandler`.

**Rationale**:
- EF Core FK config uses `OnDelete(DeleteBehavior.SetNull)` for Filing→Report, meaning DB-level cascade only nulls out `ReportId` on filings rather than deleting them. The spec requires actual deletion of linked filings.
- Application-level cascade (delete filings first, then reports) is the established pattern in `DeleteReportCommandHandler`.
- For bulk reports, we iterate the filing cleanup per report to reuse the proven `DeleteByReportIdAsync`, then batch-delete all reports at once.

**Alternatives Considered**:
- **Change EF FK to `DeleteBehavior.Cascade`**: Rejected — would change existing single-delete semantics and break the deliberate SetNull design (filings should normally survive report deletion unless explicitly deleted).
- **Single SQL `DELETE FROM Filings WHERE ReportId IN (...)`**: Considered — more efficient, but would require a new repository method and potentially `ExecuteDeleteAsync` which contradicts the load-then-remove mandate.

---

### 4. Selection Model in Avalonia DataGrid — ViewModel Property vs DataGrid.SelectedItems

**Decision**: Add an `IsSelected` observable property to `FilingRowViewModel` and `ReportRowViewModel` with two-way binding to a checkbox column. The parent ViewModel (`FilingsViewModel` / `ReportsViewModel`) derives selection state reactively from the collection.

**Rationale**:
- Avalonia's `DataGrid.SelectedItems` is designed for row-highlight selection, not checkbox-based multi-select. It doesn't support checkbox columns natively.
- Adding `IsSelected` to row ViewModels gives full MVVM control over selection state, allows reactive aggregation of selected count, and is testable without UI.
- The parent ViewModel subscribes to `INotifyPropertyChanged` events from rows or uses a reactive approach (`WhenAnyValue` / `ObservableForProperty`) to compute `SelectedCount` and `HasSelection`.
- This pattern is standard in MVVM checkbox-list implementations and well-tested in Avalonia community.

**Alternatives Considered**:
- **DataGrid.SelectedItems binding**: Rejected — Avalonia DataGrid's SelectedItems is not easily bindable for checkbox UX; requires code-behind workarounds and doesn't support the visual pattern (checkbox column) specified in the feature.
- **Separate `SelectionService`**: Over-engineered for this scope — selection is page-local and cleared on navigation.

---

### 5. Reactive Toolbar State — Observable Pipeline Design

**Decision**: Use ReactiveUI's `WhenAnyValue` and `ObservableAsPropertyHelper` to derive toolbar state properties:
- `SelectedCount` (int) — count of rows where `IsSelected == true`
- `HasSelection` (bool) — `SelectedCount > 0`
- `HasItems` (bool) — `Rows.Count > 0`
- `DeleteSelectedLabel` (string) — `$"Delete Selected ({SelectedCount})"`

**Rationale**:
- ReactiveUI's observable pipeline guarantees UI updates within the same event loop tick as the source change, well within the 200ms requirement (SC-004).
- This is the established pattern in the codebase — `FilingsViewModel` already uses `WhenAnyValue` for `HasItems`, `IsEmpty`, pagination state, etc.
- `ObservableAsPropertyHelper` properties are read-only, preventing accidental mutation.

**Alternatives Considered**:
- **Manual `PropertyChanged` aggregation**: More error-prone, requires explicit recalculation on every row change.
- **Polling timer**: Absurd for this use case — latency, CPU waste.

---

### 6. Confirmation Dialog Approach for Bulk Operations

**Decision**: Reuse the existing `ConfirmDialogHelper.ShowAsync(title, message, confirmText, cancelText)` pattern. The ViewModel constructs a dynamic message string using `string.Format` on a localised template from `Strings.resx` that includes the count N. For Reports, the message includes the cascade warning.

**Rationale**:
- The existing dialog pattern is well-tested, returns `Task<bool>`, handles headless/test scenarios (returns false when no window), and requires zero new UI components.
- Parameterised resource strings like `"You are about to delete {0} filing(s). This action cannot be undone."` allow clean localisation.
- The 2-arg delegate (`Func<string, string, Task<bool>>`) already registered for ReportsViewModel is sufficient.

**Alternatives Considered**:
- **New custom dialog with item list preview**: Over-engineered — the spec asks for count summary only, not individual item preview.
- **Inline confirmation (expand panel instead of dialog)**: Doesn't match existing UX pattern; would require new component development.

---

### 7. Preventing Double-Submission During Async Delete

**Decision**: Use `ReactiveCommand`'s built-in `CanExecute` observable. The `BulkDeleteCommand` will include an `isExecuting` predicate that disables the button while the command is in flight.

**Rationale**:
- `ReactiveCommand.CreateFromTask` automatically tracks execution state via `IsExecuting`. Binding button `IsEnabled` to `!IsExecuting` is the standard ReactiveUI pattern.
- No custom state management or manual locking required.
- Already proven in the codebase — `SyncCommand` in `ReportsViewModel` uses `IsEnabled="{Binding !IsSyncing}"` for the same purpose.

**Alternatives Considered**:
- **Manual `_isDeleting` flag**: Redundant — ReactiveCommand handles this natively.
- **Debounce/throttle**: Addresses a different problem (rapid repeated clicks vs concurrent execution).

---

### 8. Handling Already-Deleted Items (Edge Case)

**Decision**: The `DeleteManyAsync` repository method loads entities by ID, filters to only those that exist, and calls `RemoveRange` on the found set. Missing IDs are silently skipped. The handler returns success.

**Rationale**:
- Spec edge case: "already-deleted items are skipped without error."
- This is consistent with existing `DeleteAsync(Guid id)` which does a `FindAsync` and no-ops on null.
- In a local SQLite app with single-user access, this race condition is rare but should be handled gracefully.

**Alternatives Considered**:
- **Return partial failure result**: Over-engineered — the spec says "skip without error" not "report which ones were missing."
- **Throw on missing IDs**: Violates spec requirement.

---

### 9. "Select All" Scope — Current Page vs All Pages

**Decision**: "Select All" applies only to the items currently loaded in `Rows` (current page for Filings, all loaded records for Reports).

**Rationale**:
- Spec assumption: "Select All on the Filings page applies only to the currently visible page."
- Reports page loads all records (no pagination) so Select All = all reports.
- This avoids complexity of cross-page selection state, which would require tracking IDs across page loads.

**Alternatives Considered**:
- **Cross-page select all with batch fetch**: Out of scope per spec assumptions. Would require new query patterns and selection persistence across navigation.

---

### 10. Post-Delete Page Adjustment for Filings (Pagination Edge Case)

**Decision**: After bulk delete, reload the current page. If the current page becomes empty (all items deleted) and it's not page 1, decrement to the previous page before reloading.

**Rationale**:
- Extends the existing single-delete pattern in `FilingsViewModel.DeleteCommand` which already handles this case: `if (Rows.Count == 1 && _currentPage > 1) _currentPage--;`
- For bulk delete, the check becomes: if the number of selected items on the current page equals `Rows.Count` and `_currentPage > 1`, decrement page.
- Reports page has no pagination — reload simply fetches all records again.

**Alternatives Considered**:
- **Always reset to page 1**: Disruptive UX — user loses their place in a long list when deleting a few items from page 5.
- **Smart page calculation based on remaining total count**: Over-engineered — the simple "decrement if current page is now empty" covers all practical cases.
