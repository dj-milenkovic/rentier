using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Rentier.Infrastructure.Persistence;
using Rentier.Tests.Common.Builders;

namespace Rentier.Infrastructure.Tests.Migrations;

/// <summary>
/// Creates a file-based SQLite database seeded at a specific historical migration point,
/// ready for upgrade testing. Each factory instance manages its own temp file.
/// </summary>
/// <remarks>
/// EF Core applies migrations in timestamp order, not in the numeric name order.
/// The actual application sequence is: 0001→0010 (April 6–8), then 0012→0014 (April 14–23),
/// and finally 0011 (July 17 timestamp — out-of-sequence naming). This means:
/// <list type="bullet">
///   <item>At baseline 0010: Reports lacks EmailDate; Filings lacks Ticker and ExchangeRateSource*.</item>
///   <item>At baseline 0014: Reports has EmailDate and Filings has Ticker, but Filings still lacks
///         ExchangeRateSourceDate/Type (those are in 0011, applied last).</item>
/// </list>
/// Because EF's current model includes all columns, we use raw SQL to seed Reports and Filings
/// so the INSERT matches the actual baseline schema rather than the latest model.
/// </remarks>
public sealed class MigrationBaselineFactory : IAsyncDisposable
{
    /// <summary>Last migration applied in the "0010" baseline (before 0012/0013/0014/0011).</summary>
    public const string Migration0010Id = "20260408180904_0010_SyncReplayControls";

    /// <summary>Last migration applied in the "0014" baseline (before 0011 — the July outlier).</summary>
    public const string Migration0014Id = "20260423112907_0014_UserPreferences";

    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    private MigrationBaselineFactory(string dbPath, SqliteConnection connection)
    {
        _dbPath = dbPath;
        _connection = connection;
    }

    /// <summary>
    /// Creates a baseline DB at migration 0010 seeded with realistic data.
    /// Pending migrations when upgrading to latest:
    ///   0012 (EmailDate on Reports), 0013 (Ticker on Filings),
    ///   0014 (UserPreferences table), 0011 (ExchangeRateSourceDate/Type on Filings).
    /// </summary>
    public static Task<MigrationBaselineFactory> CreateAtMigration0010Async()
        => CreateAsync(Migration0010Id);

    /// <summary>
    /// Creates a baseline DB at migration 0014 seeded with realistic data.
    /// Pending migration when upgrading to latest:
    ///   0011 (ExchangeRateSourceDate/Type columns on Filings).
    /// </summary>
    public static Task<MigrationBaselineFactory> CreateAtMigration0014Async()
        => CreateAsync(Migration0014Id);

