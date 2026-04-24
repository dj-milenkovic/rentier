# Research: Velopack Auto-Update

**Feature**: 041-velopack-auto-update  
**Date**: 2025-07-14

## R-001: Velopack Framework Selection and API Surface

**Decision**: Use the `Velopack` NuGet package with `GithubSource` for update distribution.

**Rationale**: Velopack is MIT-licensed, cross-platform (.NET 8+, Windows/macOS/Linux), and provides a complete auto-update lifecycle: check → download (with progress) → apply → restart. Its `GithubSource` natively targets GitHub Releases, which aligns with Rentier's existing CI/CD on GitHub Actions. The API is fully async, compatible with Avalonia and ReactiveUI patterns.

**Alternatives Considered**:
- **Squirrel.Windows**: Windows-only, maintenance mode, no cross-platform support. Rejected.
- **AutoUpdater.NET**: Simpler API but less control over lifecycle hooks, no built-in GitHub integration for cross-platform. Rejected.
- **Manual HTTP polling**: Maximum flexibility but requires implementing delta updates, verification, and restart logic from scratch. Unjustified complexity. Rejected.

**Key API Surface**:
| Method | Return | Purpose |
|---|---|---|
| `VelopackApp.Build().Run()` | void | Lifecycle hooks — must be first call in `Main()` |
| `UpdateManager.IsInstalled` | bool | Guard for dev/debug mode |
| `UpdateManager.CheckForUpdatesAsync()` | `UpdateInfo?` | null = no update; non-null = new version available |
| `UpdateManager.DownloadUpdatesAsync(info, progress, cancel)` | Task | Downloads with `Action<int>` progress (0-100) |
| `UpdateManager.ApplyUpdatesAndRestart(release)` | void (exits) | Applies update and restarts process |
| `UpdateManager.WaitExitThenApplyUpdates(release)` | void | Schedules apply on app exit |

## R-002: Lifecycle Hooks Integration with Avalonia

**Decision**: Call `VelopackApp.Build().Run()` as the very first line in `Program.Main()`, before `BuildAvaloniaApp()`.

**Rationale**: Velopack lifecycle hooks (install, update, uninstall) must execute before any UI framework initialization. Velopack intercepts command-line arguments passed by the installer/updater and may exit the process immediately (e.g., during uninstall). Placing the call after Avalonia initialization would cause UI framework errors during lifecycle events.

**Alternatives Considered**:
- **Inside `App.Initialize()`**: Too late — Avalonia is already bootstrapped. Rejected.
- **Inside `App.OnFrameworkInitializationCompleted()`**: Even later — DI and DB are initialized. Rejected.

**Implementation Pattern**:
```csharp
public static int Main(string[] args)
{
    VelopackApp.Build().Run();  // Must be first — may exit process
    return BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
}
```

## R-003: Dev/Debug Mode Guard

**Decision**: Use `UpdateManager.IsInstalled` to skip all update operations when running from IDE or unpackaged builds.

**Rationale**: `IsInstalled` returns `false` when the app is not packaged by Velopack (i.e., during development). This prevents `NotInstalledException` and avoids unnecessary GitHub API calls during development. The `IUpdateService` interface returns a no-op result when not installed.

**Alternatives Considered**:
- **Preprocessor directives (`#if DEBUG`)**: Prevents testing the update flow in debug builds. Rejected.
- **Configuration toggle**: Adds unnecessary configuration surface for a concern that Velopack handles natively. Rejected.

## R-004: Architecture Layer Placement

**Decision**: Define `IUpdateService` in `Rentier.Application.Interfaces`; implement `VelopackUpdateService` in `Rentier.Infrastructure`; consume from `MainWindowViewModel` in `Rentier.Desktop`.

**Rationale**: Follows the existing pattern established by `IMailboxSyncService`, `IExchangeRateFetcher`, and other infrastructure service interfaces. The Application layer defines the contract; Infrastructure implements it using the Velopack SDK; Desktop consumes it via DI. No layer boundary violations.

**Alternatives Considered**:
- **Desktop-only service (no Application interface)**: Violates Clean Architecture — the Desktop layer would depend directly on Velopack. Rejected.
- **Domain layer interface**: Update checking is not a domain concern — it's infrastructure/application orchestration. Rejected.

## R-005: Update Notification UI Placement

**Decision**: Add a notification bar as a `DockPanel.Dock="Top"` element in the content area of `MainWindow.axaml`, above the `ContentControl`. Driven by `MainWindowViewModel` properties.

**Rationale**: The notification must be visible regardless of which page the user navigates to. Placing it inside individual views would require duplicating state across all ViewModels. The MainWindow's DockPanel layout naturally supports docking a bar at the top of the content area. This follows the spec requirement for a "notification bar at the top of the main window."

**Alternatives Considered**:
- **Toast/overlay notification**: Less discoverable, harder to include progress bar. Rejected.
- **Status bar at bottom**: Spec explicitly calls for "top of the main window." Rejected.
- **Separate popup window**: Intrusive, violates the "non-intrusive" requirement. Rejected.

## R-006: Update State Machine Design

**Decision**: Model update workflow as a reactive state machine with states: `Idle`, `Checking`, `UpdateAvailable`, `Downloading`, `Downloaded`, `Error`.

**Rationale**: The spec defines clear state transitions matching these states. Using a reactive property (`UpdateState`) on `MainWindowViewModel` allows the notification bar AXAML to bind visibility and content declaratively. ReactiveUI `WhenAnyValue` drives UI transitions without imperative code-behind.

**State Transitions**:
```
Idle --(app start)--> Checking
Checking --(no update)--> Idle
Checking --(update found)--> UpdateAvailable
Checking --(error)--> Idle (silent)
UpdateAvailable --(user clicks Update Now)--> Downloading
UpdateAvailable --(user clicks Later)--> Dismissed
Downloading --(progress)--> Downloading (with %)
Downloading --(complete)--> Downloaded
Downloading --(error)--> Error
Downloaded --(user clicks Restart Now)--> [app restarts]
Downloaded --(user clicks Later)--> Idle (update applied on next restart)
Error --(user clicks Retry)--> Downloading
Error --(user clicks Dismiss)--> Idle
```

## R-007: Constitution Amendment — Network Scope

**Decision**: The constitution's approved outbound network endpoints (currently IMAP + NBS) must be amended to include `api.github.com` and `github.com` for auto-update functionality.

**Rationale**: The spec explicitly identifies this in CA-003/CA-004. GitHub API calls are read-only, public, unauthenticated, and transmit no user data. This is a minimal scope expansion for significant user value (automatic updates).

**Amendment Scope**: Add to Principle II (Local-First Security and Privacy):
> Outbound network access is restricted to IMAP (user mailbox), NBS exchange-rate endpoints, and GitHub Releases API (auto-update check and download only).

## R-008: Thread Safety and Concurrency

**Decision**: Use a `SemaphoreSlim(1,1)` in the update service to prevent concurrent download attempts. Progress callbacks are marshalled to UI thread via `RxApp.MainThreadScheduler`.

**Rationale**: Velopack's `DownloadUpdatesAsync` uses a file-based mutex internally, but a second concurrent call throws `UpdateLockException`. An application-level semaphore provides cleaner error handling. Progress callbacks arrive on background threads and must be marshalled to the UI thread for Avalonia binding updates.

**Alternatives Considered**:
- **Relying solely on Velopack's internal lock**: Throws exceptions rather than queuing. Less user-friendly. Rejected.
- **Disabling the Update button during download**: Good UX but insufficient as a thread-safety mechanism alone. Used in combination.
