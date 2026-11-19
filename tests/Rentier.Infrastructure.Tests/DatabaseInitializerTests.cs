using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Tests.Migrations;
using Rentier.Tests.Common.Fakes;

namespace Rentier.Infrastructure.Tests;

[Trait("Category", "Integration")]
public sealed class DatabaseInitializerTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    public DatabaseInitializerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"rentier_init_{Guid.NewGuid():N}.db");
        _connection = new SqliteConnection($"Data Source={_dbPath};Pooling=False");
        _connection.Open();
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    // ── Fresh-install ─────────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_FreshDatabase_AppliesAllMigrationsWithoutError()
    {
        await using var ctx = CreateContext();
        var sut = new DatabaseInitializer(ctx, NullLogger<DatabaseInitializer>.Instance);

        await sut.InitializeAsync(TestContext.Current.CancellationToken);

        var pending = await ctx.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);
        pending.Should().BeEmpty(because: "fresh install must leave no pending migrations");
    }

    [Fact]
    public async Task InitializeAsync_FreshDatabase_ProducesNonEmptyMigrationHistory()
    {
        await using var ctx = CreateContext();
        var sut = new DatabaseInitializer(ctx, NullLogger<DatabaseInitializer>.Instance);

        await sut.InitializeAsync(TestContext.Current.CancellationToken);

        var applied = await ctx.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        applied.Should().NotBeEmpty(because: "at least one migration must be recorded on a fresh install");
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_IsIdempotent()
    {
        await using var ctx1 = CreateContext();
        await new DatabaseInitializer(ctx1, NullLogger<DatabaseInitializer>.Instance).InitializeAsync(TestContext.Current.CancellationToken);

        await using var ctx2 = CreateContext();
        var act = async () =>
            await new DatabaseInitializer(ctx2, NullLogger<DatabaseInitializer>.Instance).InitializeAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync(
            because: "MigrateAsync on an already-migrated database must be idempotent");
    }

    // ── Upgrade from baseline ─────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_FromMigration0010Baseline_CompletesAllPendingMigrations()
    {
        await using var factory = await MigrationBaselineFactory.CreateAtMigration0010Async();
        var ctx = factory.OpenContext();
        await using (ctx)
        {
            var sut = new DatabaseInitializer(ctx, NullLogger<DatabaseInitializer>.Instance);

            await sut.InitializeAsync(TestContext.Current.CancellationToken);

            var pending = await ctx.Database.GetPendingMigrationsAsync(
                TestContext.Current.CancellationToken);
            pending.Should().BeEmpty(
                because: "upgrade from 0010 baseline must apply migrations 0012, 0013, 0014, and 0011");
        }
    }

    [Fact]
    public async Task InitializeAsync_FromMigration0014Baseline_CompletesAllPendingMigrations()
    {
        await using var factory = await MigrationBaselineFactory.CreateAtMigration0014Async();
        var ctx = factory.OpenContext();
        await using (ctx)
        {
            var sut = new DatabaseInitializer(ctx, NullLogger<DatabaseInitializer>.Instance);

            await sut.InitializeAsync(TestContext.Current.CancellationToken);

            var pending = await ctx.Database.GetPendingMigrationsAsync(
                TestContext.Current.CancellationToken);
            pending.Should().BeEmpty(
                because: "upgrade from 0014 baseline must apply the out-of-sequence 0011 migration");
        }
    }

    // ── Interrupted migration (stale __EFMigrationsLock row) ──────────────────

    private static readonly TimeSpan SHORT_TIMEOUT = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Leaves the lock row an interrupted migration leaves behind: EF Core's SQLite provider
    /// takes the migration lock by inserting this row and only deletes it when it finishes.
    /// </summary>
    private static Task LeaveStaleMigrationLockAsync(AppDbContext ctx) =>
        ctx.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsLock" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK___EFMigrationsLock" PRIMARY KEY,
                "Timestamp" TEXT NOT NULL);
            INSERT INTO "__EFMigrationsLock" ("Id", "Timestamp") VALUES (1, '2026-11-19 21:55:00');
            """,
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task InitializeAsync_StaleLockWithPendingMigrations_ThrowsTimeoutInsteadOfHanging()
    {
        await using var factory = await MigrationBaselineFactory.CreateAtMigration0014Async();
        await using var ctx = factory.OpenContext();
        await LeaveStaleMigrationLockAsync(ctx);
        var sut = new DatabaseInitializer(ctx, NullLogger<DatabaseInitializer>.Instance, SHORT_TIMEOUT);

        var act = () => sut.InitializeAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<TimeoutException>())
            .WithMessage("*__EFMigrationsLock*");
    }

    [Fact]
    public async Task InitializeAsync_StaleLockWithPendingMigrations_LogsTheTimeoutAsError()
    {
        await using var factory = await MigrationBaselineFactory.CreateAtMigration0014Async();
        await using var ctx = factory.OpenContext();
        await LeaveStaleMigrationLockAsync(ctx);
        var logger = new CapturingLogger<DatabaseInitializer>();
        var sut = new DatabaseInitializer(ctx, logger, SHORT_TIMEOUT);

        try { await sut.InitializeAsync(TestContext.Current.CancellationToken); }
        catch (TimeoutException) { /* asserted in the test above */ }

        logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message.Contains("timed out"));
    }

    [Fact]
    public async Task InitializeAsync_StaleLockButDatabaseUpToDate_StartsWithoutWaitingForTheLock()
    {
        await using var ctx = CreateContext();
        await new DatabaseInitializer(ctx, NullLogger<DatabaseInitializer>.Instance)
            .InitializeAsync(TestContext.Current.CancellationToken);
        await LeaveStaleMigrationLockAsync(ctx);
        var sut = new DatabaseInitializer(ctx, NullLogger<DatabaseInitializer>.Instance, SHORT_TIMEOUT);

        var act = () => sut.InitializeAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync(because: "nothing is pending, so the leftover lock must not block startup");
    }

    [Fact]
    public async Task InitializeAsync_CallerCancels_PropagatesCancellationNotTimeout()
    {
        await using var ctx = CreateContext();
        var sut = new DatabaseInitializer(ctx, NullLogger<DatabaseInitializer>.Instance);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => sut.InitializeAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Diagnostics ───────────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_FreshDatabase_LogsMigrationStartAndCompletion()
    {
        await using var ctx = CreateContext();
        var logger = new CapturingLogger<DatabaseInitializer>();

        await new DatabaseInitializer(ctx, logger).InitializeAsync(TestContext.Current.CancellationToken);

        logger.Entries.Select(e => e.Message).Should().Satisfy(
            m => m.StartsWith("Migrating database:"),
            m => m.StartsWith("Database migrated:"));
    }

    [Fact]
    public async Task InitializeAsync_DatabaseUpToDate_LogsThatNothingWasApplied()
    {
        await using var ctx = CreateContext();
        await new DatabaseInitializer(ctx, NullLogger<DatabaseInitializer>.Instance)
            .InitializeAsync(TestContext.Current.CancellationToken);
        var logger = new CapturingLogger<DatabaseInitializer>();

        await new DatabaseInitializer(ctx, logger).InitializeAsync(TestContext.Current.CancellationToken);

        logger.AllMessages.Should().Contain("up to date");
    }

    // ── IAsyncDisposable ──────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
        SqliteConnection.ClearPool(_connection);
        foreach (var path in new[] { _dbPath, $"{_dbPath}-wal", $"{_dbPath}-shm" })
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                if (!File.Exists(path)) break;
                try { File.Delete(path); break; }
                catch (IOException) when (attempt < 3) { await Task.Delay(50); }
                catch (UnauthorizedAccessException) when (attempt < 3) { await Task.Delay(50); }
            }
        }
    }
}