    /// <summary>Opens a new AppDbContext against the baseline database connection.</summary>
    public AppDbContext OpenContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);

    /// <summary>Applies all remaining pending migrations to the baseline database.</summary>
    public async Task MigrateToLatestAsync()
    {
        await using var ctx = OpenContext();
        await ctx.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        // ClearAllPools ensures any pooled handles (from EF's internal connection management)
        // are released before we attempt file deletion — critical on Windows.
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    // ── Factory ───────────────────────────────────────────────────────────

    private static async Task<MigrationBaselineFactory> CreateAsync(string targetMigration)
    {
        var path = Path.Combine(Path.GetTempPath(), $"rentier_baseline_{Guid.NewGuid():N}.db");
        // Pooling=False ensures the file handle is released immediately on Dispose (required on Windows).
        var conn = new SqliteConnection($"Data Source={path};Pooling=False");
        await conn.OpenAsync();

        // Guard: EF Core deliberately does NOT close or dispose externally-managed connections
        // when a DbContext is disposed. Any exception thrown during setup would orphan conn and
        // leave the temp file behind, accumulating on flaky CI. The catch block ensures cleanup.
        try
        {
            return await SetupBaselineAsync(conn, path, targetMigration);
        }
        catch
        {
            await conn.DisposeAsync();
            SqliteConnection.ClearAllPools();
            foreach (var p in new[] { path, path + "-wal", path + "-shm" })
                if (File.Exists(p)) File.Delete(p);
            throw;
        }
    }

    private static async Task<MigrationBaselineFactory> SetupBaselineAsync(
        SqliteConnection conn, string path, string targetMigration)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;

        await using (var ctx = new AppDbContext(options))
        {
            var migrator = ctx.GetInfrastructure().GetRequiredService<IMigrator>();
            await migrator.MigrateAsync(targetMigration);
        }

        // Seed stable entities via EF (their schema is the same at both baselines).
        Guid importerId;
        await using (var ctx = new AppDbContext(options))
        {
            var primary   = SeedDataBuilder.PrimaryProfile();
            var secondary = SeedDataBuilder.SecondaryProfile();
            await ctx.TaxpayerProfiles.AddRangeAsync(primary, secondary);

            await ctx.PublicHolidays.AddRangeAsync(SeedDataBuilder.SerbianHolidays2024());
            await ctx.HolidayYearRange.AddAsync(SeedDataBuilder.HolidayYearRange2024To2026());

            var mailbox = SeedDataBuilder.Mailbox();
            await ctx.Mailboxes.AddAsync(mailbox);

            var linked     = SeedDataBuilder.LinkedImporter(primary.Id, mailbox.Id);
            var standalone = SeedDataBuilder.StandaloneImporter();
            await ctx.Importers.AddRangeAsync(linked, standalone);
            importerId = linked.Id;

            await ctx.ExchangeRateCache.AddRangeAsync(SeedDataBuilder.ExchangeRates());
            await ctx.SaveChangesAsync();

            // Seed schema-evolving entities using raw SQL matching the baseline column set.
            // FK enforcement is temporarily disabled so the raw inserts succeed even though
            // EF's value converters may format GUIDs differently than a plain string compare.
            bool is0014 = targetMigration == Migration0014Id;
            await ctx.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
            await InsertReportsRawAsync(conn, importerId, includeEmailDate: is0014);
            await InsertFilingsRawAsync(conn, includeTicker: is0014);
            if (is0014)
                await InsertUserPreferencesRawAsync(conn);
            await ctx.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");
        }

        return new MigrationBaselineFactory(path, conn);
    }

    // ── Raw SQL seeders (Reports & Filings schema differs per baseline) ────

    /// <param name="includeEmailDate">True at 0014 baseline; false at 0010 baseline.</param>
    private static async Task InsertReportsRawAsync(
        SqliteConnection conn, Guid importerId, bool includeEmailDate)
    {
        var content = "Id,ActivityCode,Symbol,Date,Description,Amount,Currency\n"u8.ToArray();
        var rows = new[]
        {
            (Guid.NewGuid(), importerId, "U12345678_20240301.csv",
             (long?)1001L, (DateOnly?)new DateOnly(2024, 3, 1)),
            (Guid.NewGuid(), importerId, "U12345678_20240401.csv",
             (long?)1002L, (DateOnly?)new DateOnly(2024, 4, 1)),
            (Guid.NewGuid(), importerId, "U12345678_20240501.csv",
             (long?)1003L, (DateOnly?)null),
        };

        string cols = includeEmailDate
            ? "Id, ImportDate, ImporterId, Status, ReportName, AttachmentContent, MailboxMessageId, OriginalReportId, EmailDate"
            : "Id, ImportDate, ImporterId, Status, ReportName, AttachmentContent, MailboxMessageId, OriginalReportId";

        foreach (var (id, impId, name, msgId, emailDate) in rows)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"INSERT INTO Reports ({cols}) VALUES " +
                              (includeEmailDate
                                  ? "(@id,@date,@impId,0,@name,@content,@msgId,NULL,@emailDate)"
                                  : "(@id,@date,@impId,0,@name,@content,@msgId,NULL)");

            cmd.Parameters.AddWithValue("@id",      id.ToString());
            cmd.Parameters.AddWithValue("@date",    DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@impId",   impId.ToString());
            cmd.Parameters.AddWithValue("@name",    name);
            cmd.Parameters.AddWithValue("@content", content);
            cmd.Parameters.AddWithValue("@msgId",   (object?)msgId ?? DBNull.Value);

            if (includeEmailDate)
                cmd.Parameters.AddWithValue("@emailDate",
                    emailDate.HasValue ? emailDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <param name="includeTicker">True at 0014 baseline (0013 adds Ticker); false at 0010.</param>
    private static async Task InsertFilingsRawAsync(SqliteConnection conn, bool includeTicker)
    {
        var primaryId   = SeedDataBuilder.PrimaryProfileId.ToString();
        var secondaryId = SeedDataBuilder.SecondaryProfileId.ToString();

        // (profileId, incomeType, entity, incomeDate, gross, wht, grossTax, tax)
        var rows = new[]
        {
            (primaryId,   0, "Apple Inc",               "2024-03-15", 123456.78m, 18518.52m, 18518.52m, 0.00m),
            (primaryId,   0, "Microsoft Corp",           "2024-04-10",  87654.32m, 13148.15m, 13148.15m, 0.00m),
            (primaryId,   0, "Alphabet Inc",             "2024-05-20",  56789.01m,  8518.35m,  8518.35m, 0.00m),
            (secondaryId, 1, "Interactive Brokers LLC",  "2024-06-01",   9876.54m,     0.00m,  1481.48m, 1481.48m),
            (primaryId,   0, "Amazon.com Inc",           "2024-07-05",  44321.67m,  6648.25m,  6648.25m, 0.00m),
            (secondaryId, 0, "NIS AD Novi Sad",          "2024-08-15",  33000.00m,     0.00m,  4950.00m, 4950.00m),
        };

        // Status values: 0=Init (rows 0,3,4,5), 1=Filed (row 1), 2=Paid (row 2)
        var statuses = new[] { 0, 1, 2, 0, 0, 0 };

        string cols = includeTicker
            ? "Id,TaxpayerProfileId,TaxPeriod,Status,IncomeType,PayingEntity,IncomeDate,GrossIncomeRsd,WhtPaidRsd,GrossTaxPayableRsd,TaxPayableRsd,FilingDeadline,ReportId,PaymentReference,Ticker"
            : "Id,TaxpayerProfileId,TaxPeriod,Status,IncomeType,PayingEntity,IncomeDate,GrossIncomeRsd,WhtPaidRsd,GrossTaxPayableRsd,TaxPayableRsd,FilingDeadline,ReportId,PaymentReference";

        string valTemplate = includeTicker
            ? "(@id,@pid,@period,@status,@type,@entity,@date,@gross,@wht,@grossTax,@tax,@deadline,NULL,NULL,NULL)"
            : "(@id,@pid,@period,@status,@type,@entity,@date,@gross,@wht,@grossTax,@tax,@deadline,NULL,NULL)";

        for (var i = 0; i < rows.Length; i++)
        {
            var (pid, incomeType, entity, incomeDate, gross, wht, grossTax, tax) = rows[i];
            var deadline = DateOnly.ParseExact(incomeDate, "yyyy-MM-dd").AddDays(30).ToString("yyyy-MM-dd");

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"INSERT INTO Filings ({cols}) VALUES {valTemplate}";
            cmd.Parameters.AddWithValue("@id",       Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@pid",      pid);
            cmd.Parameters.AddWithValue("@period",   incomeDate);
            cmd.Parameters.AddWithValue("@status",   statuses[i]);
            cmd.Parameters.AddWithValue("@type",     incomeType);
            cmd.Parameters.AddWithValue("@entity",   entity);
            cmd.Parameters.AddWithValue("@date",     incomeDate);
            cmd.Parameters.AddWithValue("@gross",    D(gross));
            cmd.Parameters.AddWithValue("@wht",      D(wht));
            cmd.Parameters.AddWithValue("@grossTax", D(grossTax));
            cmd.Parameters.AddWithValue("@tax",      D(tax));
            cmd.Parameters.AddWithValue("@deadline", deadline);
            await cmd.ExecuteNonQueryAsync();
        }

        static string D(decimal v) => v.ToString(CultureInfo.InvariantCulture);
    }

    private static async Task InsertUserPreferencesRawAsync(SqliteConnection conn)
    {
        var prefs = new[] { ("Language", "sr-Latn"), ("Theme", "Dark"), ("LastOpenedTab", "Filings") };
        foreach (var (key, value) in prefs)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO UserPreferences (Key, Value) VALUES (@k, @v)";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
