# Tasks: One-Click Sync Workflow (015)

**Branch**: `feature/015-one-click-sync-workflow` (off master)
**Input**: `.specify/specs/015-one-click-sync-workflow/` — spec.md, plan.md, data-model.md, contracts/application-contracts.md, clarify.md
**Total tasks**: 28 | **User stories**: 5 | **Phases**: 8

---

## Format: `[ID] [P?] [Story?] Description — file path`

- **[P]**: Parallelisable — different file, no dependency on incomplete tasks in the same phase
- **[US1–US5]**: Maps to user story from spec.md
- All file paths are relative to repository root

---

## Phase 1: Setup

**Purpose**: Branch creation and baseline verification.

- [ ] T001 Create branch `feature/015-one-click-sync-workflow` off master; run `dotnet build Rentier.slnx` from repo root to confirm clean baseline; confirm no existing `SyncAllCommand.cs`, `SyncAllResult.cs`, `SyncProgressEntry.cs`, `ISyncAllCommandHandler.cs`, or `SyncAllCommandHandler.cs` in `src/Rentier.Application/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Application-layer types — DTOs, command, interface, and handler — that all Desktop user stories depend on. No EF Core migration required (orchestration-only; no new schema).

**⚠️ CRITICAL**: No Desktop user story work (Phases 3–7) can begin until this phase is complete.

- [ ] T002 Create `SyncProgressEntry` record and `SyncProgressSeverity` enum in `src/Rentier.Application/DTOs/SyncProgressEntry.cs`; exact content:
  ```csharp
  namespace Rentier.Application.DTOs;
  public enum SyncProgressSeverity { Info, Warning, Error }
  public sealed record SyncProgressEntry(
      DateTimeOffset Timestamp,
      string Message,
      SyncProgressSeverity Severity);
  ```
  Do NOT modify existing `SyncProgress.cs` (that DTO is unchanged).

- [ ] T003 [P] Create `SyncAllResult` record in `src/Rentier.Application/DTOs/SyncAllResult.cs`; exact content:
  ```csharp
  namespace Rentier.Application.DTOs;
  public sealed record SyncAllResult(
      int MailboxesSynced,
      int AttachmentsDownloaded,
      int ReportsProcessed,
      int FilingsCreated,
      IReadOnlyList<string> Errors);
  ```
  `AttachmentsDownloaded` maps from `SyncResult.ReportsCreated`; `ReportsProcessed` and `FilingsCreated` map from `ProcessReportsResult`.

- [ ] T004 [P] Create `SyncAllCommand` parameterless record in `src/Rentier.Application/Commands/SyncAllCommand.cs`; exact content:
  ```csharp
  namespace Rentier.Application.Commands;
  public sealed record SyncAllCommand();
  ```

- [ ] T005 Create dedicated `ISyncAllCommandHandler` interface in `src/Rentier.Application/Interfaces/ISyncAllCommandHandler.cs`; must NOT inherit from `ICommandHandler<TCmd, TResult>` because the standard interface cannot carry `IProgress<SyncProgressEntry>`; exact content:
  ```csharp
  using Rentier.Application.Commands;
  using Rentier.Application.Common;
  using Rentier.Application.DTOs;
  namespace Rentier.Application.Interfaces;
  public interface ISyncAllCommandHandler
  {
      Task<Result<SyncAllResult, Error>> HandleAsync(
          SyncAllCommand command,
          IProgress<SyncProgressEntry> progress,
          CancellationToken ct = default);
  }
  ```
  (depends on T002, T003, T004)

- [ ] T006 Create `SyncAllCommandHandler` in `src/Rentier.Application/Handlers/SyncAllCommandHandler.cs` implementing `ISyncAllCommandHandler`; constructor injects `ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>` and `ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>`; `HandleAsync` must:
  - Phase 1 (mailbox sync): Report `"Starting mailbox sync..."` (Info); create `Progress<SyncProgress>` adapter that converts each `SyncProgress` event → `SyncProgressEntry(DateTimeOffset.Now, p.CurrentFile ?? $"Processing {p.Processed}/{p.Total}", Info)`; call `_syncMailboxHandler.HandleAsync(new SyncMailboxCommand(Progress: internalProgress), ct)` — progress goes in constructor, NOT as a method argument; on success: set `attachmentsDownloaded = syncResult.Value.ReportsCreated`, `mailboxesSynced = 1`, forward each error as Warning; report completion summary; on failure: add error to list, report Error entry; CONTINUE to Phase 2 regardless.
  - Phase 2 (report processing): Report `"Processing reports..."` (Info); call `_processReportsHandler.HandleAsync(new ProcessReportsCommand(), ct)`; on success: extract `ReportsProcessed` and `FilingsCreated`, forward errors as Warnings; report `"No new reports to process."` (Info) when `reportsProcessed == 0`, else report `"Processed {N} report(s), created {M} filing(s)"` (Info); on failure: add error, report Error entry.
  - Phase 3 (aggregation): Return `Result<SyncAllResult, Error>.Success(new SyncAllResult(mailboxesSynced, attachmentsDownloaded, reportsProcessed, filingsCreated, errors.AsReadOnly()))`. NOTE: `mailboxesSynced` is approximated as `1` (success) / `0` (failure) because `SyncResult` does not expose a mailbox-group count — do NOT modify `SyncResult.cs` to add a count.
  (depends on T005)

**Checkpoint**: Application DTOs, command, interface, and handler are compilable. Desktop user story implementation can begin.

---

## Phase 3: User Story 1 — Run Full Sync (Priority: P1) 🎯 MVP

**Goal**: Clicking the Sync button orchestrates mailbox sync + report processing in sequence, auto-navigates to Filings when `FilingsCreated > 0 && Errors.Count == 0`.

**Independent Test**: Use NSubstitute stubs for `ISyncAllCommandHandler`; verify `SyncCommand.Execute(Unit.Default)` triggers `HandleAsync`, `LogEntries` receives entries, `SummaryMessage` is set, and `_navigateToFilings` is invoked when result has FilingsCreated > 0 and no errors.

### Application Tests

- [ ] T007 [P] [US1] Write `SyncAllCommandHandlerTests` in `tests/Rentier.Application.Tests/SyncAllCommandHandlerTests.cs` using xUnit + NSubstitute + FluentAssertions; test methods:
  - `HandleAsync_WhenBothStepsSucceed_ReturnsSyncAllResultWithAggregatedCounts` — stub both sub-handlers returning success; assert `MailboxesSynced == 1`, `AttachmentsDownloaded == syncResult.ReportsCreated`, `ReportsProcessed`, `FilingsCreated`, `Errors.Count == 0`
  - `HandleAsync_WhenMailboxSyncFails_ContinuesToProcessReports` — stub sync handler returning `Failure`; verify process handler is still called; assert result is `Success` with the failure message in `Errors`
  - `HandleAsync_WhenProcessReportsFails_ReturnsSuccessWithErrorInErrors` — stub process handler returning `Failure`; assert result `IsSuccess == true`, error in `Errors`
  - `HandleAsync_ReportsProgressEntries_StartMailboxSync` — capture reported entries via `IProgress<SyncProgressEntry>` stub; assert first entry has `Message == "Starting mailbox sync..."` and `Severity == Info`
  - `HandleAsync_ReportsProgressEntries_ProcessingReports` — assert progress includes `"Processing reports..."` Info entry
  - `HandleAsync_AdaptsSyncProgressToSyncProgressEntry` — pass a `SyncProgress(1, 1, "file.csv", false)` from the mailbox handler's adapter; assert corresponding `SyncProgressEntry` with `Message == "file.csv"` is reported
  - `HandleAsync_WhenSyncHasNonFatalErrors_ReportsWarningEntries` — `SyncResult.Errors` has one item; assert Warning-severity `SyncProgressEntry` reported for it
  - `HandleAsync_WhenBothStepsHaveErrors_AggregatesAllErrors` — both handlers succeed with non-empty `.Errors`; assert `SyncAllResult.Errors.Count == syncErrors.Count + processErrors.Count`
  - `HandleAsync_PassesProgressViaCommandConstructor_NotAsMethodArg` — verify `_syncMailboxHandler` received a `SyncMailboxCommand` whose `.Progress` property is non-null
  (depends on T006)

### Desktop Core ViewModel

- [ ] T008 [US1] Create `SyncViewModel` in `src/Rentier.Desktop/ViewModels/SyncViewModel.cs` implementing `ReactiveObject, IActivatableViewModel`; exact shape:
  - `ViewModelActivator Activator { get; } = new()`
  - Fields: `ISyncAllCommandHandler _syncHandler`, `Action _navigateToFilings`, `IScheduler _scheduler`, `CancellationTokenSource? _cts`
  - `bool IsRunning` with `this.RaiseAndSetIfChanged` (no Fody)
  - `string? SummaryMessage` with `this.RaiseAndSetIfChanged`
  - `bool HasErrors` with `this.RaiseAndSetIfChanged`
  - `ObservableCollection<SyncProgressEntryViewModel> LogEntries { get; } = new()`
  - `ReactiveCommand<Unit, Unit> SyncCommand` — created with `ReactiveCommand.CreateFromTask(RunSyncAsync, outputScheduler: _scheduler)`
  - `ReactiveCommand<Unit, Unit> CancelCommand` — created with `ReactiveCommand.Create(() => _cts?.Cancel(), this.WhenAnyValue(x => x.IsRunning), outputScheduler: _scheduler)`
  - Constructor: `(ISyncAllCommandHandler syncHandler, Action navigateToFilings, IScheduler? scheduler = null)`; bind `SyncCommand.IsExecuting.Subscribe(v => IsRunning = v)`; use `WhenActivated(disposables => SyncCommand.ThrownExceptions.Subscribe(_ => {}).DisposeWith(disposables))`
  - `RunSyncAsync(CancellationToken ct)`: clear `LogEntries` + reset `SummaryMessage` + `HasErrors`; create `_cts = CancellationTokenSource.CreateLinkedTokenSource(ct)`; create `Progress<SyncProgressEntry>` on UI thread scheduling `LogEntries.Add(SyncProgressEntryViewModel.From(entry))` via `_scheduler.Schedule`; call `await _syncHandler.HandleAsync(new SyncAllCommand(), progress, _cts.Token)`; on success build SummaryMessage from counts, set `HasErrors = r.Errors.Count > 0`, auto-navigate if `r.FilingsCreated > 0 && r.Errors.Count == 0`; catch `OperationCanceledException`: add cancellation log entry, set `SummaryMessage = "Sync cancelled by user"`; finally: `_cts?.Dispose(); _cts = null`
  (depends on T005; `SyncProgressEntryViewModel` will be created in T010 — forward-declare or create stub to compile)

---

## Phase 4: User Story 2 — View Real-Time Progress (Priority: P1)

**Goal**: Each progress entry appears in the log with timestamp, severity icon (coloured), and message; log auto-scrolls to newest entry.

**Independent Test**: Stub handler that reports 3 entries (Info, Warning, Error); verify `LogEntries` has 3 items; verify Icon, Timestamp format (`HH:mm:ss`), and ForegroundColor values match severity.

### Display Model

- [ ] T009 [P] [US2] Create `SyncProgressEntryViewModel` in `src/Rentier.Desktop/ViewModels/SyncProgressEntryViewModel.cs`; exact content:
  ```csharp
  using Rentier.Application.DTOs;
  namespace Rentier.Desktop.ViewModels;
  public sealed class SyncProgressEntryViewModel
  {
      public string Icon { get; }
      public string Message { get; }
      public string Timestamp { get; }
      public string ForegroundColor { get; }
      private SyncProgressEntryViewModel(SyncProgressEntry entry)
      {
          Message = entry.Message;
          Timestamp = entry.Timestamp.ToString("HH:mm:ss");
          (Icon, ForegroundColor) = entry.Severity switch
          {
              SyncProgressSeverity.Info    => ("✓", "Green"),
              SyncProgressSeverity.Warning => ("⚠", "Orange"),
              SyncProgressSeverity.Error   => ("✗", "Red"),
              _ => ("•", "Gray")
          };
      }
      public static SyncProgressEntryViewModel From(SyncProgressEntry entry) => new(entry);
  }
  ```
  (depends on T002; resolves the forward-declaration dependency in T008)

### View

- [ ] T010 [US2] Create `SyncView.axaml` and `SyncView.axaml.cs` in `src/Rentier.Desktop/Views/`; view requirements:
  - Root: `<reactive:ReactiveUserControl x:TypeArguments="vm:SyncViewModel" x:CompileBindings="False">` (MUST have `x:CompileBindings="False"`)
  - Sync button: `Command="{Binding SyncCommand}"`, `IsVisible="{Binding !IsRunning}"`, `Content="{x:Static res:Strings.Sync_Button_Start}"`
  - `ProgressBar IsIndeterminate="True" IsVisible="{Binding IsRunning}" Height="4"`
  - `ScrollViewer` containing `ItemsControl ItemsSource="{Binding LogEntries}"` with `DataTemplate` rendering each entry as horizontal `StackPanel Spacing="8"` with: `TextBlock Text="{Binding Icon}" Foreground="{Binding ForegroundColor}"`, `TextBlock Text="{Binding Timestamp}" Foreground="Gray"`, `TextBlock Text="{Binding Message}"`
  - Auto-scroll: wire `ScrollViewer.ScrollChanged` in code-behind to call `scrollViewer.ScrollToEnd()` — this is the one permitted layout-concern code-behind addition
  - `TextBlock Text="{Binding SummaryMessage}" IsVisible="{Binding SummaryMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"` for completion/cancel summary
  - Empty-state `TextBlock Text="{x:Static res:Strings.Sync_Empty}"` visible only when `LogEntries.Count == 0` and `!IsRunning` (use converter or multi-binding)
  - Code-behind: `public partial class SyncView : ReactiveUserControl<SyncViewModel>` with `IViewFor<SyncViewModel>` wired via ReactiveUI
  (depends on T008, T009)

---

## Phase 5: User Story 3 — Cancel a Running Sync (Priority: P2)

**Goal**: Cancel button aborts the sync at the nearest cancellation checkpoint; log shows "Sync cancelled by user"; previously completed DB writes are retained.

**Independent Test**: Stub handler that blocks until cancellation; click Cancel; verify `OperationCanceledException` is caught, cancellation entry added to `LogEntries`, `SummaryMessage == "Sync cancelled by user"`, `IsRunning` returns to `false`.

- [ ] T011 [US3] Verify `CancelCommand` in `SyncViewModel` (`src/Rentier.Desktop/ViewModels/SyncViewModel.cs`) is wired with `canExecute = this.WhenAnyValue(x => x.IsRunning)` so it is enabled only while sync is running; verify `RunSyncAsync` catch block for `OperationCanceledException` adds a `SyncProgressEntry(DateTimeOffset.Now, "Sync cancelled by user", SyncProgressSeverity.Info)` via `_scheduler.Schedule` and sets `SummaryMessage = "Sync cancelled by user"`; verify `_cts?.Cancel()` is called in `CancelCommand` body; confirm via unit test that `_cts.IsCancellationRequested == true` after `CancelCommand.Execute(Unit.Default)` (no new file — edits to T008 output only if incomplete; otherwise write test verification in T021 desktop tests)

- [ ] T012 [US3] Extend `SyncView.axaml` in `src/Rentier.Desktop/Views/SyncView.axaml` to add Cancel button: `<Button Command="{Binding CancelCommand}" Content="{x:Static res:Strings.Sync_Button_Cancel}" IsVisible="{Binding IsRunning}" />` positioned next to the Sync button in the button row; confirm Sync button has `IsVisible="{Binding !IsRunning}"` so exactly one of the two buttons is visible at any time
  (depends on T010)

---

## Phase 6: User Story 4 — Review Error Summary (Priority: P2)

**Goal**: When errors occur, a dedicated error section appears after the progress log listing each error; successful operations are still summarised with counts; no auto-navigation when errors are present.

**Independent Test**: Stub handler returning `SyncAllResult` with `Errors = ["err1", "err2"]`; verify `HasErrors == true`, `SummaryMessage` contains count info, and error panel is visible in the view.

- [ ] T013 [US4] Verify `HasErrors` bool property in `SyncViewModel` (`src/Rentier.Desktop/ViewModels/SyncViewModel.cs`) is set to `r.Errors.Count > 0` in `RunSyncAsync` after `HandleAsync` returns success; verify `HasErrors` remains `false` after cancellation; confirm `HasErrors` is reset to `false` at the start of each `RunSyncAsync` invocation alongside `LogEntries.Clear()`
  (edits to T008 output; verify completeness)

- [ ] T014 [US4] Extend `SyncView.axaml` in `src/Rentier.Desktop/Views/SyncView.axaml` to add error summary section: `<ItemsControl ItemsSource="{Binding LogEntries}" IsVisible="{Binding HasErrors}">` filtered to Error-severity items — OR — add a dedicated `ErrorEntries` property on `SyncViewModel` that returns only Error-severity items for simpler binding; implement whichever approach keeps the AXAML binding simple; the section header should read "Errors:" and each item should show its `Message`; section must be `IsVisible="{Binding HasErrors}"` so it is hidden when there are no errors
  (depends on T012, T013)

---

## Phase 7: User Story 5 — Navigate to Sync Pane (Priority: P3)

**Goal**: "Sync" sidebar entry appears between "Reports" and "Settings"; clicking it shows the `SyncView` with empty log ready for first use; returning to the pane after a completed sync preserves the session-only log.

**Independent Test**: After MainWindowViewModel and CompositionRoot changes, launch app and verify sidebar order is Filings → Reports → Sync → Settings; click Sync → pane is visible; click away and back → log still visible.

- [ ] T015 [P] [US5] Add new string keys to `src/Rentier.Desktop/Resources/Strings.resx` (open file in text editor — do NOT use Visual Studio designer to avoid format corruption); add the following keys (check each does not already exist before adding):
  - `Nav_Sync` = `Sync`
  - `Sync_Button_Start` = `Start Sync`
  - `Sync_Button_Cancel` = `Cancel`
  - `Sync_Empty` = `Click "Start Sync" to synchronise mailboxes and process reports.`
  - `Sync_Progress_StartingMailboxSync` = `Starting mailbox sync...`
  - `Sync_Progress_ProcessingReports` = `Processing reports...`
  - `Sync_Progress_NoReports` = `No new reports to process.`
  - `Sync_Progress_Cancelled` = `Sync cancelled by user`
  - `Sync_Summary_Complete` = `Sync complete`
  - `Sync_Summary_Failed` = `Sync failed`

- [ ] T016 [US5] Register `ISyncAllCommandHandler` in `src/Rentier.Desktop/Composition/CompositionRoot.cs` inside `AddDesktopServices()`; add after existing handler registrations and before ViewModels block:
  ```csharp
  services.AddTransient<ISyncAllCommandHandler, SyncAllCommandHandler>();
  ```
  Add required `using` directives: `using Rentier.Application.Interfaces;` and `using Rentier.Application.Handlers;` if not already present. `SyncViewModel` is NOT registered in DI — it is constructed manually via `ActivatorUtilities.CreateInstance`.
  (depends on T006)

- [ ] T017 [US5] Update `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs` to insert Sync navigation entry; changes required:
  1. Add `navigateToFilings_sync` delegate (type `Action`, no Guid — SyncViewModel does not filter by report ID):
     ```csharp
     Action navigateToFilings_sync = () =>
     {
         var filingsEntry = NavigationEntries?.FirstOrDefault(e => e.ViewModel is FilingsViewModel);
         if (filingsEntry is not null) SelectedEntry = filingsEntry;
     };
     ```
  2. Create `SyncViewModel` using `ActivatorUtilities.CreateInstance<SyncViewModel>(provider, navigateToFilings_sync)` — same pattern as `reportsVm` creation above it
  3. Change `NavigationEntries` initialisation to: `[new(Strings.Nav_Filings, filingsVm), new(Strings.Nav_Reports, reportsVm), new(Strings.Nav_Sync, syncVm), new(Strings.Nav_Settings, settingsVm)]`
  4. Add `using Rentier.Desktop.ViewModels;` if `SyncViewModel` is not already in scope
  Note: `NavigationEntries` changes from 3 to 4 entries — existing index `[0]` for default selected entry (Filings) remains correct.
  (depends on T008, T015, T016)

- [ ] T018 [US5] Wire `SyncView` in the Avalonia view-locator or `DataTemplates` in `App.axaml` so `SyncViewModel` resolves to `SyncView`; check how `FilingsViewModel → FilingsView` and `ReportsViewModel → ReportsView` are wired in `App.axaml` or `ViewLocator.cs` and apply the same pattern for `SyncViewModel → SyncView`; also register `SyncView.axaml` in the `App.axaml` `<Application.DataTemplates>` block if that is the existing pattern
  (depends on T010, T017)

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Desktop ViewModel unit tests, build verification, and smoke-test validation.

- [ ] T019 [P] Write `SyncViewModelTests` in `tests/Rentier.Desktop.Tests/SyncViewModelTests.cs` using xUnit + NSubstitute + FluentAssertions + `new TestScheduler()` (RxUI test scheduler); test methods:
  - `SyncCommand_WhenExecuted_CallsHandlerHandleAsync` — stub returns success with empty result; verify `HandleAsync` called once
  - `SyncCommand_WhenExecuted_PopulatesLogEntries` — stub reports 2 entries; assert `LogEntries.Count == 2` after command completes
  - `SyncCommand_WhenStarted_ClearsLogEntriesFromPreviousRun` — pre-populate `LogEntries`; run again; assert old entries gone
  - `SyncCommand_WhenFilingsCreatedAndNoErrors_CallsNavigateToFilings` — stub returns `SyncAllResult(1, 1, 1, 1, [])` (FilingsCreated=1, Errors empty); assert `navigateToFilings` delegate invoked
  - `SyncCommand_WhenFilingsCreatedButErrorsPresent_DoesNotNavigate` — stub returns `SyncAllResult(1, 1, 1, 1, ["err"])` (FilingsCreated=1 but Errors non-empty); assert delegate NOT invoked
  - `SyncCommand_WhenZeroFilingsCreated_DoesNotNavigate` — stub returns `SyncAllResult(1, 0, 0, 0, [])` (FilingsCreated=0); assert delegate NOT invoked
  - `SyncCommand_SetsIsRunning_WhileExecuting` — assert `IsRunning == true` during execution, `false` after
  - `SyncCommand_SetsSummaryMessage_AfterCompletion` — assert `SummaryMessage` is non-null after handler returns
  - `SyncCommand_SetsHasErrors_WhenResultContainsErrors` — stub returns errors; assert `HasErrors == true`
  - `SyncCommand_SetsHasErrorsFalse_WhenResultHasNoErrors` — assert `HasErrors == false`
  - `CancelCommand_WhenExecuted_CancelsCts` — start `SyncCommand` with blocking stub; call `CancelCommand.Execute`; assert `OperationCanceledException` caught, cancellation log entry added, `SummaryMessage == "Sync cancelled by user"`
  - `CancelCommand_IsEnabledOnlyWhenRunning` — assert `CancelCommand.CanExecute` observable emits `true` during sync and `false` when idle
  (depends on T008, T009, T012, T013)

- [ ] T020 [P] Verify converters: search `src/Rentier.Desktop/Converters/` for an `IsZeroConverter` or equivalent that maps `int 0 → bool true`; if absent, create `IsZeroConverter` in `src/Rentier.Desktop/Converters/IsZeroConverter.cs` as `public static readonly IValueConverter Instance = new FuncValueConverter<int, bool>(n => n == 0)` and reference it in `SyncView.axaml` for the empty-state binding; `StringConverters.IsNotNullOrEmpty` is built-in to Avalonia and does NOT need to be created

- [ ] T021 Run `dotnet build Rentier.slnx` from repo root — resolve all compiler errors; then run `dotnet test` to confirm all existing and new unit tests pass (zero failures, zero skipped); fix any NSubstitute/FluentAssertions issues in test projects before marking done

- [ ] T022 Manual smoke-test (record findings in a comment on this task): launch `Rentier.Desktop`, verify sidebar order is Filings → Reports → Sync → Settings; click Sync → `SyncView` appears with "Start Sync" button and empty-state text; click "Start Sync" → progress entries appear in log, progress bar visible, Cancel button shown; click Cancel during run → log shows "Sync cancelled by user", Sync button re-appears; run a full sync to completion → `SummaryMessage` visible with counts; if filings created with no errors → auto-navigated to Filings pane

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — BLOCKS all Desktop phases
- **Phase 3 (US1)**: Depends on Phase 2; T007 (handler tests) and T008 (SyncViewModel) can start in parallel after T006
- **Phase 4 (US2)**: T009 depends on T002 only (parallelisable with Phase 3 Application work); T010 depends on T008, T009
- **Phase 5 (US3)**: Depends on T010 (view exists before extending it)
- **Phase 6 (US4)**: Depends on Phase 5 (T012 view has Cancel button before adding error panel)
- **Phase 7 (US5)**: T015 (strings) is parallelisable with Phase 3; T016 depends on T006; T017 depends on T008, T015, T016; T018 depends on T010, T017
- **Phase 8 (Polish)**: T019 (SyncViewModelTests) depends on T008, T009, T012, T013; T020–T022 depend on all phases complete

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational (Phase 2) — no dependencies on other stories
- **US2 (P1)**: T009 (SyncProgressEntryViewModel) parallelisable with US1; T010 (SyncView) depends on T008 and T009
- **US3 (P2)**: Depends on US1 (SyncViewModel exists) and US2 (SyncView exists)
- **US4 (P2)**: Depends on US3 (Cancel button in view before adding error panel)
- **US5 (P3)**: T015 (strings) parallelisable with US1; navigation wiring depends on SyncViewModel and SyncView being complete

### Critical Path

```
T001 → T002-T006 (sequential within phase 2) → T008 (SyncViewModel) → T010 (SyncView) → T012 (Cancel button) → T014 (Error panel) → T017 (MainWindow wiring) → T018 (view-locator) → T021 (build+test)
```

### Parallel Opportunities

**After T006 (handler complete)**:
- T007 `SyncAllCommandHandlerTests` in parallel with T008 `SyncViewModel` in parallel with T009 `SyncProgressEntryViewModel` in parallel with T015 `Strings.resx`

**After T008 + T009 complete**:
- T010 `SyncView` can proceed; T016 `CompositionRoot` can proceed

---

## Parallel Example: Phase 2 (Foundational)

```
Parallel:
  Task: T002 — SyncProgressEntry + SyncProgressSeverity in src/Rentier.Application/DTOs/SyncProgressEntry.cs
  Task: T003 — SyncAllResult in src/Rentier.Application/DTOs/SyncAllResult.cs
  Task: T004 — SyncAllCommand in src/Rentier.Application/Commands/SyncAllCommand.cs

