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

    public async Task<string?> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        var info = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
        _pendingUpdate = info;
        return info?.TargetFullRelease?.Version?.ToString();
    }

    public async Task DownloadUpdatesAsync(Action<int> progress, CancellationToken cancellationToken)
    {
        if (_pendingUpdate is null) return;
        await _manager.DownloadUpdatesAsync(_pendingUpdate, progress, cancellationToken)
                      .ConfigureAwait(false);
    }

    public void ApplyUpdatesAndRestart()
    {
        if (_pendingUpdate?.TargetFullRelease is null) return;
        _manager.ApplyUpdatesAndRestart(_pendingUpdate.TargetFullRelease);
    }

    public void WaitExitThenApplyUpdates()
    {
        if (_pendingUpdate?.TargetFullRelease is null) return;
        _manager.WaitExitThenApplyUpdates(_pendingUpdate.TargetFullRelease);
    }
}
