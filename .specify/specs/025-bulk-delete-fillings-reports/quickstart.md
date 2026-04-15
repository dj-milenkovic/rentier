# Quickstart: Bulk Delete for Filings and Reports

**Feature**: 025-bulk-delete-fillings-reports  
**Branch**: `025-bulk-delete-fillings-reports`

## Prerequisites

- .NET 8 SDK installed
- Feature branch checked out: `git checkout 025-bulk-delete-fillings-reports`
- Solution builds clean: `dotnet build Rentier.slnx`

## Implementation Order

The recommended implementation order follows dependency flow (inner layers first):

### Step 1: Repository Interface + Implementation (Infrastructure)

1. Add `DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct)` to `IFilingRepository`
2. Add `DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct)` to `IReportRepository`
3. Implement in `FilingRepository` using `Where(e => ids.Contains(e.Id))` + `RemoveRange` + `SaveChangesAsync`
4. Implement in `ReportRepository` using the same pattern

**Verify**: `dotnet build src/Rentier.Infrastructure`

### Step 2: CQRS Commands + Handlers (Application)

1. Create `BulkDeleteFilingsCommand.cs` in `Commands/`
2. Create `BulkDeleteReportsCommand.cs` in `Commands/`
3. Create `BulkDeleteFilingsCommandHandler.cs` in `Handlers/` — validate input, call `DeleteManyAsync`
4. Create `BulkDeleteReportsCommandHandler.cs` in `Handlers/` — validate input, loop `DeleteByReportIdAsync` per report, then `DeleteManyAsync`

**Verify**: `dotnet build src/Rentier.Application`

### Step 3: Application Tests

1. Create `BulkDeleteFilingsCommandHandlerTests.cs` — test: empty list rejected, valid IDs deleted, handler returns success
2. Create `BulkDeleteReportsCommandHandlerTests.cs` — test: empty list rejected, cascade filings deleted first, handler returns success, exception wrapped in error

**Verify**: `dotnet test tests/Rentier.Application.Tests`

### Step 4: Row ViewModel Changes (Desktop)

1. Add `IsSelected` property to `FilingRowViewModel` (ReactiveUI `RaiseAndSetIfChanged`)
2. Add `IsSelected` property to `ReportRowViewModel` (same pattern)

### Step 5: Parent ViewModel Changes (Desktop)

1. Add `SelectedCount`, `HasSelection`, `DeleteSelectedLabel` observable properties to `FilingsViewModel`
2. Add `SelectAllCommand`, `ClearSelectionCommand`, `BulkDeleteCommand` to `FilingsViewModel`
3. Repeat for `ReportsViewModel`
4. Wire selection subscription: when any row's `IsSelected` changes, recalculate `SelectedCount`

### Step 6: Strings.resx

Add all `BulkDelete_*` string resources (see data-model.md for full list).

### Step 7: View Changes (AXAML)

1. Add checkbox template column as first column in both DataGrids
2. Add "Select All", "Clear Selection", "Delete Selected (N)" buttons to toolbar area
3. Bind visibility and content to ViewModel properties

### Step 8: DI Registration

Register `BulkDeleteFilingsCommandHandler` and `BulkDeleteReportsCommandHandler` in `CompositionRoot.cs`.

### Step 9: Desktop Tests

1. Create `FilingsViewModelBulkDeleteTests.cs` — test: selection state, toolbar reactivity, command flow
2. Create `ReportsViewModelBulkDeleteTests.cs` — test: selection state, cascade warning, command flow

**Verify**: `dotnet test tests/Rentier.Desktop.Tests`

## Build & Test Commands

```bash
# Full build
dotnet build Rentier.slnx

# Run all tests
dotnet test Rentier.slnx

# Run specific test projects
dotnet test tests/Rentier.Application.Tests
dotnet test tests/Rentier.Desktop.Tests

# Run with coverage (if configured)
dotnet test Rentier.slnx --collect:"XPlat Code Coverage"
```

## Key Files to Reference

| File | Purpose |
|------|---------|
| `src/Rentier.Application/Handlers/DeleteFilingCommandHandler.cs` | Pattern for single-delete handler |
| `src/Rentier.Application/Handlers/DeleteReportCommandHandler.cs` | Pattern for cascade-delete handler |
| `src/Rentier.Infrastructure/Repositories/FilingRepository.cs` | Pattern for `RemoveRange` batch delete (`DeleteByReportIdAsync`) |
| `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` | Existing delete command, pagination, toolbar patterns |
| `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs` | Existing delete command, confirmation delegate patterns |
| `src/Rentier.Desktop/Dialogs/ConfirmDialogHelper.cs` | Reusable confirmation dialog |
| `src/Rentier.Desktop/Composition/CompositionRoot.cs` | DI registration pattern |
| `src/Rentier.Desktop/Resources/Strings.resx` | Localisation resource file |
