using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rentier.Application.Interfaces;
using Rentier.Infrastructure.Logging;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure;

/// <summary>
/// Applies pending EF Core migrations at startup, bounded by a timeout.
/// <para>
/// On SQLite, EF Core takes its migration lock by inserting a row into <c>__EFMigrationsLock</c>.
/// If a migration is interrupted (the app is killed or stopped in the debugger mid-migration)
/// that row is left behind and every later <c>MigrateAsync</c> waits for it forever. The timeout
/// turns that silent hang into a clear startup error, and skipping <c>MigrateAsync</c> when
/// nothing is pending means a leftover lock only matters when an update actually has to migrate.
/// </para>
/// </summary>
public sealed class DatabaseInitializer : IDatabaseInitializer
{
    /// <summary>
    /// Generous for a desktop SQLite database (real migrations take well under a second), short
    /// enough that a stuck lock fails visibly instead of leaving the user at a blank start.
    /// </summary>
    public static readonly TimeSpan DEFAULT_MIGRATION_TIMEOUT = TimeSpan.FromSeconds(60);

    private readonly AppDbContext _db;
    private readonly ILogger<DatabaseInitializer> _logger;
    private readonly TimeSpan _timeout;

    public DatabaseInitializer(AppDbContext db, ILogger<DatabaseInitializer> logger)
        : this(db, logger, DEFAULT_MIGRATION_TIMEOUT) { }

    /// <summary>Test constructor: lets tests use a short timeout.</summary>
    internal DatabaseInitializer(AppDbContext db, ILogger<DatabaseInitializer> logger, TimeSpan timeout)
    {
        _db = db;
        _logger = logger;
        _timeout = timeout;
    }

    /// <exception cref="TimeoutException">
    /// Migrations did not finish within the timeout, typically because an interrupted earlier
    /// migration left its lock behind.
    /// </exception>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var pending = (await _db.Database.GetPendingMigrationsAsync(ct)).Count();
        if (pending == 0)
        {
            _logger.DatabaseUpToDate();
            return;
        }

        _logger.MigrationStarted(pending);
        var stopwatch = Stopwatch.StartNew();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_timeout);
        try
        {
            await _db.Database.MigrateAsync(timeoutCts.Token);
        }
        // Only our timeout is translated; a caller's cancellation propagates unchanged.
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.MigrationTimedOut((int)_timeout.TotalSeconds, pending);
            throw new TimeoutException(
                $"Updating the Rentier database did not finish within {(int)_timeout.TotalSeconds} seconds. " +
                "This usually means an earlier update was interrupted (for example Rentier was closed or " +
                "stopped in the debugger while it was updating the database) and left a lock behind. " +
                "Close every other Rentier window and start Rentier again. If this keeps happening, delete " +
                "the rows in the __EFMigrationsLock table of the Rentier database, then start Rentier again.",
                ex);
        }

        _logger.MigrationCompleted(pending, stopwatch.ElapsedMilliseconds);
    }
}
