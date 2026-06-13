using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Application.Enums;
using Rentier.Application.Queries;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;

namespace Rentier.Infrastructure.Tests;

[Trait("Category", "Integration")]
public class FilingRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private FilingRepository _repository = null!;

    [Fact]
    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        _repository = new FilingRepository(_context);
    }

    [Fact]
    public async ValueTask DisposeAsync()
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
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var filing = MakeFiling(profile.Id);
        await _repository.AddAsync(filing, TestContext.Current.CancellationToken);

        var all = await _repository.GetAllAsync(TestContext.Current.CancellationToken);
        all.Should().HaveCount(1);
        all[0].Id.Should().Be(filing.Id);
        all[0].PayingEntity.Should().Be("ACME");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsFiling()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var filing = MakeFiling(profile.Id);
        await _repository.AddAsync(filing, TestContext.Current.CancellationToken);

        var result = await _repository.GetByIdAsync(filing.Id, TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Id.Should().Be(filing.Id);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsByIncomeAsync_Existing_ReturnsTrue()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var date = new DateOnly(2024, 6, 15);
        var filing = MakeFiling(profile.Id, "ACME", date, 1000m);
        await _repository.AddAsync(filing, TestContext.Current.CancellationToken);

        var exists = await _repository.ExistsByIncomeAsync(profile.Id, "ACME", date, filing.GrossIncomeRsd, TestContext.Current.CancellationToken);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByIncomeAsync_Missing_ReturnsFalse()
    {
        var exists = await _repository.ExistsByIncomeAsync(Guid.NewGuid(), "ACME", new DateOnly(2024, 1, 1), 999m, TestContext.Current.CancellationToken);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetByReportIdAsync_WithMatchingReport_ReturnsFiling()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var importer = Importer.Create("Test");
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = Report.Create(importer.Id, "report.csv", null, null);
        await _context.Reports.AddAsync(report, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var filing = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "ACME",
            new DateOnly(2024, 6, 15), 1000m, 0m, 150m, 150m, new DateOnly(2024, 7, 15), report.Id);
        await _repository.AddAsync(filing, TestContext.Current.CancellationToken);

        var results = await _repository.GetByReportIdAsync(report.Id, TestContext.Current.CancellationToken);
        results.Should().HaveCount(1);
        results[0].ReportId.Should().Be(report.Id);
    }

    [Fact]
    public async Task GetByReportIdAsync_NoMatch_ReturnsEmpty()
    {
        var results = await _repository.GetByReportIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ModifiedFiling_PersistsChanges()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var filing = MakeFiling(profile.Id);
        await _repository.AddAsync(filing, TestContext.Current.CancellationToken);

        filing.AdvanceStatus(FilingStatus.Filed);
        await _repository.UpdateAsync(filing, TestContext.Current.CancellationToken);

        var retrieved = await _repository.GetByIdAsync(filing.Id, TestContext.Current.CancellationToken);
        retrieved!.Status.Should().Be(FilingStatus.Filed);
    }

    [Fact]
    public async Task DeleteAsync_ExistingFiling_RemovesFromDb()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var filing = MakeFiling(profile.Id);
        await _repository.AddAsync(filing, TestContext.Current.CancellationToken);

        await _repository.DeleteAsync(filing.Id, TestContext.Current.CancellationToken);

        var all = await _repository.GetAllAsync(TestContext.Current.CancellationToken);
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
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var init = MakeFiling(profile.Id, date: new DateOnly(2024, 1, 1));
        var filed = MakeFiling(profile.Id, date: new DateOnly(2024, 2, 1));
        var paid = MakeFiling(profile.Id, date: new DateOnly(2024, 3, 1));

        filed.AdvanceStatus(FilingStatus.Filed);
        paid.AdvanceStatus(FilingStatus.Filed);
        paid.AdvanceStatus(FilingStatus.Paid);

        await _repository.AddAsync(init, TestContext.Current.CancellationToken);
        await _repository.AddAsync(filed, TestContext.Current.CancellationToken);
        await _repository.AddAsync(paid, TestContext.Current.CancellationToken);

        var (items, total) = await _repository.GetPagedAsync(FilingFilterMode.Unpaid, 0, 100, ct: TestContext.Current.CancellationToken);

        items.Should().HaveCount(2);
        total.Should().Be(2);
        items.Should().NotContain(f => f.Status == FilingStatus.Paid);
    }

    [Fact]
    public async Task GetPagedAsync_AllFilter_ReturnsAllFilings()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var init = MakeFiling(profile.Id, date: new DateOnly(2024, 1, 1));
        var filed = MakeFiling(profile.Id, date: new DateOnly(2024, 2, 1));
        var paid = MakeFiling(profile.Id, date: new DateOnly(2024, 3, 1));

        filed.AdvanceStatus(FilingStatus.Filed);
        paid.AdvanceStatus(FilingStatus.Filed);
        paid.AdvanceStatus(FilingStatus.Paid);

        await _repository.AddAsync(init, TestContext.Current.CancellationToken);
        await _repository.AddAsync(filed, TestContext.Current.CancellationToken);
        await _repository.AddAsync(paid, TestContext.Current.CancellationToken);

        var (items, total) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 100, ct: TestContext.Current.CancellationToken);

        items.Should().HaveCount(3);
        total.Should().Be(3);
    }

    [Fact]
    public async Task GetPagedAsync_DefaultSort_ReturnsFilingDeadlineDescending()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var later = MakeFiling(profile.Id, date: new DateOnly(2024, 6, 1));
        var earlier = MakeFiling(profile.Id, date: new DateOnly(2024, 1, 1));
        var middle = MakeFiling(profile.Id, date: new DateOnly(2024, 3, 1));

        await _repository.AddAsync(later, TestContext.Current.CancellationToken);
        await _repository.AddAsync(earlier, TestContext.Current.CancellationToken);
        await _repository.AddAsync(middle, TestContext.Current.CancellationToken);

        var (items, _) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 100, ct: TestContext.Current.CancellationToken);

        // Default: FilingDeadline DESC
        items[0].FilingDeadline.Should().BeAfter(items[1].FilingDeadline);
        items[1].FilingDeadline.Should().BeAfter(items[2].FilingDeadline);
    }

    [Fact]
    public async Task GetPagedAsync_Pagination_SkipsAndTakesCorrectly()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        for (var i = 1; i <= 5; i++)
            await _repository.AddAsync(MakeFiling(profile.Id, date: new DateOnly(2024, i, 1)), TestContext.Current.CancellationToken);

        // skip 2, take 2 => items 3 and 4
        var (items, _) = await _repository.GetPagedAsync(FilingFilterMode.All, 2, 2, ct: TestContext.Current.CancellationToken);

        items.Should().HaveCount(2);
        items[0].FilingDeadline.Should().Be(new DateOnly(2024, 3, 1).AddDays(30));
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsTotalCountBeforePaging()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        for (var i = 1; i <= 5; i++)
            await _repository.AddAsync(MakeFiling(profile.Id, date: new DateOnly(2024, i, 1)), TestContext.Current.CancellationToken);

        // take only 2, but total should be 5
        var (items, total) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 2, ct: TestContext.Current.CancellationToken);

        items.Should().HaveCount(2);
        total.Should().Be(5);
    }

    // ── GetPagedAsync sort tests (feature 027) ──────────────────────────────

    [Fact]
    public async Task GetPagedAsync_SortByFilingDeadlineDescending_MostRecentFirst()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var f1 = MakeFiling(profile.Id, date: new DateOnly(2024, 1, 1));
        var f2 = MakeFiling(profile.Id, date: new DateOnly(2024, 6, 1));
        await _repository.AddAsync(f1, TestContext.Current.CancellationToken);
        await _repository.AddAsync(f2, TestContext.Current.CancellationToken);

        var (items, _) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 100, FilingSortColumn.FilingDeadline, sortDescending: true, ct: TestContext.Current.CancellationToken);

        items[0].FilingDeadline.Should().BeAfter(items[1].FilingDeadline);
    }

    [Fact]
    public async Task GetPagedAsync_SortByFilingDeadlineAscending_EarliestFirst()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var f1 = MakeFiling(profile.Id, date: new DateOnly(2024, 6, 1));
        var f2 = MakeFiling(profile.Id, date: new DateOnly(2024, 1, 1));
        await _repository.AddAsync(f1, TestContext.Current.CancellationToken);
        await _repository.AddAsync(f2, TestContext.Current.CancellationToken);

        var (items, _) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 100, FilingSortColumn.FilingDeadline, sortDescending: false, ct: TestContext.Current.CancellationToken);

        items[0].FilingDeadline.Should().BeBefore(items[1].FilingDeadline);
    }

    [Fact]
    public async Task GetPagedAsync_SortByPayingEntityDescending_OrdersAlphabeticallyDescending()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var fA = MakeFiling(profile.Id, entity: "Alpha Corp");
        var fZ = MakeFiling(profile.Id, entity: "Zeta Corp");
        await _repository.AddAsync(fA, TestContext.Current.CancellationToken);
        await _repository.AddAsync(fZ, TestContext.Current.CancellationToken);

        var (items, _) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 100, FilingSortColumn.PayingEntity, sortDescending: true, ct: TestContext.Current.CancellationToken);

        items[0].PayingEntity.Should().Be("Zeta Corp");
        items[1].PayingEntity.Should().Be("Alpha Corp");
    }

    [Fact]
    public async Task GetPagedAsync_SortByStatusDescending_PaidFirst()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var fInit = MakeFiling(profile.Id, date: new DateOnly(2024, 1, 1));
        var fFiled = MakeFiling(profile.Id, date: new DateOnly(2024, 2, 1));
        var fPaid = MakeFiling(profile.Id, date: new DateOnly(2024, 3, 1));
        fFiled.AdvanceStatus(FilingStatus.Filed);
        fPaid.AdvanceStatus(FilingStatus.Filed);
        fPaid.AdvanceStatus(FilingStatus.Paid);
        await _repository.AddAsync(fInit, TestContext.Current.CancellationToken);
        await _repository.AddAsync(fFiled, TestContext.Current.CancellationToken);
        await _repository.AddAsync(fPaid, TestContext.Current.CancellationToken);

        var (items, _) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 100, FilingSortColumn.Status, sortDescending: true, ct: TestContext.Current.CancellationToken);

        // Descending: Paid (2) → Filed (1) → Init (0)
        items[0].Status.Should().Be(FilingStatus.Paid);
        items[2].Status.Should().Be(FilingStatus.Init);
    }

    [Fact]
    public async Task GetPagedAsync_TieBreaker_DuplicatePrimarySort_SecondaryIdAscIsApplied()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Two filings with the same deadline: tie-breaker should be Id ASC
        var f1 = MakeFiling(profile.Id, date: new DateOnly(2024, 3, 1));
        var f2 = MakeFiling(profile.Id, date: new DateOnly(2024, 3, 1));
        await _repository.AddAsync(f1, TestContext.Current.CancellationToken);
        await _repository.AddAsync(f2, TestContext.Current.CancellationToken);

        var (items, _) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 100, FilingSortColumn.FilingDeadline, sortDescending: true, ct: TestContext.Current.CancellationToken);

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
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = Report.Create(importer.Id, "stmt.csv", null, null);
        await _context.Reports.AddAsync(report);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (importer, report, profile);
    }

    [Fact]
    public async Task GetFilingCountByReportIdAsync_WhenNoFilingsExist_ReturnsZero()
    {
        var (_, report, _) = await SeedReportAsync();

        var count = await _repository.GetFilingCountByReportIdAsync(report.Id, TestContext.Current.CancellationToken);

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
            await _repository.AddAsync(f, TestContext.Current.CancellationToken);
        }

        var count = await _repository.GetFilingCountByReportIdAsync(report.Id, TestContext.Current.CancellationToken);

        count.Should().Be(3);
    }

    [Fact]
    public async Task GetFilingCountByReportIdAsync_WithUnknownReportId_ReturnsZero()
    {
        var count = await _repository.GetFilingCountByReportIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

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
            await _repository.AddAsync(f, TestContext.Current.CancellationToken);
        }

        await _repository.DeleteByReportIdAsync(report.Id, TestContext.Current.CancellationToken);

        var remaining = await _repository.GetByReportIdAsync(report.Id, TestContext.Current.CancellationToken);
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
        await _repository.AddAsync(f, TestContext.Current.CancellationToken);

        await _repository.DeleteByReportIdAsync(report.Id, TestContext.Current.CancellationToken);
        var act = async () => await _repository.DeleteByReportIdAsync(report.Id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteByReportIdAsync_OnlyDeletesFilingsMatchingReportId()
    {
        var (_, report1, profile) = await SeedReportAsync();

        var importer2 = Importer.Create("Other");
        await _context.Importers.AddAsync(importer2, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var report2 = Report.Create(importer2.Id, "other.csv", null, null);
        await _context.Reports.AddAsync(report2, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var f1 = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "Co1",
            new DateOnly(2024, 1, 15), 1000m, 0m, 150m, 150m,
            new DateOnly(2024, 2, 15), report1.Id);
        var f2 = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "Co2",
            new DateOnly(2024, 3, 15), 2000m, 0m, 300m, 300m,
            new DateOnly(2024, 4, 15), report2.Id);
        await _repository.AddAsync(f1, TestContext.Current.CancellationToken);
        await _repository.AddAsync(f2, TestContext.Current.CancellationToken);

        await _repository.DeleteByReportIdAsync(report1.Id, TestContext.Current.CancellationToken);

        var remaining = await _repository.GetByReportIdAsync(report2.Id, TestContext.Current.CancellationToken);
        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().Be(f2.Id);
    }

    // ── GetByTaxPeriodAsync tests ────────────────────────────────────────────

    [Fact]
    public async Task GetByTaxPeriodAsync_WithMatchingFiling_ReturnsIt()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // TaxPeriod is derived from incomeDate in CreateFromIncome
        var incomeDate = new DateOnly(2024, 6, 15);
        var filing = MakeFiling(profile.Id, date: incomeDate);
        await _repository.AddAsync(filing, TestContext.Current.CancellationToken);

        var result = await _repository.GetByTaxPeriodAsync(profile.Id, incomeDate, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Id.Should().Be(filing.Id);
    }

    [Fact]
    public async Task GetByTaxPeriodAsync_WithNoMatch_ReturnsNull()
    {
        var result = await _repository.GetByTaxPeriodAsync(Guid.NewGuid(), new DateOnly(2024, 6, 15), TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByTaxPeriodAsync_MatchesByProfileIdAndTaxPeriod()
    {
        // Two profiles with filings on the same date — each should only see their own
        var profile1 = new TaxpayerProfile(Guid.NewGuid(), "1111111111111", "Alice", "Belgrade", "11001");
        var profile2 = new TaxpayerProfile(Guid.NewGuid(), "2222222222222", "Bob", "Novi Sad", "21000");
        await _context.TaxpayerProfiles.AddRangeAsync(profile1, profile2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var incomeDate = new DateOnly(2024, 6, 15);
        var f1 = MakeFiling(profile1.Id, date: incomeDate);
        var f2 = MakeFiling(profile2.Id, date: incomeDate);
        await _repository.AddAsync(f1, TestContext.Current.CancellationToken);
        await _repository.AddAsync(f2, TestContext.Current.CancellationToken);

        var result = await _repository.GetByTaxPeriodAsync(profile1.Id, incomeDate, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Id.Should().Be(f1.Id);
    }

    // ── GetEarliestIncomeDateByReportIdAsync tests ───────────────────────────

    [Fact]
    public async Task GetEarliestIncomeDateByReportIdAsync_WithMultipleFilings_ReturnsEarliest()
    {
        var (_, report, profile) = await SeedReportAsync();

        var dates = new[] { new DateOnly(2024, 6, 1), new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 1) };
        foreach (var d in dates)
        {
            var f = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, $"Co-{d.Month}",
                d, 1000m, 0m, 150m, 150m, d.AddDays(30), report.Id);
            await _repository.AddAsync(f, TestContext.Current.CancellationToken);
        }

        var result = await _repository.GetEarliestIncomeDateByReportIdAsync(report.Id, TestContext.Current.CancellationToken);

        result.Should().Be(new DateOnly(2024, 1, 1));
    }

    [Fact]
    public async Task GetEarliestIncomeDateByReportIdAsync_WithNoFilings_ReturnsNull()
    {
        var result = await _repository.GetEarliestIncomeDateByReportIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    // ── DeleteManyAsync tests ────────────────────────────────────────────────

    [Fact]
    public async Task DeleteManyAsync_WithMultipleIds_RemovesAll()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var f1 = MakeFiling(profile.Id, date: new DateOnly(2024, 1, 1));
        var f2 = MakeFiling(profile.Id, date: new DateOnly(2024, 2, 1));
        var f3 = MakeFiling(profile.Id, date: new DateOnly(2024, 3, 1));
        await _repository.AddAsync(f1, TestContext.Current.CancellationToken);
        await _repository.AddAsync(f2, TestContext.Current.CancellationToken);
        await _repository.AddAsync(f3, TestContext.Current.CancellationToken);

        await _repository.DeleteManyAsync([f1.Id, f2.Id], TestContext.Current.CancellationToken);

        var remaining = await _repository.GetAllAsync(TestContext.Current.CancellationToken);
        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().Be(f3.Id);
    }

    [Fact]
    public async Task DeleteManyAsync_WithEmptyList_NoException()
    {
        var act = async () => await _repository.DeleteManyAsync([]);

        await act.Should().NotThrowAsync();
    }

    // ── GetPagedAsync additional sort column tests ───────────────────────────

    [Fact]
    public async Task GetPagedAsync_SortByIncomeType_ReturnsCorrectOrder()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Dividend = 0, Interest = 1; descending puts Interest first
        var fDividend = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "DivCo",
            new DateOnly(2024, 1, 1), 1000m, 0m, 150m, 150m, new DateOnly(2024, 2, 1));
        var fInterest = Filing.CreateFromIncome(profile.Id, IncomeType.Interest, "IntCo",
            new DateOnly(2024, 2, 1), 1000m, 0m, 150m, 150m, new DateOnly(2024, 3, 1));
        await _repository.AddAsync(fDividend, TestContext.Current.CancellationToken);
        await _repository.AddAsync(fInterest, TestContext.Current.CancellationToken);

        var (items, _) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 100, FilingSortColumn.IncomeType, sortDescending: true, ct: TestContext.Current.CancellationToken);

        items[0].IncomeType.Should().Be(IncomeType.Interest);
        items[1].IncomeType.Should().Be(IncomeType.Dividend);
    }

    [Fact]
    public async Task GetPagedAsync_SortByTaxPayable_ReturnsCorrectOrder()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var fLow = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "Co1",
            new DateOnly(2024, 1, 1), 1000m, 0m, 100m, 100m, new DateOnly(2024, 2, 1));
        var fHigh = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "Co2",
            new DateOnly(2024, 2, 1), 2000m, 0m, 400m, 400m, new DateOnly(2024, 3, 1));
        await _repository.AddAsync(fLow, TestContext.Current.CancellationToken);
        await _repository.AddAsync(fHigh, TestContext.Current.CancellationToken);

        var (items, _) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 100, FilingSortColumn.TaxPayable, sortDescending: true, ct: TestContext.Current.CancellationToken);

        items[0].TaxPayableRsd.Should().Be(400m);
        items[1].TaxPayableRsd.Should().Be(100m);
    }

    [Fact]
    public async Task GetPagedAsync_SortByPaymentReference_ReturnsCorrectOrder()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var fAlpha = MakeFiling(profile.Id, date: new DateOnly(2024, 1, 1));
        fAlpha.SetPaymentReference("AAA-001");
        var fZeta = MakeFiling(profile.Id, date: new DateOnly(2024, 2, 1));
        fZeta.SetPaymentReference("ZZZ-999");
        await _repository.AddAsync(fAlpha, TestContext.Current.CancellationToken);
        await _repository.AddAsync(fZeta, TestContext.Current.CancellationToken);

        var (items, _) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 100, FilingSortColumn.PaymentReference, sortDescending: true, ct: TestContext.Current.CancellationToken);

        items[0].PaymentReference.Should().Be("ZZZ-999");
        items[1].PaymentReference.Should().Be("AAA-001");
    }

    // ── GetPagedAsync column filter tests (feature 045) ─────────────────────

    [Fact]
    public async Task GetPagedAsync_FilterByStatus_ReturnsOnlyMatchingFilings()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var init1 = MakeFiling(profile.Id, "CompanyA");
        var init2 = MakeFiling(profile.Id, "CompanyB");
        var filed = MakeFiling(profile.Id, "CompanyC");
        filed.AdvanceStatus(FilingStatus.Filed);
        await _repository.AddAsync(init1, TestContext.Current.CancellationToken);
        await _repository.AddAsync(init2, TestContext.Current.CancellationToken);
        await _repository.AddAsync(filed, TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(Status: FilingStatus.Init);
        var (items, total) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(2);
        items.Should().AllSatisfy(f => f.Status.Should().Be(FilingStatus.Init));
    }

    [Fact]
    public async Task GetPagedAsync_FilterByIncomeType_ReturnsOnlyMatchingFilings()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var div = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "DivCo",
            new DateOnly(2024, 3, 1), 1000m, 150m, 150m, 0m, new DateOnly(2024, 6, 15));
        var interest = Filing.CreateFromIncome(profile.Id, IncomeType.Interest, "IntCo",
            new DateOnly(2024, 3, 2), 500m, 75m, 75m, 0m, new DateOnly(2024, 6, 16));
        await _repository.AddAsync(div, TestContext.Current.CancellationToken);
        await _repository.AddAsync(interest, TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(IncomeType: IncomeType.Dividend);
        var (items, total) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(1);
        items[0].PayingEntity.Should().Be("DivCo");
    }

    [Fact]
    public async Task GetPagedAsync_FilterByPayingEntityContains_CaseInsensitive()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _repository.AddAsync(MakeFiling(profile.Id, "ACME Corporation"), TestContext.Current.CancellationToken);
        await _repository.AddAsync(MakeFiling(profile.Id, "Globex Corp"), TestContext.Current.CancellationToken);
        await _repository.AddAsync(MakeFiling(profile.Id, "Initech Ltd"), TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(PayingEntity: "corp");
        var (items, total) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(2);
        items.Should().Contain(f => f.PayingEntity == "ACME Corporation");
        items.Should().Contain(f => f.PayingEntity == "Globex Corp");
    }

    [Fact]
    public async Task GetPagedAsync_FilterByFilingDeadlineExact()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var target = new DateOnly(2024, 7, 15);
        await _repository.AddAsync(MakeFiling(profile.Id, "A", target), TestContext.Current.CancellationToken);
        await _repository.AddAsync(MakeFiling(profile.Id, "B", new DateOnly(2024, 8, 15)), TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(FilingDeadline: target.AddDays(30));
        var (items, total) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(1);
        items[0].FilingDeadline.Should().Be(target.AddDays(30));
    }

    [Fact]
    public async Task GetPagedAsync_CombinedFilter_StatusAndIncomeType()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var f1 = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "DivInit",
            new DateOnly(2024, 3, 1), 1000m, 150m, 150m, 0m, new DateOnly(2024, 6, 15));
        var f2 = Filing.CreateFromIncome(profile.Id, IncomeType.Interest, "IntInit",
            new DateOnly(2024, 3, 2), 500m, 75m, 75m, 0m, new DateOnly(2024, 6, 16));
        var f3 = Filing.CreateFromIncome(profile.Id, IncomeType.Dividend, "DivFiled",
            new DateOnly(2024, 3, 3), 800m, 120m, 120m, 0m, new DateOnly(2024, 6, 17));
        f3.AdvanceStatus(FilingStatus.Filed);
        await _repository.AddAsync(f1, TestContext.Current.CancellationToken);
        await _repository.AddAsync(f2, TestContext.Current.CancellationToken);
        await _repository.AddAsync(f3, TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(Status: FilingStatus.Init, IncomeType: IncomeType.Dividend);
        var (items, total) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(1);
        items[0].PayingEntity.Should().Be("DivInit");
    }

    [Fact]
    public async Task GetPagedAsync_FilterWithNoMatch_ReturnsEmpty()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _repository.AddAsync(MakeFiling(profile.Id, "ACME"), TestContext.Current.CancellationToken);

        var cf = new FilingColumnFilter(PayingEntity: "XYZ_NOT_FOUND");
        var (items, total) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 30, columnFilter: cf, ct: TestContext.Current.CancellationToken);

        total.Should().Be(0);
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_NullColumnFilter_ReturnsSameResultsAsNoFilter()
    {
        var profile = MakeProfile();
        await _context.TaxpayerProfiles.AddAsync(profile, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _repository.AddAsync(MakeFiling(profile.Id, "A"), TestContext.Current.CancellationToken);
        await _repository.AddAsync(MakeFiling(profile.Id, "B"), TestContext.Current.CancellationToken);

        var (items, total) = await _repository.GetPagedAsync(FilingFilterMode.All, 0, 30, columnFilter: null, ct: TestContext.Current.CancellationToken);

        total.Should().Be(2);
        items.Should().HaveCount(2);
    }
}
