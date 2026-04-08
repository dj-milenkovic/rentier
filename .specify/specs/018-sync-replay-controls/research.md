# Research: 018 Sync Replay Controls

**Feature Branch**: `feature/018-sync-replay-controls`
**Date**: 2025-07-15

## R-001: Avalonia CalendarDatePicker with DateOnly Binding

**Decision**: Use Avalonia `CalendarDatePicker` control bound to a `DateTimeOffset?` wrapper property in the ViewModel, with internal conversion to `DateOnly`.

**Rationale**: Avalonia's `CalendarDatePicker.SelectedDate` expects `DateTimeOffset?`, not `DateOnly`. The Rentier codebase already uses this exact pattern in `MailboxSettingsViewModel` (the `InitialSyncDateOffset` / `InitialSyncDate` pair). Reusing this established pattern ensures consistency and leverages proven code.

**Implementation Pattern**:
```csharp
// ViewModel
private DateOnly? _replayFromDate;
public DateOnly? ReplayFromDate
{
    get => _replayFromDate;
    set
    {
        this.RaiseAndSetIfChanged(ref _replayFromDate, value);
        this.RaisePropertyChanged(nameof(ReplayFromDateOffset));
    }
}

public DateTimeOffset? ReplayFromDateOffset
{
    get => _replayFromDate.HasValue
        ? new DateTimeOffset(_replayFromDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
        : null;
    set => ReplayFromDate = value.HasValue
        ? DateOnly.FromDateTime(value.Value.DateTime)
        : null;
}
```

```xml
<!-- AXAML -->
<CalendarDatePicker
    SelectedDate="{Binding ReplayFromDateOffset, Mode=TwoWay}"
    IsVisible="{Binding IsReplayFromDateMode}"
    DisplayDateEnd="{Binding TodayOffset}"
    Watermark="{x:Static res:Strings.Sync_ReplayDate_Watermark}" />
```

**Alternatives Considered**:
- `DatePicker` (spinner-based): Rejected — less user-friendly for arbitrary date selection; `CalendarDatePicker` shows a full calendar popup that matches "pick a date to replay from" UX better.
- Custom `IValueConverter` for `DateOnly` ↔ `DateTimeOffset?`: Rejected — adds indirection and doesn't improve readability over the ViewModel wrapper pattern already used in the codebase.
- Direct `DateOnly` binding with custom control: Rejected — would require forking Avalonia's control, excessive effort for no gain.

---

## R-002: Avalonia ComboBox Enum Binding for Mode/Strategy Selection

**Decision**: Expose enum values via `Enum.GetValues<T>()` arrays in the ViewModel and bind to `ComboBox.ItemsSource` with `SelectedItem` two-way binding. Use `FuncValueConverter<T, string>` static instances for display names with localized strings.

**Rationale**: This is the exact pattern used throughout Rentier — `ImporterSettingsView` binds `ReportType` enum to a ComboBox using `AvailableReportTypes` array + `ReportTypeDisplayConverter`. Reusing this pattern means no new infrastructure and familiar code for all contributors.

**Implementation Pattern**:
```csharp
// ViewModel
public SyncMode[] AvailableSyncModes { get; } = Enum.GetValues<SyncMode>();
public DuplicateStrategy[] AvailableDuplicateStrategies { get; } = Enum.GetValues<DuplicateStrategy>();

private SyncMode _selectedSyncMode = SyncMode.Incremental;
public SyncMode SelectedSyncMode
{
    get => _selectedSyncMode;
    set => this.RaiseAndSetIfChanged(ref _selectedSyncMode, value);
}
```

```xml
<!-- AXAML -->
<ComboBox ItemsSource="{Binding AvailableSyncModes}"
          SelectedItem="{Binding SelectedSyncMode, Mode=TwoWay}">
    <ComboBox.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Converter={x:Static local:SyncModeDisplayConverter.Instance}}" />
        </DataTemplate>
    </ComboBox.ItemTemplate>
</ComboBox>
```

**Alternatives Considered**:
- `record` wrapper objects with `Value` + `Label`: Rejected — overkill for 3-item enums; `FuncValueConverter` with localized strings achieves the same display quality.
- `DataTemplateSelector`: Rejected — Avalonia doesn't have a native equivalent; the `ItemTemplate` + converter approach is simpler and proven.

---

## R-003: MailboxCursor Temporary Override Strategy for Replay

**Decision**: Introduce a `SyncParameters` value object that carries the effective start position (date or beginning-of-time) alongside mode and strategy. The existing `MailboxCursor` is never mutated during replay — it is read as the "real" cursor and the `SyncParameters` override determines the IMAP query. Cursor is only updated on successful completion.

**Rationale**: The current sync pipeline already follows the pattern of reading `Mailbox.Cursor`, building an IMAP query from it, then calling `Mailbox.UpdateCursor()` only on success. Replay simply bypasses the cursor read for query construction while preserving it for the final update. This requires zero changes to `MailboxCursor` itself — the override is external, passed through `SyncParameters`.

**Implementation**:
```csharp
// New value object in Domain
public record SyncParameters(
    SyncMode Mode,
    DuplicateStrategy Strategy,
    DateOnly? ReplayFromDate = null,   // Only for ReplayFromDate mode
    Guid? ScopeImporterId = null       // Only for Full Replay per-importer
);

// In ImapMailboxSyncService.SyncAsync:
// Instead of: query from Mailbox.Cursor
// Now: query from SyncParameters.GetEffectiveStartDate(cursor)
```

**Alternatives Considered**:
- Add `OverrideDate` field to `MailboxCursor`: Rejected — cursor is a pure position tracker; mixing override intent into it violates single responsibility and complicates serialization.
- Create a `ReplayCursor` subtype: Rejected — `MailboxCursor` is a `record` value object; subtyping records adds complexity and EF Core mapping burden for no benefit.
- Store replay state in a separate `ReplaySession` entity: Rejected — replay is a transient operation, not persisted state. A value object parameter is sufficient.

