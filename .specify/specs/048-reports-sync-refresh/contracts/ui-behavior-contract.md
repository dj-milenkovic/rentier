# UI Behavior Contract: Reports Sync Refresh

## Contract: Post-Sync Table Refresh

**Trigger**: `SyncCommand` execution completes successfully
**Observable Effect**: `Rows` collection is re-populated from database query
**Preserved State**: `CurrentPage`, `SortDescending` (all query parameters)

### Sequence

```
1. User clicks "Sync Mailbox" button
2. SyncCommand.Execute() fires
3. _syncHandler.HandleAsync(SyncMailboxCommand) executes
4. IF result.IsSuccess:
   a. LoadPageAsync(ct) — re-queries GetReportsQuery with current page/sort
   b. Rows collection cleared and re-populated
   c. SyncProgressValue = 100
   d. SyncStatusMessage set with report count
5. IF result.IsFailure:
   a. SyncStatusMessage set with error message
   b. NO table refresh (Rows unchanged)
```

### Guarantees

| Property | Guarantee |
|----------|-----------|
| Table data | Fresh from DB after successful sync |
| Sort order | Unchanged (`SortDescending` preserved) |
| Current page | Unchanged (`CurrentPage` preserved) |
| Error isolation | Sync failure does not trigger refresh |
| Status message | Always set regardless of refresh outcome |
| No navigation | User stays on Reports page |
