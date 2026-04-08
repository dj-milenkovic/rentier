# Analysis: 015 One-Click Sync Workflow

**Generated**: 2025-07-14  
**Source files inspected**: spec.md, clarify.md, plan.md, data-model.md, contracts/application-contracts.md, tasks.md  
**Code files inspected**: SyncMailboxCommand.cs, SyncMailboxCommandHandler.cs, ProcessReportsCommand.cs, SyncProgress.cs, SyncResult.cs, ProcessReportsResult.cs, Result.cs, Error.cs, ICommandHandler.cs, MainWindowViewModel.cs, FilingsViewModel.cs, ReportsViewModel.cs, NavigationEntry.cs, CompositionRoot.cs, App.axaml.cs, App.axaml, ViewLocator.cs, ReportsView.axaml(.cs), InvertBoolConverter.cs

---

## Verified Type Signatures

### 1. `SyncMailboxCommand` — CONFIRMED
```csharp
// src/Rentier.Application/Commands/SyncMailboxCommand.cs
public sealed record SyncMailboxCommand(IProgress<SyncProgress>? Progress = null);
```
- **Single parameter `Progress`**, nullable, defaults to `null`.
- Named parameter — pass as `new SyncMailboxCommand(Progress: internalProgress)` **or** positional `new SyncMailboxCommand(internalProgress)` (both compile).
- `SyncMailboxCommandHandler.HandleAsync` passes `command.Progress` directly to `_syncService.SyncAsync(mailbox, importers, command.Progress, ct)`.
- **Do NOT attempt to pass progress as a method argument** — `HandleAsync` signature is `(SyncMailboxCommand command, CancellationToken ct = default)`.

### 2. `SyncResult` — CONFIRMED
```csharp
// src/Rentier.Application/DTOs/SyncResult.cs
public sealed record SyncResult(int ReportsCreated, IReadOnlyList<string> Errors);
```
- **Two fields only**: `ReportsCreated` and `Errors`.
- `ReportsCreated` = total count of attachments downloaded and stored as Report entities across all mailboxes (accumulated inside `SyncMailboxCommandHandler` via `totalCreated += result.Value.ReportsCreated`).
- **This maps to `SyncAllResult.AttachmentsDownloaded`** — the naming mismatch is intentional and documented.
- There is **no `MailboxesSynced` field** in `SyncResult`. The handler aggregates it internally as `byMailbox.Count`, but that count is not returned. `SyncAllCommandHandler` must approximate `MailboxesSynced` as `1` on success / `0` on failure.

### 3. `ProcessReportsResult` — CONFIRMED
```csharp
// src/Rentier.Application/DTOs/ProcessReportsResult.cs
public sealed record ProcessReportsResult(
    int FilingsCreated,
    int ReportsProcessed,
    int ReportsErrored,
    IReadOnlyList<string> Errors);
```
- ⚠️ **Constructor order**: `FilingsCreated` is **first**, `ReportsProcessed` is **second**.
- `ReportsErrored` contains the error count; individual messages are in `Errors`.
- All four fields must be read; `ReportsErrored` is informational only (not surfaced in `SyncAllResult` as a count — its messages go into `SyncAllResult.Errors`).

### 4. `SyncProgress` — CONFIRMED
```csharp
// src/Rentier.Application/DTOs/SyncProgress.cs
public sealed record SyncProgress(int Total, int Processed, string? CurrentFile, bool IsComplete);
```
- `CurrentFile` is nullable — use null-coalescing fallback: `p.CurrentFile ?? $"Processing {p.Processed}/{p.Total}"`.
- `IsComplete` flag is present but not used by the adapter pattern (the final `Progress<SyncProgress>` call from the sync service fires it when done — the adapter converts it to an Info entry either way).

### 5. `Result<TValue, TError>` — CONFIRMED
```csharp
public sealed class Result<TValue, TError>
{
    public bool IsSuccess { get; }
    public TValue Value => IsSuccess ? _value! : throw new InvalidOperationException("Result is failure.");
    public TError Error => !IsSuccess ? _error! : throw new InvalidOperationException("Result is success.");
    public static Result<TValue, TError> Success(TValue value) => new(value);
    public static Result<TValue, TError> Failure(TError error) => new(error);
}
```
- Accessing `.Value` on a failed result **throws** — always check `IsSuccess` first.
- Accessing `.Error` on a success result **throws** — same guard required.