---

## R-004: Cursor Regression Prevention on Partial Replay Failure

**Decision**: Adopt a "cursor only advances on full success" policy. The cursor update is the final step after all messages for all importers are processed. If any message fails, the cursor is not advanced. Failed messages are logged with details for retry.

**Rationale**: The existing sync pipeline already does not advance the cursor on exception. The enhancement for replay is to ensure this holds when the effective query range is broader than the cursor (replay from a past date). The cursor must advance to `max(currentCursor, latestProcessedPosition)` — never regress.

**Implementation**:
```csharp
// After successful sync:
var newCursorDate = DateOnly.FromDateTime(DateTime.UtcNow);
var newCursorUid = maxUidProcessed;

// Prevent regression: only advance, never go backward
var effectiveCursor = new MailboxCursor(
    LastSyncDate: Max(mailbox.Cursor.LastSyncDate, newCursorDate),
    LastUid: Max(mailbox.Cursor.LastUid, newCursorUid)
);

mailbox.UpdateCursor(effectiveCursor);
```

**Key Rules**:
1. Cursor update is atomic — either fully committed or not at all (within EF Core SaveChanges transaction).
2. On cancellation (`OperationCanceledException`): cursor is NOT updated.
3. On partial failure (some messages error): cursor is NOT updated; user retries.
4. On full success: cursor advances to latest position, logged with before/after values.

**Alternatives Considered**:
- Per-message cursor advancement: Rejected — creates checkpoint overhead and partial-progress state that complicates retry logic; the current batch-commit model is simpler and sufficient.
- Two-phase cursor (tentative + committed): Rejected — adds persistence complexity for a problem that doesn't exist; the current "commit only on success" model already handles this.

---

## R-005: EF Core Migration Strategy for Removing InitialSyncDate

**Decision**: Create a single EF Core migration (`0010_RemoveInitialSyncDate`) that:
1. For mailboxes with `Cursor_LastSyncDate IS NULL` (never synced), copies `InitialSyncDate` to `Cursor_LastSyncDate` to preserve the intended start point.
2. Drops the `InitialSyncDate` column.

**Rationale**: `InitialSyncDate` was used as the starting point for the first sync. After the first sync, the cursor takes over and `InitialSyncDate` is never read again. For un-synced mailboxes, the value must be preserved to avoid changing behavior. Copying to cursor before drop is safe because cursor is only used as "start from here" on first sync.

**Migration SQL**:
```sql
-- Preserve InitialSyncDate for un-synced mailboxes
UPDATE Mailboxes
SET Cursor_LastSyncDate = InitialSyncDate
WHERE Cursor_LastSyncDate IS NULL;

-- Remove column
ALTER TABLE Mailboxes DROP COLUMN InitialSyncDate;
-- Note: SQLite doesn't support DROP COLUMN directly before 3.35.0
-- EF Core handles this via table rebuild for older SQLite versions
```

**Alternatives Considered**:
- Keep `InitialSyncDate` as deprecated but populated: Rejected — creates confusion about which date is authoritative; clean removal is better.
- Add a `DefaultSyncStartDate` to application settings: Rejected — the 90-day default for new mailboxes is simple enough to hardcode; no need for a setting.
- Soft-delete via rename to `_deprecated_InitialSyncDate`: Rejected — SQLite doesn't benefit from this; clean removal is simpler.

---

## R-006: Duplicate Detection Implementation per Strategy

**Decision**: Implement duplicate detection at the report level using the existing `ExistsByImporterAndNameAsync` check, with strategy-specific behavior:

| Strategy | When Duplicate Found | Implementation |
|----------|---------------------|----------------|
| Skip Existing | Skip report, log as skipped | Current behavior — no change |
| Create New Revision | Create new Report with `_rev{N}` suffix, link to original | New: add `OriginalReportId` FK to Report |
| Reprocess in Place | Delete old filings, re-parse into existing report | New: use `DeleteByReportIdAsync` + re-process |

**Rationale**: The existing sync pipeline already checks `ExistsByImporterAndNameAsync` before creating reports. The strategy pattern adds a branching point at this check — instead of always skipping, the system consults the active `DuplicateStrategy`.

**Safety Check for Reprocess in Place**: Before overwriting, check if any filing linked to the report has `Status != Init` (Filed or Paid). If so, fall back to Create New Revision and warn.

**Alternatives Considered**:
- Hash-based duplicate detection (content hash): Rejected — IMAP UIDs + report names are already unique per importer; adding hashing increases complexity without improving accuracy.
- Separate deduplication pass after import: Rejected — duplicates should be resolved at import time to avoid partial state.

---

## R-007: Impact Preview Estimation

**Decision**: The impact preview panel shows a read-only summary of what the sync will do, based on:
- **Mode**: Descriptive text (e.g., "Sync new emails since last sync" / "Replay all emails from 2024-06-01")
- **Scope**: Mailbox name + importer count, or specific importer name
- **Strategy**: Selected strategy with explanation
- **Estimated range**: "From: {date} to: today" (computed from cursor or replay date)

The preview does NOT make any server calls — it is computed locally from the cursor state and selected parameters.

**Rationale**: Making IMAP calls for preview would be slow and potentially unreliable. A local estimation based on cursor position and date range is instant and sufficient for user decision-making.

**Alternatives Considered**:
- IMAP SEARCH count before sync: Rejected — adds network latency to mode selection; users don't need exact counts, just directional guidance.
- Historical sync stats to estimate: Rejected — insufficient data for new mailboxes; local computation is simpler.
