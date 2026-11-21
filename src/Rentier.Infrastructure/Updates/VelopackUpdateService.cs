using Microsoft.Extensions.Logging;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Infrastructure.Logging;

namespace Rentier.Infrastructure.Updates;

/// <summary>
/// Velopack-backed implementation of <see cref="IUpdateService"/>.
/// Wraps the internal <see cref="IVelopackManager"/> abstraction so the class
/// can be fully unit-tested without a live Velopack installation.
/// </summary>
public sealed class VelopackUpdateService : IUpdateService, IDisposable
{
    public const string UPDATE_NOT_INSTALLED = "UPDATE_NOT_INSTALLED";
    public const string UPDATE_NO_PENDING = "UPDATE_NO_PENDING";
    public const string UPDATE_DOWNLOAD_FAILED = "UPDATE_DOWNLOAD_FAILED";
    public const string UPDATE_APPLY_FAILED = "UPDATE_APPLY_FAILED";

    private static readonly Error NotInstalled =
        new(UPDATE_NOT_INSTALLED, "This copy of Rentier cannot update itself.");

    private static readonly Error NothingToDownload =
        new(UPDATE_NO_PENDING, "No update is available to download.");

    private static readonly Error NothingToApply =
        new(UPDATE_NO_PENDING, "No downloaded update is waiting to be applied.");

    private readonly IVelopackManager _manager;
    private readonly ILogger<VelopackUpdateService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Production constructor — creates a real <see cref="VelopackManagerAdapter"/>
    /// pointing at the Rentier GitHub repository.
    /// </summary>
    public VelopackUpdateService(ILogger<VelopackUpdateService> logger)
        : this(new VelopackManagerAdapter(), logger) { }

    /// <summary>
    /// Test constructor — accepts an injected <see cref="IVelopackManager"/> for full
    /// control over Velopack behavior in unit tests.
    /// </summary>
    internal VelopackUpdateService(IVelopackManager manager, ILogger<VelopackUpdateService> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsInstalled => _manager.IsInstalled;

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_manager.IsInstalled)
        {
            _logger.UpdateCheckSkippedNotInstalled();
            return new UpdateCheckResult(false, null);
        }

        try
        {
            var version = await _manager.CheckForUpdatesAsync(cancellationToken)
                                        .ConfigureAwait(false);
            if (version is null)
            {
                _logger.UpdateNotAvailable();
                return new UpdateCheckResult(false, null);
            }

            _logger.UpdateAvailable(version);
            return new UpdateCheckResult(true, version);
        }
        catch (Exception ex)
        {
            // Silent for the user — a failed check must never interrupt startup — but logged.
            // Deliberately broad (and includes cancellation): the result type has no failure
            // channel and "no update information" is the correct outcome for every failure.
            _logger.UpdateCheckFailed(ex);
            return new UpdateCheckResult(false, null);
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool, Error>> DownloadUpdateAsync(
        Action<int> progress,
        CancellationToken cancellationToken = default)
    {
        if (!_manager.IsInstalled)
            return Result<bool, Error>.Failure(NotInstalled);

        if (!_manager.HasUpdateToDownload)
            return Result<bool, Error>.Failure(NothingToDownload);

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _manager.DownloadUpdatesAsync(progress, cancellationToken)
                          .ConfigureAwait(false);
            return Result<bool, Error>.Success(true);
        }
        // Infrastructure boundary: network, disk, checksum and lock failures all become a
        // Result the UI can show; none of them may crash the app.
        catch (Exception ex)
        {
            _logger.UpdateDownloadFailed(ex);
            return Result<bool, Error>.Failure(new Error(UPDATE_DOWNLOAD_FAILED, ex.Message));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public Result<bool, Error> ApplyUpdateAndRestart()
    {
        if (!_manager.IsInstalled)
            return Result<bool, Error>.Failure(NotInstalled);

        if (!_manager.HasUpdateToApply)
            return Result<bool, Error>.Failure(NothingToApply);

        try
        {
            // On success Velopack exits the process here.
            _manager.ApplyUpdatesAndRestart();
            return Result<bool, Error>.Success(true);
        }
        // Infrastructure boundary: a missing Update.exe or a held lock must surface as an
        // error the user can see, not an unhandled exception.
        catch (Exception ex)
        {
            _logger.UpdateApplyFailed(ex);
            return Result<bool, Error>.Failure(new Error(UPDATE_APPLY_FAILED, ex.Message));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore.Dispose();
    }
}
