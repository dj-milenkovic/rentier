# Tasks: Velopack Auto-Update

**Input**: Design documents from `.specify/specs/041-velopack-auto-update/`  
**Branch**: `041-velopack-auto-update`  
**Generated**: 2025-07-14  
**Prerequisites**: plan.md ✅ spec.md ✅ research.md ✅ data-model.md ✅ contracts/ ✅ quickstart.md ✅

**Tests**: Included — explicitly required by plan.md CA-006 and constitution quality gates.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths are included in all descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Install Velopack NuGet packages into the two projects that require it.

- [X] T001 Add `Velopack` NuGet package reference to `src/Rentier.Infrastructure/Rentier.Infrastructure.csproj` (implements `IUpdateService`)
- [X] T002 [P] Add `Velopack` NuGet package reference to `src/Rentier.Desktop/Rentier.Desktop.csproj` (lifecycle bootstrap in `Program.cs` only)

**Checkpoint**: `dotnet build Rentier.slnx` succeeds with Velopack packages restored in both projects.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Application-layer contracts and DTOs that all user stories depend on. No user story work can begin until this phase is complete.

**⚠️ CRITICAL**: These files define the shared contracts consumed by every subsequent phase.

- [X] T003 Create `UpdateState` enum (`Idle`, `Checking`, `UpdateAvailable`, `Downloading`, `Downloaded`, `Error`, `Dismissed`) in `src/Rentier.Application/DTOs/UpdateState.cs`
- [X] T004 [P] Create `UpdateCheckResult` record (`bool IsUpdateAvailable`, `string? TargetVersion`) in `src/Rentier.Application/DTOs/UpdateCheckResult.cs`
- [X] T005 [P] Create `DownloadProgressInfo` record (`int ProgressPercent`) in `src/Rentier.Application/DTOs/DownloadProgressInfo.cs`
- [X] T006 Create `IUpdateService` interface (`IsInstalled`, `CheckForUpdatesAsync`, `DownloadUpdateAsync`, `ApplyUpdateAndRestart`, `ScheduleUpdateOnExit`) in `src/Rentier.Application/Interfaces/IUpdateService.cs` — use the full signature from `contracts/IUpdateService.md`

**Checkpoint**: All four Application-layer files compile. No Desktop or Infrastructure references introduced yet.

---

## Phase 3: User Story 1 — Background Update Check on App Start (Priority: P1) 🎯 MVP

**Goal**: On app launch, silently check for updates in the background. If a newer version is available, show a non-intrusive notification bar with the version number and [Update Now] / [Later] buttons. All network failures are silent.

**Independent Test**: Launch the app with a newer release on GitHub → notification bar appears within ~10 seconds showing the correct version. Click [Later] → bar disappears and does not reappear during the session. Launch with no newer release → no bar shown. Launch offline → no bar shown, app functions normally.

### Tests for User Story 1 ⚠️

> **Write these tests FIRST. Verify they FAIL before implementing.**

- [X] T007 [P] [US1] Write unit tests for `UpdateCheckResult` DTO (valid construction, `TargetVersion` is null when not available, equality) in `tests/Rentier.UnitTests/Application/UpdateCheckResultTests.cs`
- [X] T008 [P] [US1] Write `MainWindowViewModel` update tests for state machine transitions `Idle → Checking → UpdateAvailable → Dismissed` and that `CheckForUpdateCommand` is enabled in `Idle` only, and `DismissUpdateCommand` is enabled in `UpdateAvailable` only, in `tests/Rentier.UnitTests/Desktop/MainWindowViewModel_UpdateTests.cs`
- [X] T009 [P] [US1] Write infrastructure integration tests for `VelopackUpdateService.CheckForUpdatesAsync()` with a substituted `UpdateManager`: update available returns `UpdateCheckResult(true, version)`, no update returns `UpdateCheckResult(false, null)`, network exception returns `UpdateCheckResult(false, null)`, `IsInstalled = false` returns `UpdateCheckResult(false, null)` without calling the manager, in `tests/Rentier.Infrastructure.Tests/Updates/VelopackUpdateServiceTests.cs`

