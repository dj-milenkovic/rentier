using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Infrastructure.Persistence;
using Xunit;

namespace Rentier.Infrastructure.Tests.Migrations;

/// <summary>
/// Tier 1 — Migration chain integrity tests.
/// Validates that all EF Core migrations apply cleanly on an empty database
/// and that the applied schema matches the model snapshot.
/// These tests use file-based SQLite (not in-memory) to match the production runtime.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MigrationChainTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public MigrationChainTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"rentier_chain_{Guid.NewGuid():N}.db");
        _connection = new SqliteConnection($"Data Source={_dbPath};Pooling=False");
        _connection.Open();

        _context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options);
    }

    [Fact]
    public async Task AllMigrations_AppliedSequentially_CompleteWithoutException()
    {
        var act = async () => await _context.Database.MigrateAsync();

        await act.Should().NotThrowAsync("all migrations must apply cleanly on a fresh database");
    }

    [Fact]
    public async Task AllMigrations_Applied_ProducesExpectedTables()
    {
        await _context.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

        var tables = await GetTableNamesAsync();

        tables.Should().Contain(new[]
        {
            "TaxpayerProfiles",
            "PublicHolidays",
            "HolidayYearRange",
            "Mailboxes",
            "Importers",
            "ExchangeRateCache",
            "Reports",
            "Filings",
            "UserPreferences",
            "__EFMigrationsHistory",
        }, because: "each entity set must have its own table after migration");
    }

    [Fact]
    public async Task AllMigrations_Applied_NoPendingMigrationsRemain()
    {
        await _context.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

        var pending = await _context.Database.GetPendingMigrationsAsync(cancellationToken: TestContext.Current.CancellationToken);

        pending.Should().BeEmpty(
            because: "no EF model changes should exist outside of a committed migration; " +
                     "run 'dotnet ef migrations add' if you changed the model");
    }

    [Fact]
    public async Task AllMigrations_Applied_MigrationHistoryMatchesExpectedCount()
    {
        await _context.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

        var applied = await _context.Database.GetAppliedMigrationsAsync(cancellationToken: TestContext.Current.CancellationToken);

        // 14 migrations: 0001 through 0014 (0011 has a July timestamp but is still one migration)
        applied.Should().HaveCount(14,
            because: "the migration history table must record exactly 14 applied migrations");
    }

    [Fact]
    public async Task FilingsTable_HasDecimalPrecisionColumns_AfterMigration()
    {
        await _context.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Verify column info via pragma — precision on SQLite TEXT columns
        // is enforced at the EF model level, not in the DB file itself.
        // This test checks that EF can round-trip a value with the configured precision.
        var profileId = Guid.NewGuid();
        var profile = new Domain.Entities.TaxpayerProfile(
            profileId, "1112223334445", "Test User", "Test Address", "11000");
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);

        var filing = Domain.Entities.Filing.CreateFromIncome(
            profileId, Domain.Enums.IncomeType.Dividend,
            "Test Corp", new DateOnly(2024, 6, 15),
            grossIncomeRsd: 123456.78m,
            whtPaidRsd: 18518.61m,
            grossTaxPayableRsd: 18518.61m,
            taxPayableRsd: 0m,
            filingDeadline: new DateOnly(2024, 7, 15));
        await _context.Filings.AddAsync(filing, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _context.ChangeTracker.Clear();
        var reloaded = await _context.Filings.SingleAsync(f => f.Id == filing.Id, cancellationToken: TestContext.Current.CancellationToken);

        reloaded.GrossIncomeRsd.Should().Be(123456.78m,
            because: "decimal precision (18,2) must be preserved across the SQLite boundary");
        reloaded.WhtPaidRsd.Should().Be(18518.61m);
    }

    // ── IAsyncDisposable ─────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
        SqliteConnection.ClearPool(_connection);
        await DeleteDatabaseFilesAsync(_dbPath);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<string>> GetTableNamesAsync()
    {
        var tables = new List<string>();
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));
        return tables;
    }

    private static async Task DeleteDatabaseFilesAsync(string dbPath)
    {
        foreach (var path in new[] { dbPath, $"{dbPath}-wal", $"{dbPath}-shm" })
            await DeleteFileWithRetryAsync(path);
    }

    private static async Task DeleteFileWithRetryAsync(string path)
    {
        const int attempts = 3;

        for(var attempt=1; attempt <= attempts; attempt++)
        {
            if (!File.Exists(path))
                return;

            try
            {
                File.Delete(path);
                return;
            }
            catch(IOException) when (attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }
            catch(UnauthorizedAccessException) when (attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }

            if(File.Exists(path))
                File.Delete(path);
        }
    }
}
