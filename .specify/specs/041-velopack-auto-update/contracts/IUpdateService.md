# Contract: IUpdateService

**Layer**: Rentier.Application.Interfaces  
**Implemented by**: VelopackUpdateService (Rentier.Infrastructure)  
**Consumed by**: MainWindowViewModel (Rentier.Desktop)

## Interface Definition

```csharp
namespace Rentier.Application.Interfaces;

/// <summary>
/// Application-layer contract for auto-update operations.
/// Infrastructure provides the Velopack-based implementation.
/// Desktop consumes this via DI — never references Velopack directly.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Whether the application is installed via the update framework.
    /// Returns false in dev/debug mode (unpackaged builds).
    /// When false, all other methods are no-ops.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Checks the configured update source for a newer version.
    /// Returns a result indicating whether an update is available and the target version.
    /// Must be non-blocking and safe to call during app startup.
    /// Failures are returned as errors, not thrown.
    /// </summary>
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the available update with progress reporting.
    /// The progress callback receives values from 0 to 100.
    /// Callers must ensure CheckForUpdatesAsync returned an available update before calling.
    /// </summary>
    Task DownloadUpdateAsync(
        Action<int> progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the downloaded update and restarts the application.
    /// This method does NOT return — the process exits and relaunches.
    /// Callers must ensure DownloadUpdateAsync completed successfully.
    /// </summary>
    void ApplyUpdateAndRestart();

    /// <summary>
    /// Schedules the downloaded update to be applied on next app exit.
    /// Use when the user clicks [Later] on the restart prompt.
    /// </summary>
    void ScheduleUpdateOnExit();
}
```

## DTOs

```csharp
namespace Rentier.Application.DTOs;

/// <summary>
/// Result of an update availability check.
/// </summary>
public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string? TargetVersion);
```

## Behavioral Contract

| Method | Precondition | Postcondition | Error Handling |
|---|---|---|---|
| `IsInstalled` | None | Returns true if app was packaged by Velopack | Never throws |
| `CheckForUpdatesAsync` | None | Returns `UpdateCheckResult` | Network errors → `UpdateCheckResult(false, null)` |
| `DownloadUpdateAsync` | `CheckForUpdatesAsync` returned available | Update package downloaded locally | Throws on network failure (caller handles) |
| `ApplyUpdateAndRestart` | `DownloadUpdateAsync` completed | Process exits and relaunches | Throws if no downloaded update exists |
| `ScheduleUpdateOnExit` | `DownloadUpdateAsync` completed | Update applied on next manual exit | Throws if no downloaded update exists |

## DI Registration

```csharp
// In InfrastructureServiceExtensions.AddInfrastructureServicesAsync():
services.AddSingleton<IUpdateService, VelopackUpdateService>();

// Singleton because UpdateManager caches state (downloaded update info)
// and must survive across the app session.
```
