namespace Rentier.Infrastructure.Updates;

/// <summary>
/// Internal abstraction over Velopack's UpdateManager.
/// Allows VelopackUpdateService to be fully unit-tested without relying on the
/// sealed Velopack.UpdateManager class.
/// </summary>
internal interface IVelopackManager
{
    /// <summary>Whether the application is installed via Velopack.</summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Checks for an available update.
    /// Returns the target version string if available, or null if none.
    /// </summary>
    Task<string?> CheckForUpdatesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the pending update, reporting progress via the callback (0–100).
    /// </summary>
    Task DownloadUpdatesAsync(Action<int> progress, CancellationToken cancellationToken);

    /// <summary>Applies the downloaded update and restarts the process.</summary>
    void ApplyUpdatesAndRestart();

    /// <summary>Schedules the downloaded update to be applied on next process exit.</summary>
    void WaitExitThenApplyUpdates();
}
