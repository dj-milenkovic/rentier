using Rentier.Application.DTOs;

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
