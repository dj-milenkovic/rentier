# Quickstart: Manual Filing Creation (034)

**Feature**: 034-manual-filing-creation
**Branch**: `feature/032-033-034-column-xml-manual`

---

## Prerequisites

- .NET 8 SDK installed
- Feature branches 006 (NBS Exchange Rate), 008 (Tax Calculation), 009 (Filing Deadline),
  012 (Filings List) already merged
- Taxpayer profile configured in the app (Settings → Profile)

## Build & Run

```bash
cd F:\Projects\Rentier\rentier
dotnet build Rentier.slnx
dotnet run --project src/Rentier.Desktop
```

## Files to Create

### Application Layer (`src/Rentier.Application/`)

| File | Purpose |
|------|---------|
| `Commands/CalculateManualFilingCommand.cs` | Command record for calculate-only step |
| `Commands/CreateManualFilingCommand.cs` | Command record for persist step |
| `DTOs/ManualFilingPreviewDto.cs` | Preview result DTO |
| `Handlers/CalculateManualFilingCommandHandler.cs` | Validates, resolves rate, computes tax+deadline, returns preview |
| `Handlers/CreateManualFilingCommandHandler.cs` | Validates, resolves rate, computes tax+deadline, checks duplicates, persists filing |

### Desktop Layer (`src/Rentier.Desktop/`)

| File | Purpose |
|------|---------|
| `ViewModels/ManualFilingViewModel.cs` | Form ViewModel with Calculate/Save/Cancel commands |
| `Views/ManualFilingView.axaml` | Avalonia form layout |
| `Views/ManualFilingView.axaml.cs` | Code-behind (minimal, ReactiveUserControl) |
| `Resources/Strings.resx` | Add ManualFiling_* keys (see ui-contract.md) |
| `Composition/CompositionRoot.cs` | Register new ViewModel + handlers |

### Files to Modify

| File | Change |
|------|--------|
| `Desktop/ViewModels/MainWindowViewModel.cs` | Wire ManualFilingViewModel navigation delegate |
| `Desktop/ViewModels/FilingsViewModel.cs` | Add "New Filing" command that triggers navigation |
| `Desktop/Views/FilingsView.axaml` | Add "New Filing" toolbar button |
| `Infrastructure/InfrastructureServiceExtensions.cs` | Register new command handlers (if handler uses infra) |

## Testing

### Application Unit Tests (`tests/Rentier.Application.Tests/`)

| Test Class | Covers |
|------------|--------|
| `CalculateManualFilingCommandHandlerTests` | Happy path (with/without WHT), validation failures, rate fetch failure, deadline computation |
| `CreateManualFilingCommandHandlerTests` | Happy path, duplicate detection, all validation error paths |

### Desktop ViewModel Tests (`tests/Rentier.Desktop.Tests/`)

| Test Class | Covers |
|------------|--------|
| `ManualFilingViewModelTests` | Command enablement, preview state management, error display, navigation |

## Key Patterns to Follow

1. **Command Handler**: See `UpdateFilingStatusCommandHandler` for the canonical pattern
   (`ICommandHandler<TCommand, Result<T, Error>>`, constructor injection, `HandleAsync`).
2. **Form ViewModel**: See `ProfileSettingsViewModel` for `WhenAnyValue` canExecute guards,
   `IsLoading` pattern, and `WhenActivated` for initial data loading.
3. **Navigation**: See `MainWindowViewModel` for delegate-based navigation wiring.
4. **Tax Calculation**: See `ProcessReportsCommandHandler` lines 157–174 for the
   rate-resolve → tax-calculate → deadline-compute → duplicate-check → create-filing flow.
5. **String Resources**: All user-visible strings in `Resources/Strings.resx`.
6. **Result Pattern**: All handlers return `Result<T, Error>`, never throw for expected failures.

## Architecture Rules Reminder

- Handler MUST be in `Rentier.Application` — depends only on Domain + Application interfaces
- ViewModel MUST be in `Rentier.Desktop` — calls Application handlers only, never repositories
- All monetary values MUST be `decimal` — no float/double
- All dates MUST be `DateOnly` — convert DateTimeOffset from DatePicker at ViewModel boundary
- All I/O MUST be `async` — use `ReactiveCommand.CreateFromTask`
- User-visible strings MUST be in `Strings.resx`
