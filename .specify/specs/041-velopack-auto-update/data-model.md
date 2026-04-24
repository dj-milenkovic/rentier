# Data Model: Velopack Auto-Update

**Feature**: 041-velopack-auto-update  
**Date**: 2025-07-14

## Entities and Value Objects

### UpdateState (Enum — Application Layer)

Represents the current phase of the update workflow. Drives notification bar visibility and content in the UI.

| Value | Description | UI Effect |
|---|---|---|
| `Idle` | No update activity | Notification bar hidden |
| `Checking` | Background check in progress | No visible UI (silent) |
| `UpdateAvailable` | New version detected | Notification bar visible with version + [Update Now] / [Later] |
| `Downloading` | Download in progress | Progress bar visible with percentage |
| `Downloaded` | Download complete, restart pending | Restart prompt with [Restart Now] / [Later] |
| `Error` | Download failed | Error message with [Retry] / [Dismiss] |
| `Dismissed` | User clicked [Later] on notification | Notification bar hidden for session |

**Location**: `Rentier.Application.DTOs.UpdateState`  
**Kind**: `enum`

### UpdateCheckResult (Record — Application Layer)

Read-only DTO returned by `IUpdateService.CheckForUpdatesAsync()`.

| Field | Type | Description |
|---|---|---|
| `IsUpdateAvailable` | `bool` | Whether a newer version exists |
| `TargetVersion` | `string?` | Version string of the available update (e.g., "1.2.0") |

**Location**: `Rentier.Application.DTOs.UpdateCheckResult`  
**Kind**: `record`  
**Validation**: None — immutable read-only DTO from infrastructure  
**Nullability**: `TargetVersion` is null when `IsUpdateAvailable` is false

### DownloadProgressInfo (Record — Application Layer)

DTO for progress reporting during download.

| Field | Type | Description |
|---|---|---|
| `ProgressPercent` | `int` | Download progress from 0 to 100 |

**Location**: `Rentier.Application.DTOs.DownloadProgressInfo`  
**Kind**: `record`  
**Validation**: `ProgressPercent` constrained to 0–100 by infrastructure; consumers should clamp defensively

## Relationships

```text
IUpdateService (Application interface)
    │
    ├── CheckForUpdatesAsync() → UpdateCheckResult
    ├── DownloadUpdateAsync(progress, cancel) → Task
    ├── ApplyUpdateAndRestartAsync() → void (exits)
    ├── ScheduleUpdateOnExit() → void
    └── IsInstalled → bool
    │
    └── Implemented by VelopackUpdateService (Infrastructure)
            │
            └── Wraps Velopack.UpdateManager + GithubSource
```

```text
MainWindowViewModel (Desktop)
    │
    ├── UpdateState : UpdateState (reactive property)
    ├── AvailableVersion : string? (reactive property)
    ├── DownloadProgress : int (reactive property)
    ├── UpdateErrorMessage : string? (reactive property)
    │
    ├── CheckForUpdateCommand : ReactiveCommand
    ├── DownloadUpdateCommand : ReactiveCommand
    ├── RestartCommand : ReactiveCommand
    ├── DismissUpdateCommand : ReactiveCommand
    └── RetryDownloadCommand : ReactiveCommand
```

## State Transitions

```text
                        ┌──────────────┐
            app start   │     Idle     │◄─── dismiss / later
            ──────────► │              │
                        └──────┬───────┘
                               │
                        ┌──────▼───────┐
                        │   Checking   │
                        └──┬───┬───┬───┘
                    no     │   │   │  error
                  update   │   │   │  (silent)
                     ┌─────┘   │   └────────┐
                     │         │             │
                     ▼    ┌────▼──────┐      ▼
                   Idle   │  Update   │    Idle
                          │ Available │
                          └─────┬─────┘
                      [Later]   │   [Update Now]
                         │      │
                    Dismissed    │
                          ┌─────▼──────┐
                          │ Downloading │◄── [Retry]
                          └──┬─────┬───┘
                     error   │     │  complete
                        ┌────┘     └────┐
                        ▼               ▼
                    ┌───────┐    ┌─────────────┐
                    │ Error │    │  Downloaded  │
                    └───────┘    └──┬───────┬──┘
                            [Later] │       │ [Restart Now]
                                    ▼       ▼
                                  Idle   [app restarts]
```

## No Persistence Required

This feature has no database entities. All state is transient (in-memory, session-scoped):
- `UpdateState` lives on `MainWindowViewModel` and resets to `Idle` on each app launch
- `UpdateCheckResult` is a transient DTO from the network call
- No EF Core migrations needed
- No new database tables