### Implementation for User Story 1

- [X] T010 [US1] Implement `VelopackUpdateService` with `IsInstalled` guard (returns false when unpackaged), `SemaphoreSlim(1,1)` for concurrency, `GithubSource` targeting the Rentier repository, and `CheckForUpdatesAsync()` that catches all exceptions and returns `UpdateCheckResult(false, null)` on failure, in `src/Rentier.Infrastructure/Updates/VelopackUpdateService.cs`
- [X] T011 [US1] Register `IUpdateService` as `Singleton<IUpdateService, VelopackUpdateService>` in `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs`
- [X] T012 [US1] Add update-related reactive properties (`CurrentUpdateState`, `AvailableVersion`, `UpdateBarVisible`) and `CheckForUpdateCommand` (fires background check via `ReactiveCommand.CreateFromTask`, transitions `Idle→Checking→UpdateAvailable/Idle`) and `DismissUpdateCommand` (transitions to `Dismissed`) to `MainWindowViewModel`, with auto-check triggered via `this.WhenActivated` in `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs`
- [X] T013 [P] [US1] Add `UpdateAvailable`-state string resources (`Update_Available_Message`, `Update_Now_Button`, `Update_Later_Button`) to `src/Rentier.Desktop/Resources/Strings.resx`
- [X] T014 [US1] Add the notification bar `Border` as `DockPanel.Dock="Top"` above the `ContentControl` in `src/Rentier.Desktop/Views/MainWindow.axaml` — bind `IsVisible` to `UpdateBarVisible`, show version text bound to `AvailableVersion` using `Update_Available_Message`, bind [Update Now] to `BeginUpdateCommand` (placeholder) and [Later] to `DismissUpdateCommand`; apply `RentierInfoBrush` background per UI design system

**Checkpoint**: US1 fully functional — all three acceptance scenarios testable. `UpdateCheckResultTests`, state machine tests for `Idle/Checking/UpdateAvailable/Dismissed`, and `CheckForUpdatesAsync` service tests all pass.

---

## Phase 4: User Story 2 — Download and Apply Update (Priority: P2)

**Goal**: When the user clicks [Update Now], download the update with progress feedback. Show a progress bar during download. On completion, prompt to restart with [Restart Now] / [Later]. [Restart Now] exits and relaunches; [Later] schedules apply on next exit. Network failures during download show an error with [Retry] / [Dismiss].

**Independent Test**: Click [Update Now] on the notification bar → progress bar appears and advances → restart prompt appears on completion → [Restart Now] relaunches with new version. Simulate network failure → error message with [Retry] shown. [Later] on restart prompt → update applied on next manual restart.

### Tests for User Story 2 ⚠️

> **Write these tests FIRST. Verify they FAIL before implementing.**

- [X] T015 [P] [US2] Extend `tests/Rentier.UnitTests/Desktop/MainWindowViewModel_UpdateTests.cs` with tests for: `UpdateAvailable → Downloading` (progress updates via `RxApp.MainThreadScheduler`), `Downloading → Downloaded`, `Downloading → Error` on exception, `Error → Downloading` via retry, `Downloaded → Idle` via dismiss-restart; verify `BeginUpdateCommand` only enabled in `UpdateAvailable`, `RestartNowCommand` only in `Downloaded`, `RetryDownloadCommand` only in `Error`
- [X] T016 [P] [US2] Extend `tests/Rentier.Infrastructure.Tests/Updates/VelopackUpdateServiceTests.cs` with tests for: `DownloadUpdateAsync` invokes `progress` callback at 0, 50, 100; throws on network failure; `ApplyUpdateAndRestart` calls `ApplyUpdatesAndRestart`; `ScheduleUpdateOnExit` calls `WaitExitThenApplyUpdates`

### Implementation for User Story 2

