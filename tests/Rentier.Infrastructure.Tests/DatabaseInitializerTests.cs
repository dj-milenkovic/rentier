using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Tests.Migrations;

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
        var sut = new DatabaseInitializer(ctx);

        await sut.InitializeAsync(TestContext.Current.CancellationToken);

        var pending = await ctx.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);
        pending.Should().BeEmpty(because: "fresh install must leave no pending migrations");
    }

    [Fact]
    public async Task InitializeAsync_FreshDatabase_ProducesNonEmptyMigrationHistory()
    {
        await using var ctx = CreateContext();
        var sut = new DatabaseInitializer(ctx);

        await sut.InitializeAsync(TestContext.Current.CancellationToken);

        var applied = await ctx.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        applied.Should().NotBeEmpty(because: "at least one migration must be recorded on a fresh install");
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_IsIdempotent()
    {
        await using var ctx1 = CreateContext();
        await new DatabaseInitializer(ctx1).InitializeAsync(TestContext.Current.CancellationToken);

        await using var ctx2 = CreateContext();
        var act = async () =>
            await new DatabaseInitializer(ctx2).InitializeAsync(TestContext.Current.CancellationToken);

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
            var sut = new DatabaseInitializer(ctx);

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
            var sut = new DatabaseInitializer(ctx);

            await sut.InitializeAsync(TestContext.Current.CancellationToken);

            var pending = await ctx.Database.GetPendingMigrationsAsync(
                TestContext.Current.CancellationToken);
            pending.Should().BeEmpty(
                because: "upgrade from 0014 baseline must apply the out-of-sequence 0011 migration");
        }
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
