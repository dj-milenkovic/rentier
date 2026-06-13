using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Application.DTOs;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Xunit;

namespace Rentier.Infrastructure.Tests;

[Trait("Category", "Integration")]
public class ReportRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private ReportRepository _repository = null!;

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
        _repository = new ReportRepository(_context);
    }

    [Fact]
    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static Importer MakeImporter()
        => Importer.Create("Test Importer");

    private static Report MakeReport(Guid importerId, string name = "report.csv")
        => Report.Create(importerId, name, [1, 2, 3], 100L);

    [Fact]
    public async Task AddAsync_ValidReport_PersistedInDb()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = MakeReport(importer.Id);
        await _repository.AddAsync(report, TestContext.Current.CancellationToken);

        var all = await _repository.GetAllAsync(ct: TestContext.Current.CancellationToken);
        all.Should().HaveCount(1);
        all[0].Id.Should().Be(report.Id);
        all[0].ReportName.Should().Be("report.csv");
    }

    [Fact]
    public async Task GetAllAsync_DefaultSortDescending_OrdersByLatestEmailDateFirst()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var older = Report.Create(importer.Id, "older.csv", [1], 100L, new DateOnly(2024, 1, 15));
        var newer = Report.Create(importer.Id, "newer.csv", [1], 101L, new DateOnly(2024, 3, 15));

        await _repository.AddAsync(older, TestContext.Current.CancellationToken);
        await _repository.AddAsync(newer, TestContext.Current.CancellationToken);

        var reports = await _repository.GetAllAsync(ct: TestContext.Current.CancellationToken);

        reports.Select(report => report.ReportName).Should().ContainInOrder("newer.csv", "older.csv");
    }

    [Fact]
    public async Task GetByStatusAsync_InitStatus_ReturnsMatchingReports()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var r1 = MakeReport(importer.Id, "init.csv");
        var r2 = MakeReport(importer.Id, "processed.csv");
        r2.SetStatus(ReportStatus.Processed);

        await _repository.AddAsync(r1, TestContext.Current.CancellationToken);
        await _repository.AddAsync(r2, TestContext.Current.CancellationToken);

        var result = await _repository.GetByStatusAsync(ReportStatus.Init, TestContext.Current.CancellationToken);
        result.Should().HaveCount(1);
        result[0].ReportName.Should().Be("init.csv");
    }

    [Fact]
    public async Task ExistsByImporterAndNameAsync_Existing_ReturnsTrue()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = MakeReport(importer.Id, "exists.csv");
        await _repository.AddAsync(report, TestContext.Current.CancellationToken);

        var exists = await _repository.ExistsByImporterAndNameAsync(importer.Id, "exists.csv", TestContext.Current.CancellationToken);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByImporterAndNameAsync_Missing_ReturnsFalse()
    {
        var exists = await _repository.ExistsByImporterAndNameAsync(Guid.NewGuid(), "nonexistent.csv", TestContext.Current.CancellationToken);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_DuplicateImporterAndName_ThrowsDbUpdateException()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _repository.AddAsync(MakeReport(importer.Id, "dupe.csv"), TestContext.Current.CancellationToken);

        var act = async () => await _repository.AddAsync(MakeReport(importer.Id, "dupe.csv"));
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ── GetByIdAsync tests ───────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsReport()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = MakeReport(importer.Id);
        await _repository.AddAsync(report, TestContext.Current.CancellationToken);

        var result = await _repository.GetByIdAsync(report.Id, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Id.Should().Be(report.Id);
        result.ReportName.Should().Be("report.csv");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    // ── GetByImporterAsync tests ─────────────────────────────────────────────

    [Fact]
    public async Task GetByImporterAsync_WithMatchingReports_ReturnsAll()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _repository.AddAsync(MakeReport(importer.Id, "r1.csv"), TestContext.Current.CancellationToken);
        await _repository.AddAsync(MakeReport(importer.Id, "r2.csv"), TestContext.Current.CancellationToken);

        var result = await _repository.GetByImporterAsync(importer.Id, TestContext.Current.CancellationToken);

        result.Should().HaveCount(2);
        result.Select(r => r.ReportName).Should().BeEquivalentTo(["r1.csv", "r2.csv"]);
    }

    [Fact]
    public async Task GetByImporterAsync_WithNoMatch_ReturnsEmpty()
    {
        var result = await _repository.GetByImporterAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    // ── UpdateAsync tests ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WithExistingReport_PersistsChanges()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = MakeReport(importer.Id);
        await _repository.AddAsync(report, TestContext.Current.CancellationToken);

        report.SetStatus(ReportStatus.Processed);
        await _repository.UpdateAsync(report, TestContext.Current.CancellationToken);

        var result = await _repository.GetByIdAsync(report.Id, TestContext.Current.CancellationToken);
        result!.Status.Should().Be(ReportStatus.Processed);
    }

    // ── DeleteAsync tests ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WithExistingReport_RemovesIt()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var report = MakeReport(importer.Id);
        await _repository.AddAsync(report, TestContext.Current.CancellationToken);

        await _repository.DeleteAsync(report.Id, TestContext.Current.CancellationToken);

        var all = await _repository.GetAllAsync(ct: TestContext.Current.CancellationToken);
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentReport_NoException()
    {
        var act = async () => await _repository.DeleteAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    // ── DeleteManyAsync tests ────────────────────────────────────────────────

    [Fact]
    public async Task DeleteManyAsync_WithMultipleIds_RemovesAll()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var r1 = MakeReport(importer.Id, "r1.csv");
        var r2 = MakeReport(importer.Id, "r2.csv");
        var r3 = MakeReport(importer.Id, "r3.csv");
        await _repository.AddAsync(r1, TestContext.Current.CancellationToken);
        await _repository.AddAsync(r2, TestContext.Current.CancellationToken);
        await _repository.AddAsync(r3, TestContext.Current.CancellationToken);

        await _repository.DeleteManyAsync([r1.Id, r2.Id], TestContext.Current.CancellationToken);

        var remaining = await _repository.GetAllAsync(ct: TestContext.Current.CancellationToken);
        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().Be(r3.Id);
    }

    [Fact]
    public async Task DeleteManyAsync_WithEmptyList_NoException()
    {
        var act = async () => await _repository.DeleteManyAsync([]);

        await act.Should().NotThrowAsync();
    }

    // ── GetPagedAsync tests (feature 047) ─────────────────────────────────────

    [Fact]
    public async Task GetPagedAsync_NoFilter_ReturnsAllRowsPaged()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        for (int i = 0; i < 5; i++)
            await _context.Reports.AddAsync(MakeReport(importer.Id, $"r{i}.csv"), TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (items, total) = await _repository.GetPagedAsync(null, 0, 3, true, TestContext.Current.CancellationToken);

        total.Should().Be(5);
        items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPagedAsync_NameContains_ReturnsMatchingRows()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        await _context.Reports.AddAsync(MakeReport(importer.Id, "ibkr_2024.csv"), TestContext.Current.CancellationToken);
        await _context.Reports.AddAsync(MakeReport(importer.Id, "schwab_2024.csv"), TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var filter = new ReportColumnFilter(NameContains: "ibkr");
        var (items, total) = await _repository.GetPagedAsync(filter, 0, 10, true, TestContext.Current.CancellationToken);

        total.Should().Be(1);
        items.Should().HaveCount(1);
        items[0].ReportName.Should().Be("ibkr_2024.csv");
    }

    [Fact]
    public async Task GetPagedAsync_StatusFilter_ReturnsMatchingRows()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        var r1 = MakeReport(importer.Id, "r1.csv");
        var r2 = MakeReport(importer.Id, "r2.csv");
        await _context.Reports.AddRangeAsync(r1, r2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Update r2 status
        r2.SetStatus(Rentier.Domain.Enums.ReportStatus.Processed);
        _context.Reports.Update(r2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var filter = new ReportColumnFilter(StatusFilters: new HashSet<Rentier.Domain.Enums.ReportStatus> { Rentier.Domain.Enums.ReportStatus.Init });
        var (items, total) = await _repository.GetPagedAsync(filter, 0, 10, true, TestContext.Current.CancellationToken);

        total.Should().Be(1);
        items[0].ReportName.Should().Be("r1.csv");
    }

    [Fact]
    public async Task GetPagedAsync_SkipTake_PaginatesCorrectly()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        for (int i = 0; i < 10; i++)
            await _context.Reports.AddAsync(MakeReport(importer.Id, $"r{i}.csv"), TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (page1, total1) = await _repository.GetPagedAsync(null, 0, 5, true, TestContext.Current.CancellationToken);
        var (page2, total2) = await _repository.GetPagedAsync(null, 5, 5, true, TestContext.Current.CancellationToken);

        total1.Should().Be(10);
        total2.Should().Be(10);
        page1.Should().HaveCount(5);
        page2.Should().HaveCount(5);
        page1.Select(r => r.Id).Should().NotIntersectWith(page2.Select(r => r.Id));
    }

    [Fact]
    public async Task GetPagedAsync_TotalCountReflectsFilterNotPageSize()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer, TestContext.Current.CancellationToken);
        await _context.Reports.AddAsync(MakeReport(importer.Id, "ibkr_q1.csv"), TestContext.Current.CancellationToken);
        await _context.Reports.AddAsync(MakeReport(importer.Id, "ibkr_q2.csv"), TestContext.Current.CancellationToken);
        await _context.Reports.AddAsync(MakeReport(importer.Id, "schwab.csv"), TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var filter = new ReportColumnFilter(NameContains: "ibkr");
        var (items, total) = await _repository.GetPagedAsync(filter, 0, 1, true, TestContext.Current.CancellationToken); // take only 1

        total.Should().Be(2); // total is 2, not 1
        items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedAsync_ImporterIds_FiltersToMatchingImporters()
    {
        var imp1 = MakeImporter();
        var imp2 = Importer.Create("Other Importer");
        await _context.Importers.AddRangeAsync(imp1, imp2);
        await _context.Reports.AddAsync(MakeReport(imp1.Id, "imp1_report.csv"), TestContext.Current.CancellationToken);
        await _context.Reports.AddAsync(MakeReport(imp2.Id, "imp2_report.csv"), TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var filter = new ReportColumnFilter(ImporterIds: new List<Guid> { imp1.Id }.AsReadOnly());
        var (items, total) = await _repository.GetPagedAsync(filter, 0, 10, true, TestContext.Current.CancellationToken);

        total.Should().Be(1);
        items[0].ReportName.Should().Be("imp1_report.csv");
    }
}