- [X] T017 [US2] Extend `src/Rentier.Infrastructure/Updates/VelopackUpdateService.cs` with `DownloadUpdateAsync()` (marshals `Action<int>` progress, acquires semaphore, calls `UpdateManager.DownloadUpdatesAsync`), `ApplyUpdateAndRestart()` (calls `ApplyUpdatesAndRestart`, no-op if not installed), and `ScheduleUpdateOnExit()` (calls `WaitExitThenApplyUpdates`, no-op if not installed); store `UpdateInfo` instance between check and download
- [X] T018 [US2] Extend `src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs` with `DownloadProgress` (OAPH), `UpdateErrorMessage` properties; implement `BeginUpdateCommand` (calls `DownloadUpdateAsync`, updates `DownloadProgress` via `RxApp.MainThreadScheduler`, transitions `Downloading→Downloaded` or `Downloading→Error`), `RestartNowCommand` (calls `ApplyUpdateAndRestart`), `DismissRestartCommand` (calls `ScheduleUpdateOnExit`, transitions to `Idle`), and `RetryDownloadCommand` (transitions `Error→Downloading`, retries download); all commands use `ReactiveCommand.CreateFromTask`
- [X] T019 [P] [US2] Add `Downloading`, `Downloaded`, and `Error` string resources (`Update_Downloading_Message`, `Update_Ready_Message`, `Update_Restart_Button`, `Update_Error_Message`, `Update_Retry_Button`, `Update_Dismiss_Button`) to `src/Rentier.Desktop/Resources/Strings.resx`
- [X] T020 [US2] Extend the notification bar in `src/Rentier.Desktop/Views/MainWindow.axaml` with conditional content panels (via `DataTrigger` or converter on `CurrentUpdateState`) for: Downloading state (progress text + `ProgressBar` bound to `DownloadProgress` with `AutomationProperties.Name`), Downloaded state ([Restart Now] → `RestartNowCommand`, [Later] → `DismissRestartCommand`), Error state ([Retry] → `RetryDownloadCommand`, [Dismiss] → `DismissUpdateCommand`, error text bound to `UpdateErrorMessage`); apply `RentierErrorBrush` for Error state; add `AutomationProperties.Name` to notification bar container

**Checkpoint**: US2 fully functional — all five download/apply acceptance scenarios testable. All new ViewModel and service tests pass.

---

## Phase 5: User Story 3 — Seamless Install/Uninstall Lifecycle Hooks (Priority: P3)

**Goal**: Velopack lifecycle hooks (install, update, uninstall) execute as the very first operation in `Main()`, before any Avalonia or DI initialization. This ensures clean installs, correct shortcut management, and full cleanup on uninstall — without any user interaction.

**Independent Test**: Perform a fresh install of the packaged app → app launches and shortcuts exist. Perform an update → new version runs. Perform an uninstall → files and shortcuts removed cleanly with no orphaned artifacts.

- [X] T021 [US3] Add `VelopackApp.Build().Run();` as the very first statement in `Program.Main(string[] args)` in `src/Rentier.Desktop/Program.cs`, before `BuildAvaloniaApp()` — per research.md R-002; must not add any other Velopack references in Desktop outside this single call

**Checkpoint**: US3 complete — all three lifecycle acceptance scenarios verifiable via packaging. Fresh install, update, and uninstall all complete without UI errors or orphaned artifacts.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final compliance verification, accessibility hardening, and build validation.

- [X] T022 [P] Verify architecture compliance checklist from `quickstart.md`: confirm `IUpdateService` is in `Rentier.Application.Interfaces` only, `VelopackUpdateService` is in `Rentier.Infrastructure.Updates` only, no `Velopack.*` namespace imported in any ViewModel or service outside Infrastructure/Program.cs, all update commands use `ReactiveCommand.CreateFromTask`, no `.Result` or `.Wait()` anywhere in update code
- [X] T023 [P] Verify all update-related strings are in `Strings.resx` (zero hardcoded UI text in AXAML or ViewModel), all progress updates use `RxApp.MainThreadScheduler`, and `SemaphoreSlim` is disposed in `VelopackUpdateService`
- [X] T024 Run `dotnet build Rentier.slnx` and `dotnet test` to confirm the solution compiles clean and all unit/integration tests pass before opening the pull request

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — T001 and T002 can start immediately and run in parallel
- **Foundational (Phase 2)**: Depends on Setup — T003–T006 block all user story work
  - T003 first (UpdateState enum) → T004, T005 can be parallel → T006 last (references T003–T005)
