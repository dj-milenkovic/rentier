using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace Rentier.Infrastructure.Updates;

/// <summary>
/// Production implementation of <see cref="IVelopackManager"/> that wraps Velopack's
/// real <see cref="UpdateManager"/> with a <see cref="GithubSource"/> targeting the
/// Rentier GitHub repository.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class VelopackManagerAdapter : IVelopackManager
{
    private const string UPDATE_FEED_METADATA_KEY = "UpdateFeedUrl";

    private readonly UpdateManager _manager;
    private UpdateInfo? _pendingUpdate;

    public VelopackManagerAdapter()
        : this(ResolveUpdateFeedUrl()) { }

    internal VelopackManagerAdapter(string updateFeedUrl)
    {
        _manager = new UpdateManager(new GithubSource(updateFeedUrl, null, false));
    }

    /// <summary>
    /// Reads the update feed from this assembly's <c>UpdateFeedUrl</c> metadata, which is
    /// emitted from the <c>RentierUpdateFeedUrl</c> MSBuild property. Keeping the location in
    /// build configuration rather than source avoids a hardcoded URI (Sonar S1075) and lets a
    /// fork retarget the updater without editing code.
    /// </summary>
    private static string ResolveUpdateFeedUrl()
    {
        var url = typeof(VelopackManagerAdapter).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == UPDATE_FEED_METADATA_KEY)?
            .Value;

        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException(
                $"Assembly metadata '{UPDATE_FEED_METADATA_KEY}' is missing. " +
                "Set the RentierUpdateFeedUrl MSBuild property in Rentier.Infrastructure.csproj.");

        return url;
    }

    public bool IsInstalled => _manager.IsInstalled;

    public bool HasUpdateToDownload => _pendingUpdate is not null;

    // Velopack's own record of a downloaded-but-unapplied release, so a package downloaded
    // before the last restart still counts.
    public bool HasUpdateToApply => _manager.UpdatePendingRestart is not null;

    public async Task<string?> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        // UpdateManager.CheckForUpdatesAsync takes no token; WaitAsync stops waiting on cancellation.
        // Task.Run keeps any synchronous work it does before its first await off the caller's
        // (UI) thread.
        var info = await Task.Run(() => _manager.CheckForUpdatesAsync(), cancellationToken)
                             .WaitAsync(cancellationToken)
                             .ConfigureAwait(false);
        _pendingUpdate = info;
        return info?.TargetFullRelease?.Version?.ToString();
    }

    public async Task DownloadUpdatesAsync(Action<int> progress, CancellationToken cancellationToken)
    {
        var pending = _pendingUpdate
            ?? throw new InvalidOperationException("No update to download; check for updates first.");
        await _manager.DownloadUpdatesAsync(pending, progress, cancellationToken)
                      .ConfigureAwait(false);
    }

    public void ApplyUpdatesAndRestart()
    {
        var pending = _manager.UpdatePendingRestart
            ?? throw new InvalidOperationException("No downloaded update to apply.");
        _manager.ApplyUpdatesAndRestart(pending);
    }
}
