using Rentier.Application.Common;
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
    /// Whether the running copy has the update framework's self-updating layout.
    /// False for dev runs and for packages built without it; every operation is then inert.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Checks the configured update source for a newer version.
    /// Never throws and has no failure channel by design: a failed check (offline, feed
    /// unreachable) is logged and reported as "no update available", because it must never
    /// interrupt the user at startup.
    /// </summary>
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the update found by the last successful <see cref="CheckForUpdatesAsync"/>,
    /// reporting progress from 0 to 100. Returns a failure when nothing is pending or the
    /// download fails.
    /// </summary>
    Task<Result<bool, Error>> DownloadUpdateAsync(
        Action<int> progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the downloaded update and restarts the application. On success the process
    /// exits and this method does not return; it returns only with a failure.
    /// A downloaded update that is not applied now is applied automatically on the next launch.
    /// </summary>
    Result<bool, Error> ApplyUpdateAndRestart();
}