- **User Story Phases (Phase 3–5)**: All depend on Foundational completion
  - US1 (Phase 3) and US2 (Phase 4) are sequenced: US2 extends files created in US1
  - US3 (Phase 5) is independent of US1/US2 — can be done in parallel after Phase 2 if staffed
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: Depends on Phase 2 only — independent starting point
- **US2 (P2)**: Depends on US1 completion (extends same ViewModel, service, and AXAML files)
- **US3 (P3)**: Depends on Phase 2 only — single file change, independent of US1/US2

### Within Each User Story

1. Tests MUST be written first and MUST FAIL before implementation begins
2. DTOs / Application contracts → Infrastructure service → ViewModel → AXAML → String resources
3. Story must be independently verifiable before moving to next priority

### Parallel Opportunities

```
Phase 1:    T001 ║ T002
Phase 2:    T003 → T004 ║ T005 → T006
Phase 3:    T007 ║ T008 ║ T009 → T010 → T011 → T012 → T013 ║ T014
            (US3 T021 can run here in parallel if separate developer)
Phase 4:    T015 ║ T016 → T017 → T018 → T019 ║ T020
Phase 6:    T022 ║ T023 → T024
```

---

## Parallel Example: User Story 1

```
# Write all US1 tests in parallel (different files):
Task T007: UpdateCheckResultTests.cs (Application DTO tests)
Task T008: MainWindowViewModel_UpdateTests.cs (state machine tests)
Task T009: VelopackUpdateServiceTests.cs (check behavior tests)

# Once tests fail as expected, implement in sequence:
T010 → T011 → T012 → T013 (parallel with T012) → T014
```

## Parallel Example: User Story 2

```
# Extend both test files in parallel:
Task T015: MainWindowViewModel_UpdateTests.cs (download/error states)
Task T016: VelopackUpdateServiceTests.cs (download/apply/schedule)

# Once tests fail, implement in sequence:
T017 → T018 → T019 (parallel with T018) → T020
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (install Velopack)
2. Complete Phase 2: Foundational (Application DTOs + IUpdateService)
3. Complete Phase 3: User Story 1 (background check + notification bar)
4. **STOP and VALIDATE**: Launch app with a newer GitHub release → bar appears; launch offline → silent
5. Merge as an MVP — users see update notifications immediately

### Incremental Delivery

1. Setup + Foundational → contracts in place
2. US1 complete → users are notified of updates (MVP)
3. US2 complete → users can download and apply updates in-app
4. US3 complete → install/uninstall lifecycle is correct for packaged distributions
5. Each increment is independently testable and deployable

### Single Developer Sequence

With one developer, the recommended order is:

```
Phase 1 → Phase 2 → Phase 3 (US1) → Phase 5 (US3) → Phase 4 (US2) → Phase 6
```

US3 is a single-line change in `Program.cs` — best done early to avoid forgetting it.

---

## Notes

- `[P]` tasks operate on different files with no dependency on incomplete tasks in their phase
- Each user story produces an independently verifiable increment — do not merge partial stories
- Velopack NuGet is referenced in **both** Desktop (lifecycle bootstrap) and Infrastructure (service) — this is documented in plan.md Complexity Tracking and is intentional
- `IsInstalled` guard means zero GitHub API calls during IDE development — safe to run without packaging
- All ViewModel commands must use `ReactiveCommand.CreateFromTask` — never `async void`
- All progress callbacks from Velopack arrive on background threads — always marshal via `RxApp.MainThreadScheduler`
- Constitution amendment (adding `api.github.com` to approved endpoints) is documented in research.md R-007 — update constitution after this feature merges