### 6. `Error` — CONFIRMED
```csharp
public sealed record Error(string Code, string Message)
{
    public static Error Domain(string message) => new("DOMAIN_ERROR", message);
    public static Error NotFound(string message) => new("NOT_FOUND", message);
    public static Error Infrastructure(string message) => new("INFRASTRUCTURE_ERROR", message);
}
```
- Use `Error.Infrastructure(message)` for unexpected exceptions in `SyncAllCommandHandler`.

### 7. `ICommandHandler<TCommand, TResult>` — CONFIRMED
```csharp
public interface ICommandHandler<TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}
```
- **`ISyncAllCommandHandler` must NOT implement this interface** — signature cannot carry `IProgress<T>`.

### 8. `NavigationEntry` — CONFIRMED
```csharp
// src/Rentier.Desktop/ViewModels/NavigationEntry.cs
public record NavigationEntry(string Label, ReactiveObject ViewModel);
```
- `record`, not `class`.
- Created as `new(Strings.Nav_Sync, syncVm)`.

### 9. `MainWindowViewModel.NavigationEntries` — CONFIRMED
```csharp
public IReadOnlyList<NavigationEntry> NavigationEntries { get; }
```
- Backed by `new List<NavigationEntry>` created once in the constructor.
- Current order: `[Filings(0), Reports(1), Settings(2)]`.
- After this feature: `[Filings(0), Reports(1), Sync(2), Settings(3)]`.
- Default `_selectedEntry = NavigationEntries[0]` (Filings) — **index 0 remains correct after insertion**.

---

## Exact Code Patterns to Reuse

### Pattern A — `ActivatorUtilities.CreateInstance` (from MainWindowViewModel)
```csharp
// EXISTING (for ReportsViewModel — navigateToFilings is Action<Guid>):
Action<Guid> navigateToFilings = reportId =>
{
    filingsVm.ReportIdFilter = reportId;
    var filingsEntry = NavigationEntries?.FirstOrDefault(e => e.ViewModel is FilingsViewModel);
    if (filingsEntry is not null)
        SelectedEntry = filingsEntry;
};
var reportsVm = ActivatorUtilities.CreateInstance<ReportsViewModel>(
    provider, navigateToFilings);

// NEW (for SyncViewModel — navigateToFilings_sync is Action, no Guid):
Action navigateToFilings_sync = () =>
{
    var filingsEntry = NavigationEntries?.FirstOrDefault(e => e.ViewModel is FilingsViewModel);
    if (filingsEntry is not null)
        SelectedEntry = filingsEntry;
};
var syncVm = ActivatorUtilities.CreateInstance<SyncViewModel>(
    provider, navigateToFilings_sync);
```
> ⚠️ Note the `?.` null-conditional on `NavigationEntries`. This is required because the
> delegate is **defined before** `NavigationEntries` is assigned. The closure captures
> `this` (MainWindowViewModel), not the list value.

### Pattern B — DI handler registration (from CompositionRoot)
```csharp
// Existing pattern to copy:
services.AddTransient<
    ICommandHandler<SaveTaxpayerProfileCommand, Result<VoidResult, Error>>,
    SaveTaxpayerProfileCommandHandler>();

// New registration to add (simpler — non-generic interface):
services.AddTransient<ISyncAllCommandHandler, SyncAllCommandHandler>();
```

### Pattern C — `ReactiveCommand.CreateFromTask` with `IsExecuting` (from ReportsViewModel)
```csharp
SyncCommand = ReactiveCommand.CreateFromTask(
    HandleSyncAsync, outputScheduler: _scheduler);

SyncCommand.IsExecuting.Subscribe(v => IsSyncing = v);
```
For `SyncViewModel`, replace `HandleSyncAsync` with `RunSyncAsync` and `IsSyncing` with `IsRunning`.

