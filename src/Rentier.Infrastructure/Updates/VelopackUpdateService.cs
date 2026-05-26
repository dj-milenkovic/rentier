using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;

namespace Rentier.Infrastructure.Updates;

/// <summary>
/// Velopack-backed implementation of <see cref="IUpdateService"/>.
/// Wraps the internal <see cref="IVelopackManager"/> abstraction so the class
/// can be fully unit-tested without a live Velopack installation.
/// </summary>
public sealed class VelopackUpdateService : IUpdateService, IDisposable
{
    private readonly IVelopackManager _manager;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Production constructor — creates a real <see cref="VelopackManagerAdapter"/>
    /// pointing at the Rentier GitHub repository.
    /// </summary>
    public VelopackUpdateService() : this(new VelopackManagerAdapter()) { }

    /// <summary>
    /// Test constructor — accepts an injected <see cref="IVelopackManager"/> for full
    /// control over Velopack behavior in unit tests.
    /// </summary>
    internal VelopackUpdateService(IVelopackManager manager)
    {
        _manager = manager;
    }

    /// <inheritdoc />
    public bool IsInstalled => _manager.IsInstalled;

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_manager.IsInstalled)
            return new UpdateCheckResult(false, null);

        try
        {
            var version = await _manager.CheckForUpdatesAsync(cancellationToken)
                                        .ConfigureAwait(false);
            return version is not null
                ? new UpdateCheckResult(true, version)
                : new UpdateCheckResult(false, null);
        }
        catch
        {
            // Silent failure — network errors must never surface to the user during startup.
            return new UpdateCheckResult(false, null);
        }
    }

    /// <inheritdoc />
    public async Task DownloadUpdateAsync(
        Action<int> progress,
        CancellationToken cancellationToken = default)
    {
        if (!_manager.IsInstalled)
            return;

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _manager.DownloadUpdatesAsync(progress, cancellationToken)
                          .ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public void ApplyUpdateAndRestart()
    {
        if (!_manager.IsInstalled)
            return;

        _manager.ApplyUpdatesAndRestart();
    }

    /// <inheritdoc />
    public void ScheduleUpdateOnExit()
    {
        if (!_manager.IsInstalled)
            return;

        _manager.WaitExitThenApplyUpdates();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore.Dispose();
    }
}
