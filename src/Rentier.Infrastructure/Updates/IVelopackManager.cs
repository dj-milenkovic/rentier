namespace Rentier.Infrastructure.Updates;

/// <summary>
/// Internal abstraction over Velopack's UpdateManager.
/// Allows VelopackUpdateService to be fully unit-tested without relying on the
/// sealed Velopack.UpdateManager class.
/// </summary>
internal interface IVelopackManager
{
    /// <summary>Whether the running copy has Velopack's self-updating layout.</summary>
    bool IsInstalled { get; }

    /// <summary>Whether the last check found a newer release that can be downloaded.</summary>
    bool HasUpdateToDownload { get; }

    /// <summary>
    /// Whether a downloaded release is waiting to be applied — downloaded in this session or
    /// in an earlier one.
    /// </summary>
    bool HasUpdateToApply { get; }

    /// <summary>
    /// Checks for an available update.
    /// Returns the target version string if available, or null if none.
    /// </summary>
    Task<string?> CheckForUpdatesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The release-notes Markdown packed into the update found by the last check
    /// (<c>vpk pack --releaseNotes</c>), or null when there is none.
    /// </summary>
    string? PendingReleaseNotesMarkdown { get; }

    /// <summary>The installed version, or null when this copy is not an installed app.</summary>
    string? CurrentVersion { get; }

    /// <summary>
    /// Downloads the update found by the last check, reporting progress via the callback (0–100).
    /// </summary>
    Task DownloadUpdatesAsync(Action<int> progress, CancellationToken cancellationToken);

    /// <summary>Applies the pending downloaded update and restarts the process.</summary>
    void ApplyUpdatesAndRestart();
}