### Pattern D — `WhenActivated` with disposables (from ReportsViewModel)
```csharp
this.WhenActivated(disposables =>
{
    LoadReportsCommand.Execute().Subscribe().DisposeWith(disposables);
});
```
For `SyncViewModel` (no auto-load command, but wire ThrownExceptions):
```csharp
this.WhenActivated(disposables =>
{
    SyncCommand.ThrownExceptions
        .Subscribe(_ => { /* swallow — errors surfaced via SummaryMessage */ })
        .DisposeWith(disposables);
});
```

### Pattern E — `IActivatableViewModel` declaration (from FilingsViewModel / ReportsViewModel)
```csharp
public sealed class SyncViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();
    // ...
}
```

### Pattern F — `ReactiveCommand.Create` with `canExecute` (pattern for CancelCommand)
```csharp
// CancelCommand: enabled only while running
CancelCommand = ReactiveCommand.Create(
    () => _cts?.Cancel(),
    this.WhenAnyValue(x => x.IsRunning),
    outputScheduler: _scheduler);
```

### Pattern G — Progress adapter inside SyncAllCommandHandler
```csharp
// Bridge SyncProgress → SyncProgressEntry
var internalProgress = new Progress<SyncProgress>(p =>
{
    var entry = new SyncProgressEntry(
        DateTimeOffset.Now,
        p.CurrentFile ?? $"Processing {p.Processed}/{p.Total}",
        SyncProgressSeverity.Info);
    progress.Report(entry);
});

var syncResult = await _syncMailboxHandler.HandleAsync(
    new SyncMailboxCommand(internalProgress), ct);
```

### Pattern H — Existing converter style (from InvertBoolConverter)
```csharp
// src/Rentier.Desktop/Converters/InvertBoolConverter.cs  ← already exists
public static class InvertBoolConverter
{
    public static readonly IValueConverter Instance =
        new FuncValueConverter<bool, bool>(b => !b);
}

// New IsZeroConverter to create (T020):
public static class IsZeroConverter
{
    public static readonly IValueConverter Instance =
        new FuncValueConverter<int, bool>(n => n == 0);
}
```
> `IsZeroConverter` does NOT currently exist in `src/Rentier.Desktop/Converters/`. T020 must create it.

### Pattern I — ViewLocator convention
```csharp
// src/Rentier.Desktop/ViewLocator.cs
var name = param.GetType().FullName!
    .Replace("ViewModels", "Views")
    .Replace("ViewModel", "View");
var type = Type.GetType(name);
```
- `SyncViewModel` → `SyncView` — **automatic via convention**.
- `ViewLocator` is already registered in `App.axaml` as `<local:ViewLocator/>` in `<Application.DataTemplates>`.
- **No `DataTemplate` entry needed in `App.axaml`** for `SyncViewModel` → `SyncView`.
- The `SyncView` class **must be** in namespace `Rentier.Desktop.Views` and named exactly `SyncView` for `Type.GetType(name)` to resolve.

### Pattern J — View code-behind (from ReportsView.axaml.cs)
```csharp
using Avalonia.ReactiveUI;
using Rentier.Desktop.ViewModels;

namespace Rentier.Desktop.Views;

public partial class SyncView : ReactiveUserControl<SyncViewModel>
{
    public SyncView()
    {
        InitializeComponent();
    }
}
```
With auto-scroll wired in addition:
```csharp
public SyncView()
{
    InitializeComponent();
    // wire ScrollViewer.ScrollChanged → scrollViewer.ScrollToEnd()
}
```

### Pattern K — AXAML root element for ReactiveUserControl (from ReportsView.axaml)
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Rentier.Desktop.ViewModels"
             xmlns:res="using:Rentier.Desktop.Resources"
             xmlns:local="using:Rentier.Desktop.Converters"
             x:Class="Rentier.Desktop.Views.SyncView"
             x:CompileBindings="False">