Then sequential:
  Task: T005 — ISyncAllCommandHandler (depends on T002, T003, T004)
  Task: T006 — SyncAllCommandHandler (depends on T005)
```

## Parallel Example: Phase 3 start (after T006)

```
Parallel:
  Task: T007 — SyncAllCommandHandlerTests (Application tests)
  Task: T008 — SyncViewModel (Desktop)
  Task: T009 — SyncProgressEntryViewModel (Desktop)
  Task: T015 — Strings.resx keys (Desktop resources)
```

---

## Implementation Strategy

### MVP First (US1 + US2 + US5 only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3 US1: Handler tests + SyncViewModel core
4. Complete Phase 4 US2: SyncProgressEntryViewModel + SyncView with progress log
5. Complete Phase 7 US5: Strings + CompositionRoot + MainWindowViewModel wiring + view-locator
6. **STOP and VALIDATE**: Run app; verify sidebar entry, click Sync, see progress
7. Demo / smoke-test

### Full Delivery (all user stories)

1. MVP steps 1–6 above
2. Phase 5 US3: Cancel support
3. Phase 6 US4: Error summary panel
4. Phase 8: SyncViewModelTests + build verification + smoke-test

---

## Architecture Compliance Checklist

Before marking the branch ready for review, verify each item:

- [ ] No `using` of Infrastructure or EF Core types in `Rentier.Application` (orchestration only)
- [ ] `SyncViewModel` injects only `ISyncAllCommandHandler` (Application interface) — no direct repository or infrastructure references
- [ ] `x:CompileBindings="False"` is present on `SyncView.axaml`
- [ ] All properties use `this.RaiseAndSetIfChanged(ref _field, value)` — no Fody, no `[Reactive]` attribute
- [ ] `WhenActivated(disposables => ...)` with `.DisposeWith(disposables)` is present in `SyncViewModel`
- [ ] `ISyncAllCommandHandler` does NOT inherit from `ICommandHandler<TCmd, TResult>`
- [ ] `SyncMailboxCommand` is constructed with `Progress:` in the constructor — NOT passed as a `HandleAsync` argument
- [ ] `services.AddTransient<ISyncAllCommandHandler, SyncAllCommandHandler>()` — `AddTransient` only, no `AddSingleton`
- [ ] `SyncViewModel` is NOT registered in DI — constructed via `ActivatorUtilities.CreateInstance`
- [ ] No EF Core migration added (no new schema, orchestration-only feature)
- [ ] `SyncAllResult.Errors` is never null — populated with `errors.AsReadOnly()` (empty list when no errors)
