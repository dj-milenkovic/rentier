using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Rentier.Tests.Common.Builders;

namespace Rentier.Infrastructure.Tests.Migrations;

/// <summary>
/// Tier 2 — Seeded upgrade tests.
/// Validates that migrating from a realistic baseline database to the latest schema
/// preserves all data: row counts, decimal precision, nullable columns, FK integrity.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MigrationUpgradeTests
{
    // ── Baseline A: migration 0010 → latest ──────────────────────────────────

    /// <summary>
    /// Upgrading from migration 0010 (the last "foundation" migration) to the latest
    /// schema must not delete any rows or mutate existing values.
    /// Migrations applied: 0012 (EmailDate on Reports), 0013 (Ticker on Filings),
    ///                     0014 (UserPreferences table), 0011 (FilingRateProvenance).
    /// </summary>
    [Fact]
    public async Task Upgrade_FromMigration0010_AllRowsPreserved()
    {
        await using var baseline = await MigrationBaselineFactory.CreateAtMigration0010Async();

        int profileCount, holidayCount, mailboxCount, importerCount,
            rateCount, reportCount, filingCount;

        await using (var ctx = baseline.OpenContext())
        {
            profileCount = await ctx.TaxpayerProfiles.CountAsync(TestContext.Current.CancellationToken);
            holidayCount = await ctx.PublicHolidays.CountAsync(TestContext.Current.CancellationToken);
            mailboxCount = await ctx.Mailboxes.CountAsync(TestContext.Current.CancellationToken);
            importerCount = await ctx.Importers.CountAsync(TestContext.Current.CancellationToken);
            rateCount = await ctx.ExchangeRateCache.CountAsync(TestContext.Current.CancellationToken);
            reportCount = await ctx.Reports.CountAsync(TestContext.Current.CancellationToken);
            filingCount = await ctx.Filings.CountAsync(TestContext.Current.CancellationToken);
        }

        await baseline.MigrateToLatestAsync();

        await using var ctx2 = baseline.OpenContext();
        ctx2.TaxpayerProfiles.CountAsync(TestContext.Current.CancellationToken).Should().Be(profileCount);
        ctx2.PublicHolidays.CountAsync(TestContext.Current.CancellationToken).Should().Be(holidayCount);
        ctx2.Mailboxes.CountAsync(TestContext.Current.CancellationToken).Should().Be(mailboxCount);
        ctx2.Importers.CountAsync(TestContext.Current.CancellationToken).Should().Be(importerCount);
        ctx2.ExchangeRateCache.CountAsync(TestContext.Current.CancellationToken).Should().Be(rateCount);
        ctx2.Reports.CountAsync(TestContext.Current.CancellationToken).Should().Be(reportCount);
        ctx2.Filings.CountAsync(TestContext.Current.CancellationToken).Should().Be(filingCount);
    }

    [Fact]
    public async Task Upgrade_FromMigration0010_FilingDecimalPrecisionPreserved()
    {
        await using var baseline = await MigrationBaselineFactory.CreateAtMigration0010Async();
        await baseline.MigrateToLatestAsync();

        await using var ctx = baseline.OpenContext();
        var grossAmounts = await ctx.Filings
            .Select(f => f.GrossIncomeRsd)
            .OrderBy(a => a)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        var expected = SeedDataBuilder.KnownGrossAmountsSorted();
        grossAmounts.Should().Equal(expected,
            because: "GrossIncomeRsd values must survive migration with decimal(18,2) precision intact");
    }

    [Fact]
    public async Task Upgrade_FromMigration0010_NewNullableFilingColumnsAreNull()
    {
        await using var baseline = await MigrationBaselineFactory.CreateAtMigration0010Async();
        await baseline.MigrateToLatestAsync();

        await using var ctx = baseline.OpenContext();

        // All pre-existing filings must have NULL for columns added in 0011 and 0013
        var filings = await ctx.Filings.ToListAsync(TestContext.Current.CancellationToken);
        filings.Should().AllSatisfy(f =>
        {
            f.ExchangeRateSourceDate.Should().BeNull(
                "migration 0011 adds this column; pre-existing rows must default to NULL");
            f.ExchangeRateSourceType.Should().BeNull(
                "migration 0011 adds this column; pre-existing rows must default to NULL");
            f.Ticker.Should().BeNull(
                "migration 0013 adds this column; pre-existing rows must default to NULL");
        });
    }

    [Fact]
    public async Task Upgrade_FromMigration0010_NewNullableReportColumnIsNull()
    {
        await using var baseline = await MigrationBaselineFactory.CreateAtMigration0010Async();

        // Seed reports that have an EmailDate value so we can verify it was not
        // affected. The seed data already includes reports with and without EmailDate,
        // but at baseline 0010 the EmailDate column doesn't exist yet.
        // After migration 0012 adds it, all pre-existing rows should be NULL.
        await baseline.MigrateToLatestAsync();

        await using var ctx = baseline.OpenContext();
        var reports = await ctx.Reports.ToListAsync(TestContext.Current.CancellationToken);
        reports.Should().AllSatisfy(r =>
            r.EmailDate.Should().BeNull(
                "migration 0012 adds EmailDate; all pre-existing report rows must default to NULL"));
    }

    [Fact]
    public async Task Upgrade_FromMigration0010_UserPreferencesTableCreatedAndEmpty()
    {
        await using var baseline = await MigrationBaselineFactory.CreateAtMigration0010Async();
        await baseline.MigrateToLatestAsync();

        await using var ctx = baseline.OpenContext();
        var prefs = await ctx.UserPreferences.ToListAsync(TestContext.Current.CancellationToken);

        // The table must exist (migration 0014 created it) and be empty
        // (no data was present before the migration).
        prefs.Should().BeEmpty(
            because: "UserPreferences table is new in migration 0014 and had no prior rows");
    }

    [Fact]
    public async Task Upgrade_FromMigration0010_ForeignKeyIntegrityMaintained()
    {
        await using var baseline = await MigrationBaselineFactory.CreateAtMigration0010Async();
        await baseline.MigrateToLatestAsync();

        await using var ctx = baseline.OpenContext();

        // Every filing must have a valid TaxpayerProfile
        var filingProfileIds = await ctx.Filings
            .Select(f => f.TaxpayerProfileId)
            .Distinct()
            .ToListAsync(TestContext.Current.CancellationToken);

        var profileIds = await ctx.TaxpayerProfiles
            .Select(p => p.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        filingProfileIds.Should().AllSatisfy(id =>
            profileIds.Should().Contain(id,
                because: "no filing should have an orphaned TaxpayerProfileId after migration"));
    }

    [Fact]
    public async Task Upgrade_FromMigration0010_ExchangeRatePrecisionPreserved()
    {
        await using var baseline = await MigrationBaselineFactory.CreateAtMigration0010Async();
        await baseline.MigrateToLatestAsync();

        await using var ctx = baseline.OpenContext();

        // Spot-check the first EUR rate — seeded as 117.123456 exactly
        var eurRate = await ctx.ExchangeRateCache
            .Where(r => r.Currency == "EUR" && r.Date == new DateOnly(2024, 3, 1))
            .Select(r => r.RateToRsd)
            .SingleAsync(cancellationToken: TestContext.Current.CancellationToken);

        eurRate.Should().Be(117.123456m,
            because: "ExchangeRateCache.RateToRsd uses decimal(18,6) precision and must survive migration");
    }

    // ── Baseline B: migration 0014 → latest ──────────────────────────────────

    /// <summary>
    /// Upgrading from migration 0014 (last April migration) to the latest schema
    /// applies only migration 0011 (ExchangeRateSourceDate/Type columns on Filings).
    /// All existing filings must be preserved and the new columns must be NULL.
    /// </summary>
    [Fact]
    public async Task Upgrade_FromMigration0014_AllFilingsPreservedWithNullNewColumns()
    {
        await using var baseline = await MigrationBaselineFactory.CreateAtMigration0014Async();

        var countBefore = await CountFilingsAsync(baseline);

        await baseline.MigrateToLatestAsync();

        await using var ctx = baseline.OpenContext();
        var filings = await ctx.Filings.ToListAsync(TestContext.Current.CancellationToken);

        filings.Should().HaveCount(countBefore,
            because: "migration 0011 must not delete any existing filing rows");

        filings.Should().AllSatisfy(f =>
        {
            f.ExchangeRateSourceDate.Should().BeNull(
                "migration 0011 adds ExchangeRateSourceDate; existing rows default to NULL");
            f.ExchangeRateSourceType.Should().BeNull(
                "migration 0011 adds ExchangeRateSourceType; existing rows default to NULL");
        });
    }

    [Fact]
    public async Task Upgrade_FromMigration0014_UserPreferencesPreserved()
    {
        await using var baseline = await MigrationBaselineFactory.CreateAtMigration0014Async();
        await baseline.MigrateToLatestAsync();

        await using var ctx = baseline.OpenContext();
        var prefs = await ctx.UserPreferences.ToListAsync(TestContext.Current.CancellationToken);

        prefs.Should().HaveCount(3,
            because: "UserPreferences seeded at baseline 0014 must survive migration 0011 unchanged");

        prefs.Should().ContainSingle(p => p.Key == "Language" && p.Value == "sr-Latn");
        prefs.Should().ContainSingle(p => p.Key == "Theme" && p.Value == "Dark");
    }

    [Fact]
    public async Task Upgrade_FromMigration0014_MailboxCursorBackingFieldsIntact()
    {
        await using var baseline = await MigrationBaselineFactory.CreateAtMigration0014Async();
        await baseline.MigrateToLatestAsync();

        await using var ctx = baseline.OpenContext();
        var mailbox = await ctx.Mailboxes.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);

        // The cursor is a discriminated union backed by two private fields.
        // After migration, the SyncedTo variant must be restored correctly.
        mailbox.Cursor.Should().BeOfType<Domain.ValueObjects.MailboxCursor.SyncedTo>(
            because: "mailbox was seeded with a SyncedTo cursor; it must survive migration intact");

        var synced = (Domain.ValueObjects.MailboxCursor.SyncedTo)mailbox.Cursor;
        synced.Date.Should().Be(new DateOnly(2024, 1, 1));
        synced.Uid.Should().Be(42L);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<int> CountFilingsAsync(MigrationBaselineFactory baseline)
    {
        await using var ctx = baseline.OpenContext();
        return await ctx.Filings.CountAsync(TestContext.Current.CancellationToken);
    }
}