```
> ⚠️ `x:CompileBindings="False"` is **required** — every existing view uses this. Without it,
> `{Binding !IsRunning}` and `{x:Static StringConverters.IsNotNullOrEmpty}` will fail to compile.

---

## Gotchas & Non-Obvious Constraints

### G1 — `SyncResult.ReportsCreated` ≠ "reports processed" (FIELD NAME TRAP)
`SyncResult.ReportsCreated` means **attachments downloaded and stored as report entities**.  
It maps to `SyncAllResult.AttachmentsDownloaded`, NOT to `ReportsProcessed`.  
`ReportsProcessed` comes from `ProcessReportsResult.ReportsProcessed` (the second field, not the first).

### G2 — `ProcessReportsResult` constructor field order (POSITIONAL TRAP)
```csharp
// CORRECT field order — FilingsCreated is FIRST, ReportsProcessed is SECOND:
new ProcessReportsResult(FilingsCreated, ReportsProcessed, ReportsErrored, Errors)
```
If you write `new SyncAllResult(..., processResult.Value.ReportsProcessed, processResult.Value.FilingsCreated, ...)` 
without named args you will silently swap the counts. **Use named arguments** or be explicit in reading 
`processResult.Value.FilingsCreated` and `processResult.Value.ReportsProcessed` into distinct variables.

### G3 — `MailboxesSynced` cannot be read from `SyncResult` (FIELD ABSENT)
`SyncMailboxCommandHandler` accumulates `totalCreated` across mailbox groups but returns only a 
single `SyncResult(totalCreated, errors)`. The per-group count (`byMailbox.Count`) is internal and 
not surfaced. `SyncAllCommandHandler` MUST approximate: `mailboxesSynced = 1` on success, `0` on 
failure. **Do NOT modify `SyncResult.cs` to add a count field** — the spec and tasks explicitly 
prohibit modifying existing types.

### G4 — `navigateToFilings` delegate type differs per ViewModel
- **`ReportsViewModel` constructor**: `Action<Guid> navigateToFilings` (carries report ID)  
- **`SyncViewModel` constructor**: `Action navigateToFilings` (no parameter — no report ID filter)  
These are **different C# types**. Passing the `Action<Guid>` delegate from `MainWindowViewModel` to 
`SyncViewModel` will cause a compile error. Create a separate `Action navigateToFilings_sync` closure.

### G5 — `NavigationEntries` is null when delegate closures are defined
In `MainWindowViewModel`, the delegate closures are defined **before** `NavigationEntries = new List<...>`.  
The closures capture `this`, not the list. At runtime, the closures are called **after** construction 
completes. The `?.` null-conditional on `NavigationEntries?.FirstOrDefault(...)` is therefore **critical** 
and must be preserved — see Pattern A.

### G6 — `SyncViewModel` must NOT be registered in DI
`SyncViewModel` is constructed via `ActivatorUtilities.CreateInstance<SyncViewModel>(provider, navigateToFilings_sync)`.  
If you also add `services.AddTransient<SyncViewModel>()` in `CompositionRoot`, you will get a different 
(delegate-less) instance from DI rather than the hand-wired one. Do NOT register it.

### G7 — `ViewLocator` uses `Type.GetType(name)` — full namespace must match exactly
The `ViewLocator` resolves `Rentier.Desktop.ViewModels.SyncViewModel` →  
`Rentier.Desktop.Views.SyncView` at runtime via `Type.GetType()`.  
If `SyncView`'s namespace or class name is wrong, `ViewLocator.Build()` returns a `TextBlock { Text = "View not found: ..." }` with no compile error.

### G8 — `Progress<T>` SynchronizationContext capture
`new Progress<SyncProgressEntry>(callback)` **captures the current `SynchronizationContext`** at 
construction time. If created on the UI thread (inside `RunSyncAsync`, which runs on the UI thread 
because `outputScheduler: _scheduler`), callbacks are automatically marshalled back to the UI thread.  
**However**, for testability, still use `_scheduler.Schedule(() => LogEntries.Add(...))` inside the 
callback — this allows tests using `TestScheduler` to advance time and verify entries.

### G9 — `CancelCommand` canExecute must use `WhenAnyValue` not `IsRunning` directly
```csharp
// WRONG — canExecute evaluated once at construction (always false):
CancelCommand = ReactiveCommand.Create(() => _cts?.Cancel(), Observable.Return(false));

// CORRECT — reactive stream that tracks IsRunning over time:
CancelCommand = ReactiveCommand.Create(
    () => _cts?.Cancel(),
    this.WhenAnyValue(x => x.IsRunning),
    outputScheduler: _scheduler);
