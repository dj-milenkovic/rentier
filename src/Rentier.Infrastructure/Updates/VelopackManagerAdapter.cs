using Velopack;
using Velopack.Sources;

namespace Rentier.Infrastructure.Updates;

/// <summary>
/// Production implementation of <see cref="IVelopackManager"/> that wraps Velopack's
/// real <see cref="UpdateManager"/> with a <see cref="GithubSource"/> targeting the
/// Rentier GitHub repository.
/// </summary>
internal sealed class VelopackManagerAdapter : IVelopackManager
{
    private const string RepoUrl = "https://github.com/zribktad/Rentier";

    private readonly UpdateManager _manager;
    private UpdateInfo? _pendingUpdate;

    public VelopackManagerAdapter()
    {
        _manager = new UpdateManager(new GithubSource(RepoUrl, null, false));
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
