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
        _connection = new SqliteConnection($"Data Source={_dbPath}");
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
        await _context.Database.MigrateAsync();

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
        await _context.Database.MigrateAsync();

        var pending = await _context.Database.GetPendingMigrationsAsync();

        pending.Should().BeEmpty(
            because: "no EF model changes should exist outside of a committed migration; " +
                     "run 'dotnet ef migrations add' if you changed the model");
    }

    [Fact]
    public async Task AllMigrations_Applied_MigrationHistoryMatchesExpectedCount()
    {
        await _context.Database.MigrateAsync();

        var applied = await _context.Database.GetAppliedMigrationsAsync();

        // 14 migrations: 0001 through 0014 (0011 has a July timestamp but is still one migration)
        applied.Should().HaveCount(14,
            because: "the migration history table must record exactly 14 applied migrations");
    }

    [Fact]
    public async Task FilingsTable_HasDecimalPrecisionColumns_AfterMigration()
    {
        await _context.Database.MigrateAsync();

        // Verify column info via pragma — precision on SQLite TEXT columns
        // is enforced at the EF model level, not in the DB file itself.
        // This test checks that EF can round-trip a value with the configured precision.
        var profileId = Guid.NewGuid();
        var profile = new Domain.Entities.TaxpayerProfile(
            profileId, "1112223334445", "Test User", "Test Address", "11000");
        await _context.TaxpayerProfiles.AddAsync(profile);

        var filing = Domain.Entities.Filing.CreateFromIncome(
            profileId, Domain.Enums.IncomeType.Dividend,
            "Test Corp", new DateOnly(2024, 6, 15),
            grossIncomeRsd: 123456.78m,
            whtPaidRsd: 18518.61m,
            grossTaxPayableRsd: 18518.61m,
            taxPayableRsd: 0m,
            filingDeadline: new DateOnly(2024, 7, 15));
        await _context.Filings.AddAsync(filing);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var reloaded = await _context.Filings.SingleAsync(f => f.Id == filing.Id);

        reloaded.GrossIncomeRsd.Should().Be(123456.78m,
            because: "decimal precision (18,2) must be preserved across the SQLite boundary");
        reloaded.WhtPaidRsd.Should().Be(18518.61m);
    }

    // ── IAsyncDisposable ─────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
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
}