```

### G10 — `_cts` lifecycle: create per-run, dispose in finally
```csharp
private async Task RunSyncAsync(CancellationToken ct)
{
    _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    try
    {
        // ... use _cts.Token
    }
    catch (OperationCanceledException)
    {
        // add cancellation log entry
        SummaryMessage = "Sync cancelled by user";
    }
    finally
    {
        _cts?.Dispose();
        _cts = null;
    }
}
```
If `_cts` is not nulled out in `finally`, a subsequent sync run will call `_cts?.Cancel()` on a 
disposed token source (benign but confusing) and `IsRunning` may not correctly reflect state.

### G11 — `HasErrors` and `LogEntries` must be reset at start of each run
`SyncCommand` can be invoked multiple times per session. Before each `HandleAsync` call:
```csharp
LogEntries.Clear();
SummaryMessage = null;
HasErrors = false;
```
Failure to reset means entries from previous runs persist when the user clicks Sync again.

### G12 — `SyncCommand.ThrownExceptions` must be subscribed (swallow unhandled Rx exception)
ReactiveUI routes unhandled exceptions from `CreateFromTask` through `ThrownExceptions`. If no 
subscriber consumes it, the exception propagates to the global `RxApp.DefaultExceptionHandler` 
and may crash the app. Subscribe and swallow in `WhenActivated`.

### G13 — `x:CompileBindings="False"` is a project-wide pattern — do not change it
Every view (`ReportsView.axaml`, etc.) uses `x:CompileBindings="False"`. The binding expressions 
`{Binding !IsRunning}` (negation prefix) and `{x:Static StringConverters.IsNotNullOrEmpty}` only 
work with reflection-based bindings. Do not add `x:CompileBindings="True"` to `SyncView.axaml`.

### G14 — `IsZeroConverter` does not exist — T020 must create it
The Converters folder contains: `InvertBoolConverter`, `IncomeTypeDisplayConverter`, 
`FilingStatusDisplayConverter`, `ReportStatusDisplayConverter`, `ReportTypeDisplayConverter`.  
There is **no** `IsZeroConverter`. T020 must create it following the `InvertBoolConverter` pattern.

### G15 — `SyncAllCommandHandler` namespace must be `Rentier.Application.Handlers`
All existing handlers live in `src/Rentier.Application/Handlers/` with namespace 
`Rentier.Application.Handlers`. The `CompositionRoot` already imports 
`using Rentier.Application.Handlers;`. Use the same namespace.

---

## Task-by-Task Notes

### T001 — Create branch and verify baseline
No issues. `dotnet build Rentier.slnx` from repo root. Files to confirm absent:
`src/Rentier.Application/Commands/SyncAllCommand.cs`,  
`src/Rentier.Application/DTOs/SyncAllResult.cs`,  
`src/Rentier.Application/DTOs/SyncProgressEntry.cs`,  
`src/Rentier.Application/Interfaces/ISyncAllCommandHandler.cs`,  
`src/Rentier.Application/Handlers/SyncAllCommandHandler.cs`.

### T002 — Create `SyncProgressEntry` + `SyncProgressSeverity`
No issues. File: `src/Rentier.Application/DTOs/SyncProgressEntry.cs`. 
Both types in single file (no separate file for enum). Do NOT touch `SyncProgress.cs`.

### T003 — Create `SyncAllResult`
No issues. See Gotcha G1 for field mapping: `AttachmentsDownloaded` ← `SyncResult.ReportsCreated`.

### T004 — Create `SyncAllCommand`
No issues. Parameterless record: `public sealed record SyncAllCommand();`

### T005 — Create `ISyncAllCommandHandler`
Add the three using directives as shown in tasks.md. Interface must NOT inherit 
`ICommandHandler<TCmd, TResult>` — if it does, the `progress` parameter cannot be in `HandleAsync`.

### T006 — Create `SyncAllCommandHandler`
**Critical notes**:
- See Gotcha G2: read `processResult.Value.FilingsCreated` and `.ReportsProcessed` into named variables.
- See Gotcha G3: `mailboxesSynced = 1` on sync success, `0` on sync failure — do not try to read group count.
- See Gotcha G7 (Pattern G): adapter creates `new Progress<SyncProgress>(...)` inline.
- `errors` should be `var errors = new List<string>()` — accumulate from both steps.
- Even on sync failure (`!syncResult.IsSuccess`), **continue** to the `processReports` step.
- Final return: `Result<SyncAllResult, Error>.Success(new SyncAllResult(mailboxesSynced, attachmentsDownloaded, reportsProcessed, filingsCreated, errors.AsReadOnly()))`.
- The `HandleAsync` signature with `CancellationToken ct = default` — the `ct` must be passed to both inner handler calls.
- `OperationCanceledException` from inner handlers propagates to the caller (`SyncViewModel`) — do NOT catch it in `SyncAllCommandHandler`; let it bubble.

### T007 — `SyncAllCommandHandlerTests`
Test for `HandleAsync_PassesProgressViaCommandConstructor_NotAsMethodArg`:  
Use NSubstitute `Arg.Is<SyncMailboxCommand>(c => c.Progress != null)` to verify that the command 
received by `_syncMailboxHandler` has a non-null `Progress` property.

### T008 — Create `SyncViewModel`
- `SyncProgressEntryViewModel` does not exist yet when T008 is written. Add a forward-declaration 
  stub file or create T009 first (T009 has `[P]` so it can be done in parallel).
- `SyncCommand` must use `outputScheduler: _scheduler` for testability.
- `IScheduler? scheduler = null` parameter with `_scheduler = scheduler ?? RxApp.MainThreadScheduler` — matches existing VM pattern.
- See Gotcha G10 for `_cts` lifecycle pattern.
- See Gotcha G11 for reset-on-run.
- See Gotcha G8 for `Progress<SyncProgressEntry>` construction.

### T009 — Create `SyncProgressEntryViewModel`
No issues. Copy verbatim from tasks.md. Note the `_ => ("•", "Gray")` fallback case for the switch 
expression — required for exhaustiveness (compiler warning otherwise).

### T010 — Create `SyncView.axaml` and `SyncView.axaml.cs`
- Add `x:CompileBindings="False"` to root element (Gotcha G13).
- `x:Class="Rentier.Desktop.Views.SyncView"` must match namespace exactly (Gotcha G7).
- Auto-scroll: wire `ScrollViewer.ScrollChanged` in code-behind. Name the `ScrollViewer` with 
  `x:Name="LogScrollViewer"` and add `LogScrollViewer.ScrollToEnd()` in the handler.
- Empty-state visibility with `IsZeroConverter` for `LogEntries.Count` — but `IsZeroConverter` doesn't 
  exist yet (T020). Either forward-reference it or implement T020 before T010.
- `StringConverters.IsNotNullOrEmpty` is built-in to Avalonia — no custom converter needed.

### T011 — Verify `CancelCommand` wiring
No new file. This task validates that T008 was implemented correctly. Write the unit test for 
`_cts.IsCancellationRequested` assertion in T021's `SyncViewModelTests`.

### T012 — Add Cancel button to `SyncView.axaml`
Place the Cancel button **next to** the Sync button in the same button row.  
`IsVisible="{Binding IsRunning}"` for Cancel, `IsVisible="{Binding !IsRunning}"` for Sync.  
The `!` negation prefix works with `x:CompileBindings="False"`.

### T013 — Verify `HasErrors` property
No new file. Validate T008 implementation: `HasErrors = r.Errors.Count > 0` after `HandleAsync` 
returns. `HasErrors` must be reset to `false` at the start of `RunSyncAsync`.

### T014 — Add error summary section to `SyncView.axaml`
Recommendation: add a dedicated `ErrorEntries` property on `SyncViewModel` returning 
`LogEntries.Where(e => e.ForegroundColor == "Red")` or, better, a separate 
`ObservableCollection<SyncProgressEntryViewModel> ErrorEntries` populated alongside `LogEntries`.  
This simplifies AXAML binding (`IsVisible="{Binding HasErrors}"`) compared to filtered `ItemsControl`.

### T015 — Add string keys to `Strings.resx`
**Edit the `.resx` XML directly** — do not use Visual Studio designer (can corrupt file).  
Add `<data name="Nav_Sync" ...>`, etc. Check `Strings.Designer.cs` regenerates correctly 
by running `dotnet build` after editing. The designer file is auto-generated — do not hand-edit it.

### T016 — Register `ISyncAllCommandHandler` in `CompositionRoot`
Add `using Rentier.Application.Interfaces;` and `using Rentier.Application.Handlers;` if not present.  
Place the registration logically near other Application handler registrations (before ViewModel block).

### T017 — Update `MainWindowViewModel`
- Add `navigateToFilings_sync` closure (see Pattern A) after the existing `navigateToFilings` closure.
- Create `syncVm` using `ActivatorUtilities.CreateInstance<SyncViewModel>(provider, navigateToFilings_sync)`.
- Update `NavigationEntries` to 4 entries: `[filingsVm, reportsVm, syncVm, settingsVm]`.
- `_selectedEntry = NavigationEntries[0]` remains correct (index 0 = Filings).
- Add `using Rentier.Desktop.ViewModels;` if `SyncViewModel` is not already in scope.

### T018 — Wire `SyncView` via ViewLocator
**No action needed** for `App.axaml` — the `ViewLocator` is already the only `DataTemplate` entry 
and it handles all `ReactiveObject` types by naming convention.  
The only requirement: `SyncView` class is in namespace `Rentier.Desktop.Views` (Gotcha G7).  
Just creating the file with the correct namespace is sufficient.

### T019 — `SyncViewModelTests`
Use `new TestScheduler()` and pass it as the `scheduler` parameter to `SyncViewModel`.  
For tests involving `IsRunning`, use `TestScheduler.AdvanceByMs(1)` after `SyncCommand.Execute()`.  
NSubstitute stub for `ISyncAllCommandHandler`:  
```csharp
var handler = Substitute.For<ISyncAllCommandHandler>();
handler.HandleAsync(Arg.Any<SyncAllCommand>(), Arg.Any<IProgress<SyncProgressEntry>>(), Arg.Any<CancellationToken>())
       .Returns(Task.FromResult(Result<SyncAllResult, Error>.Success(new SyncAllResult(1, 1, 1, 1, []))));
