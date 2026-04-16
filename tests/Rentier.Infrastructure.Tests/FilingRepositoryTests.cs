using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Application.Enums;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Xunit;

namespace Rentier.Infrastructure.Tests;

[Trait("Category", "Integration")]
public class FilingRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private FilingRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new FilingRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static TaxpayerProfile MakeProfile()
        => new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "John Doe", "Belgrade", "11001");

    private static Filing MakeFiling(Guid profileId, string entity = "ACME", DateOnly? date = null, decimal gross = 1000m)
    {
        var d = date ?? new DateOnly(2024, 6, 15);
        return Filing.CreateFromIncome(profileId, IncomeType.Dividend, entity, d,
            gross, 150m, 150m, 0m, d.AddDays(30));
    }

    [Fact]
    public async Task AddAsync_ValidFiling_PersistedInDb()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var filing = MakeFiling(profile.Id);
        await _repository.AddAsync(filing);

        var all = await _repository.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Id.Should().Be(filing.Id);
        all[0].PayingEntity.Should().Be("ACME");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsFiling()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var filing = MakeFiling(profile.Id);
        await _repository.AddAsync(filing);

        var result = await _repository.GetByIdAsync(filing.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(filing.Id);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsByIncomeAsync_Existing_ReturnsTrue()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var date = new DateOnly(2024, 6, 15);
        var filing = MakeFiling(profile.Id, "ACME", date, 1000m);
        await _repository.AddAsync(filing);

        var exists = await _repository.ExistsByIncomeAsync(profile.Id, "ACME", date, filing.GrossIncomeRsd);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByIncomeAsync_Missing_ReturnsFalse()
    {
        var exists = await _repository.ExistsByIncomeAsync(Guid.NewGuid(), "ACME", new DateOnly(2024, 1, 1), 999m);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetByReportIdAsync_WithMatchingReport_ReturnsFiling()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var importer = Importer.Create("Test");
        await _context.Importers.AddAsync(importer);
        await _context.SaveChangesAsync();

        var report = Report.Create(importer.Id, "report.csv", null, null);
        await _context.Reports.AddAsync(report);
        await _context.SaveChangesAsync();

        var filing = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "ACME",
            new DateOnly(2024, 6, 15), 1000m, 0m, 150m, 150m, new DateOnly(2024, 7, 15), report.Id);
        await _repository.AddAsync(filing);

        var results = await _repository.GetByReportIdAsync(report.Id);
        results.Should().HaveCount(1);
        results[0].ReportId.Should().Be(report.Id);
    }

    [Fact]
    public async Task GetByReportIdAsync_NoMatch_ReturnsEmpty()
    {
        var results = await _repository.GetByReportIdAsync(Guid.NewGuid());
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ModifiedFiling_PersistsChanges()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var filing = MakeFiling(profile.Id);
        await _repository.AddAsync(filing);

        filing.AdvanceStatus(FilingStatus.Filed);
        await _repository.UpdateAsync(filing);

        var retrieved = await _repository.GetByIdAsync(filing.Id);
        retrieved!.Status.Should().Be(FilingStatus.Filed);
    }

    [Fact]
    public async Task DeleteAsync_ExistingFiling_RemovesFromDb()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var filing = MakeFiling(profile.Id);
        await _repository.AddAsync(filing);

        await _repository.DeleteAsync(filing.Id);

        var all = await _repository.GetAllAsync();
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_IsNoOp()
    {
        var act = async () => await _repository.DeleteAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    // ---- GetPagedAsync tests ----

    [Fact]
    public async Task GetPagedAsync_UnpaidFilter_ReturnsOnlyInitAndFiledFilings()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var init   = MakeFiling(profile.Id, date: new DateOnly(2024, 1, 1));
        var filed  = MakeFiling(profile.Id, date: new DateOnly(2024, 2, 1));
        var paid   = MakeFiling(profile.Id, date: new DateOnly(2024, 3, 1));

        filed.AdvanceStatus(FilingStatus.Filed);
        paid.AdvanceStatus(FilingStatus.Filed);
        paid.AdvanceStatus(FilingStatus.Paid);

        await _repository.AddAsync(init);
        await _repository.AddAsync(filed);
        await _repository.AddAsync(paid);

        var (items, total) = await _repository.GetPagedAsync(FilingFilterMode.Unpaid, 0, 100);

        items.Should().HaveCount(2);
        total.Should().Be(2);
        items.Should().NotContain(f => f.Status == FilingStatus.Paid);
    }

    [Fact]
    public async Task GetPagedAsync_AllFilter_ReturnsAllFilings()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var init  = MakeFiling(profile.Id, date: new DateOnly(2024, 1, 1));
        var filed = MakeFiling(profile.Id, date: new DateOnly(2024, 2, 1));
        var paid  = MakeFiling(profile.Id, date: new DateOnly(2024, 3, 1));

        filed.AdvanceStatus(FilingStatus.Filed);
        paid.AdvanceStatus(FilingStatus.Filed);
        paid.AdvanceStatus(FilingStatus.Paid);

        await _repository.AddAsync(init);
        await _repository.AddAsync(filed);
        await _repository.AddAsync(paid);

        var (items, total) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 100);

        items.Should().HaveCount(3);
        total.Should().Be(3);
    }

    [Fact]
    public async Task GetPagedAsync_DefaultSort_ReturnsFilingDeadlineDescending()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var later   = MakeFiling(profile.Id, date: new DateOnly(2024, 6, 1));
        var earlier = MakeFiling(profile.Id, date: new DateOnly(2024, 1, 1));
        var middle  = MakeFiling(profile.Id, date: new DateOnly(2024, 3, 1));

        await _repository.AddAsync(later);
        await _repository.AddAsync(earlier);
        await _repository.AddAsync(middle);

        var (items, _) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 100);

        // Default: FilingDeadline DESC
        items[0].FilingDeadline.Should().BeAfter(items[1].FilingDeadline);
        items[1].FilingDeadline.Should().BeAfter(items[2].FilingDeadline);
    }

    [Fact]
    public async Task GetPagedAsync_Pagination_SkipsAndTakesCorrectly()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        for (var i = 1; i <= 5; i++)
            await _repository.AddAsync(MakeFiling(profile.Id, date: new DateOnly(2024, i, 1)));

        // skip 2, take 2 => items 3 and 4
        var (items, _) = await _repository.GetPagedAsync(FilingFilterMode.All, 2, 2);

        items.Should().HaveCount(2);
        items[0].FilingDeadline.Should().Be(new DateOnly(2024, 3, 1).AddDays(30));
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsTotalCountBeforePaging()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        for (var i = 1; i <= 5; i++)
            await _repository.AddAsync(MakeFiling(profile.Id, date: new DateOnly(2024, i, 1)));

        // take only 2, but total should be 5
        var (items, total) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 2);

        items.Should().HaveCount(2);
        total.Should().Be(5);
    }

    // ── GetPagedAsync sort tests (feature 027) ──────────────────────────────

    [Fact]
    public async Task GetPagedAsync_SortByFilingDeadlineDescending_MostRecentFirst()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var f1 = MakeFiling(profile.Id, date: new DateOnly(2024, 1, 1));
        var f2 = MakeFiling(profile.Id, date: new DateOnly(2024, 6, 1));
        await _repository.AddAsync(f1);
        await _repository.AddAsync(f2);

        var (items, _) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 100,
            FilingSortColumn.FilingDeadline, sortDescending: true);

        items[0].FilingDeadline.Should().BeAfter(items[1].FilingDeadline);
    }

    [Fact]
    public async Task GetPagedAsync_SortByFilingDeadlineAscending_EarliestFirst()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var f1 = MakeFiling(profile.Id, date: new DateOnly(2024, 6, 1));
        var f2 = MakeFiling(profile.Id, date: new DateOnly(2024, 1, 1));
        await _repository.AddAsync(f1);
        await _repository.AddAsync(f2);

        var (items, _) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 100,
            FilingSortColumn.FilingDeadline, sortDescending: false);

        items[0].FilingDeadline.Should().BeBefore(items[1].FilingDeadline);
    }

    [Fact]
    public async Task GetPagedAsync_SortByPayingEntityDescending_OrdersAlphabeticallyDescending()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        var fA = MakeFiling(profile.Id, entity: "Alpha Corp");
        var fZ = MakeFiling(profile.Id, entity: "Zeta Corp");
        await _repository.AddAsync(fA);
        await _repository.AddAsync(fZ);

        var (items, _) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 100,
            FilingSortColumn.PayingEntity, sortDescending: true);

        items[0].PayingEntity.Should().Be("Zeta Corp");
        items[1].PayingEntity.Should().Be("Alpha Corp");
    }

    [Fact]
    public async Task GetPagedAsync_SortByTaxPayableDescending_HighestFirst()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        // gross=500 => taxPayable is lower; gross=2000 => higher
        var fLow  = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "Low",
            new DateOnly(2024, 1, 1), 500m, 75m, 75m, 0m, new DateOnly(2024, 2, 1));
        var fHigh = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "High",
            new DateOnly(2024, 1, 1), 2000m, 300m, 300m, 0m, new DateOnly(2024, 2, 1));
        await _repository.AddAsync(fLow);
        await _repository.AddAsync(fHigh);

        var (items, _) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 100,
            FilingSortColumn.TaxPayable, sortDescending: true);

        items[0].TaxPayableRsd.Should().BeGreaterThan(items[1].TaxPayableRsd);
    }

    [Fact]
    public async Task GetPagedAsync_TieBreaker_DuplicatePrimarySort_SecondaryIdAscIsApplied()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        // Two filings with the same deadline: tie-breaker should be Id ASC
        var f1 = MakeFiling(profile.Id, date: new DateOnly(2024, 3, 1));
        var f2 = MakeFiling(profile.Id, date: new DateOnly(2024, 3, 1));
        await _repository.AddAsync(f1);
        await _repository.AddAsync(f2);

        var (items, _) = await _repository.GetPagedAsync(
            FilingFilterMode.All, 0, 100,
            FilingSortColumn.FilingDeadline, sortDescending: true);

        items.Should().HaveCount(2);
        // Tie-breaker: Id ASC (lower GUID first when deadlines are equal)
        var ids = items.Select(i => i.Id).ToList();
        var sorted = ids.OrderBy(id => id).ToList();
        ids.Should().Equal(sorted);
    }

    // ── GetFilingCountByReportIdAsync tests (feature 014) ───────────────────

    private async Task<(Importer importer, Report report, TaxpayerProfile profile)> SeedReportAsync()
    {
        var importer = Importer.Create("Test Importer");
        await _context.Importers.AddAsync(importer);
        await _context.SaveChangesAsync();

        var report = Report.Create(importer.Id, "stmt.csv", null, null);
        await _context.Reports.AddAsync(report);
        await _context.SaveChangesAsync();

        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile);
        await _context.SaveChangesAsync();

        return (importer, report, profile);
    }

    [Fact]
    public async Task GetFilingCountByReportIdAsync_WhenNoFilingsExist_ReturnsZero()
    {
        var (_, report, _) = await SeedReportAsync();

        var count = await _repository.GetFilingCountByReportIdAsync(report.Id);

        count.Should().Be(0);
    }

    [Fact]
    public async Task GetFilingCountByReportIdAsync_WhenFilingsLinked_ReturnsCorrectCount()
    {
        var (_, report, profile) = await SeedReportAsync();
        for (var i = 0; i < 3; i++)
        {
            var f = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, $"Co{i}",
                new DateOnly(2024, 1 + i, 15), 1000m, 0m, 150m, 150m,
                new DateOnly(2024, 2 + i, 15), report.Id);
            await _repository.AddAsync(f);
        }

        var count = await _repository.GetFilingCountByReportIdAsync(report.Id);

        count.Should().Be(3);
    }

    [Fact]
    public async Task GetFilingCountByReportIdAsync_WithUnknownReportId_ReturnsZero()
    {
        var count = await _repository.GetFilingCountByReportIdAsync(Guid.NewGuid());

        count.Should().Be(0);
    }

    // ── DeleteByReportIdAsync tests (feature 014) ────────────────────────────

    [Fact]
    public async Task DeleteByReportIdAsync_WhenFilingsExist_DeletesAllMatchingFilings()
    {
        var (_, report, profile) = await SeedReportAsync();
        for (var i = 0; i < 3; i++)
        {
            var f = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, $"Co{i}",
                new DateOnly(2024, 1 + i, 15), 1000m, 0m, 150m, 150m,
                new DateOnly(2024, 2 + i, 15), report.Id);
            await _repository.AddAsync(f);
        }

        await _repository.DeleteByReportIdAsync(report.Id);

        var remaining = await _repository.GetByReportIdAsync(report.Id);
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteByReportIdAsync_WhenNoFilingsExist_IsIdempotentAndDoesNotThrow()
    {
        var (_, report, _) = await SeedReportAsync();

        var act = async () => await _repository.DeleteByReportIdAsync(report.Id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteByReportIdAsync_WhenCalledTwice_IsIdempotent()
    {
        var (_, report, profile) = await SeedReportAsync();
        var f = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "ACME",
            new DateOnly(2024, 3, 15), 1000m, 0m, 150m, 150m,
            new DateOnly(2024, 4, 15), report.Id);
        await _repository.AddAsync(f);

        await _repository.DeleteByReportIdAsync(report.Id);
        var act = async () => await _repository.DeleteByReportIdAsync(report.Id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteByReportIdAsync_OnlyDeletesFilingsMatchingReportId()
    {
        var (_, report1, profile) = await SeedReportAsync();

        var importer2 = Importer.Create("Other");
        await _context.Importers.AddAsync(importer2);
        await _context.SaveChangesAsync();
        var report2 = Report.Create(importer2.Id, "other.csv", null, null);
        await _context.Reports.AddAsync(report2);
        await _context.SaveChangesAsync();

        var f1 = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "Co1",
            new DateOnly(2024, 1, 15), 1000m, 0m, 150m, 150m,
            new DateOnly(2024, 2, 15), report1.Id);
        var f2 = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "Co2",
            new DateOnly(2024, 3, 15), 2000m, 0m, 300m, 300m,
            new DateOnly(2024, 4, 15), report2.Id);
        await _repository.AddAsync(f1);
        await _repository.AddAsync(f2);

        await _repository.DeleteByReportIdAsync(report1.Id);

        var remaining = await _repository.GetByReportIdAsync(report2.Id);
        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().Be(f2.Id);
    }
}
