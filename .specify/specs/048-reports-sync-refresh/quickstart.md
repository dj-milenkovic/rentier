# Quickstart: 048-reports-sync-refresh

## What This Feature Does

Fixes a bug where newly synced reports don't appear in the Reports table until the user navigates away and back. After this fix, pressing "Sync Mailbox" will automatically refresh the table data upon successful completion.

## Key Files

| File | Action | Description |
|------|--------|-------------|
| `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` | **Modify** | Add `LoadPageAsync(ct)` call in `HandleSyncAsync` after successful sync |
| `tests/Rentier.UnitTests/Desktop/ReportsViewModelTests.cs` | **Modify** | Add tests verifying table refresh after sync |

## The Fix (1 change)

In `ReportsViewModel.HandleSyncAsync()`, after `_syncHandler.HandleAsync()` succeeds, call `await LoadPageAsync(ct)` before setting the status message. This follows the exact same pattern used by `ImportAsync` and `DeleteAsync`.

### Before (lines 463–482):
```csharp
private async Task HandleSyncAsync(CancellationToken ct)
{
    SyncStatusMessage = null;
    SyncProgressValue = 0;

    var result = await _syncHandler.HandleAsync(new SyncMailboxCommand(SyncParameters.Default), ct);

    if (result.IsSuccess)
    {
        SyncProgressValue = 100;
        var r = result.Value;
        SyncStatusMessage = r.Errors.Count > 0
            ? $"Sync complete: {r.ReportsCreated} reports created, {r.Errors.Count} error(s)"
            : $"Sync complete: {r.ReportsCreated} reports created";
    }
    else
    {
        SyncStatusMessage = result.Error.Message;
    }
}
```

### After:
```csharp
private async Task HandleSyncAsync(CancellationToken ct)
{
    SyncStatusMessage = null;
    SyncProgressValue = 0;

    var result = await _syncHandler.HandleAsync(new SyncMailboxCommand(SyncParameters.Default), ct);

    if (result.IsSuccess)
    {
        await LoadPageAsync(ct);

        SyncProgressValue = 100;
        var r = result.Value;
        SyncStatusMessage = r.Errors.Count > 0
            ? $"Sync complete: {r.ReportsCreated} reports created, {r.Errors.Count} error(s)"
            : $"Sync complete: {r.ReportsCreated} reports created";
    }
    else
    {
        SyncStatusMessage = result.Error.Message;
    }
}
```

## Tests to Add

1. `SyncCommand_WhenSucceeds_RefreshesReportsList` — verify `_getReports.HandleAsync` is called after sync
2. `SyncCommand_WhenSucceeds_PreservesSortOrder` — verify query uses current `SortDescending` value
3. `SyncCommand_WhenSucceeds_PreservesCurrentPage` — verify query uses current `CurrentPage` value
4. `SyncCommand_WhenFails_DoesNotRefreshReportsList` — verify no re-query on sync failure
5. `SyncCommand_WhenSucceeds_NewRowsAppearInCollection` — verify Rows collection contains new data

## How to Verify

```bash
# Run the specific test class
dotnet test tests/Rentier.UnitTests --filter "ReportsViewModelTests"

# Run all tests
dotnet test
```