```
For testing cancellation: use `TaskCompletionSource` in the stub to block until cancelled.

### T020 — Create or verify `IsZeroConverter`
**Must create** — does not exist. Follow the `InvertBoolConverter` pattern (see Pattern H).  
Register in AXAML as `xmlns:conv="using:Rentier.Desktop.Converters"` and 
`Converter="{x:Static conv:IsZeroConverter.Instance}"`.

### T021 — Build and test
Run `dotnet build Rentier.slnx` from repo root, then `dotnet test`. Fix any issues before marking done.

### T022 — Smoke-test
Manual. Record findings as a comment. Key checklist:  
Sidebar: Filings → Reports → Sync → Settings. Click Sync → `SyncView` with "Start Sync" button and  
empty-state text. Click "Start Sync" → progress bar visible, entries appear, Cancel button shown.  
Cancel → "Sync cancelled by user" in log, Sync button re-appears. Full sync → summary + auto-navigate  
to Filings only if `FilingsCreated > 0 && Errors.Count == 0`.

---

## Architecture Compliance Checklist

### I. Clean Architecture Dependency Rule ✅
- `SyncAllCommandHandler` is in `Rentier.Application.Handlers` — orchestrates Application-layer handlers only.
- `SyncViewModel` injects `ISyncAllCommandHandler` (Application interface) — no repository references.
- `SyncView` is in `Rentier.Desktop.Views` — no Application dependencies beyond ViewModel.
- No circular dependencies introduced.

### II. Local-First Security and Privacy ✅
- No new credentials, network calls, or data storage.
- `SyncProgressEntry` is ephemeral (in-memory, session-only).
- Progress log may show mailbox names and file names — already visible in the application.

### III. Financial and Temporal Correctness ✅
- No monetary values. `SyncAllResult` contains integer counts only.
- `SyncProgressEntry.Timestamp` uses `DateTimeOffset` (point-in-time logging) — `DateOnly` does not apply.
- No new domain rules, filing status transitions, or deadline calculations.

### IV. Async and UI Responsiveness ✅
- `SyncAllCommandHandler.HandleAsync` is fully `async Task<...>`.
- `SyncViewModel` uses `ReactiveCommand.CreateFromTask` with `outputScheduler: _scheduler`.
- `Progress<SyncProgressEntry>` marshals callbacks to UI thread via `SynchronizationContext`.
- `CancellationToken` threaded through all async calls.
- No `.Result` or `.Wait()` usage.

### V. Specification-Driven Quality Gates ✅
- Feature traceable to `015-one-click-sync-workflow` in `.specify/specs/`.
- Application test coverage target ≥ 90% via `SyncAllCommandHandlerTests` (9 test methods covering success, partial failure, cancellation, adaptor, progress entries).
- Desktop ViewModel coverage via `SyncViewModelTests` (12 test methods).
- No domain code added (0% domain coverage requirement N/A).
- Constitution check was performed in `plan.md` and passed all 5 gates.

**Additional coding standards checks:**
- All new ViewModels implement `sealed class`.
- `SyncProgressEntryViewModel` is also `sealed class`.
- DTOs use `sealed record`.
- User-visible strings go in `Strings.resx` (T015 covers all new strings).
- Views are `ReactiveUserControl<TViewModel>` with auto-scroll as the only permitted code-behind addition.
- `SyncView.axaml.cs` inherits `ReactiveUserControl<SyncViewModel>`.

---

## Known Facts Checklist

| # | Fact | Verified |
|---|------|----------|
| F01 | `SyncMailboxCommand(IProgress<SyncProgress>? Progress = null)` — progress via constructor, not method arg | ✅ |
| F02 | `SyncResult` has two fields: `ReportsCreated` (int) and `Errors` (IReadOnlyList<string>) | ✅ |
| F03 | `SyncResult.ReportsCreated` → `SyncAllResult.AttachmentsDownloaded` (intentional name mismatch) | ✅ |
| F04 | `ProcessReportsResult` constructor order: FilingsCreated FIRST, ReportsProcessed SECOND | ✅ |
| F05 | `SyncProgress` fields: `int Total, int Processed, string? CurrentFile, bool IsComplete` | ✅ |
| F06 | `Result<T,E>.Value` throws if IsSuccess==false; `Result<T,E>.Error` throws if IsSuccess==true | ✅ |
| F07 | `NavigationEntry` is a `record` with `(string Label, ReactiveObject ViewModel)` | ✅ |
| F08 | `MainWindowViewModel.NavigationEntries` is `IReadOnlyList<NavigationEntry>` backed by `List<T>` | ✅ |
| F09 | Current nav order: `[Filings(0), Reports(1), Settings(2)]` → new: `[Filings(0), Reports(1), Sync(2), Settings(3)]` | ✅ |
| F10 | `navigateToFilings` for `ReportsViewModel` is `Action<Guid>`; for `SyncViewModel` is `Action` (no Guid) | ✅ |
| F11 | `SyncViewModel` created via `ActivatorUtilities.CreateInstance<SyncViewModel>(provider, navigateToFilings_sync)` | ✅ |
| F12 | `SyncViewModel` must NOT be registered in DI (CompositionRoot) | ✅ |
| F13 | `ViewLocator` in `App.axaml` handles all `ReactiveObject` types by naming convention — no manual DataTemplate needed | ✅ |
| F14 | All views use `x:CompileBindings="False"` — required for `!` negation and `StringConverters` | ✅ |
| F15 | `IsZeroConverter` does not exist — must be created in T020 | ✅ |
| F16 | `SyncAllCommandHandler` namespace: `Rentier.Application.Handlers` | ✅ |
| F17 | `SyncAllCommandHandler` does NOT implement `ICommandHandler<TCmd,TResult>` | ✅ |
| F18 | `MailboxesSynced` is approximated as 1 (success) / 0 (failure) — not readable from `SyncResult` | ✅ |
| F19 | No EF Core migration required | ✅ |
| F20 | `SyncMailboxCommandHandler` iterates `byMailbox` groups; passes `command.Progress` to `_syncService.SyncAsync()` | ✅ |
