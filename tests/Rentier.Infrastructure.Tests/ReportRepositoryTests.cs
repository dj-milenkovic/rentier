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

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new ReportRepository(_context);
    }

    public async Task DisposeAsync()
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
        await _context.Importers.AddAsync(importer);
        await _context.SaveChangesAsync();

        var report = MakeReport(importer.Id);
        await _repository.AddAsync(report);

        var all = await _repository.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Id.Should().Be(report.Id);
        all[0].ReportName.Should().Be("report.csv");
    }

    [Fact]
    public async Task GetAllAsync_DefaultSortDescending_OrdersByLatestEmailDateFirst()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer);
        await _context.SaveChangesAsync();

        var older = Report.Create(importer.Id, "older.csv", [1], 100L, new DateOnly(2024, 1, 15));
        var newer = Report.Create(importer.Id, "newer.csv", [1], 101L, new DateOnly(2024, 3, 15));

        await _repository.AddAsync(older);
        await _repository.AddAsync(newer);

        var reports = await _repository.GetAllAsync();

        reports.Select(report => report.ReportName).Should().ContainInOrder("newer.csv", "older.csv");
    }

    [Fact]
    public async Task GetByStatusAsync_InitStatus_ReturnsMatchingReports()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer);
        await _context.SaveChangesAsync();

        var r1 = MakeReport(importer.Id, "init.csv");
        var r2 = MakeReport(importer.Id, "processed.csv");
        r2.SetStatus(ReportStatus.Processed);

        await _repository.AddAsync(r1);
        await _repository.AddAsync(r2);

        var result = await _repository.GetByStatusAsync(ReportStatus.Init);
        result.Should().HaveCount(1);
        result[0].ReportName.Should().Be("init.csv");
    }

    [Fact]
    public async Task ExistsByImporterAndNameAsync_Existing_ReturnsTrue()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer);
        await _context.SaveChangesAsync();

        var report = MakeReport(importer.Id, "exists.csv");
        await _repository.AddAsync(report);

        var exists = await _repository.ExistsByImporterAndNameAsync(importer.Id, "exists.csv");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByImporterAndNameAsync_Missing_ReturnsFalse()
    {
        var exists = await _repository.ExistsByImporterAndNameAsync(Guid.NewGuid(), "nonexistent.csv");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_DuplicateImporterAndName_ThrowsDbUpdateException()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer);
        await _context.SaveChangesAsync();

        await _repository.AddAsync(MakeReport(importer.Id, "dupe.csv"));

        var act = async () => await _repository.AddAsync(MakeReport(importer.Id, "dupe.csv"));
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ── GetByIdAsync tests ───────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsReport()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer);
        await _context.SaveChangesAsync();

        var report = MakeReport(importer.Id);
        await _repository.AddAsync(report);

        var result = await _repository.GetByIdAsync(report.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(report.Id);
        result.ReportName.Should().Be("report.csv");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    // ── GetByImporterAsync tests ─────────────────────────────────────────────

    [Fact]
    public async Task GetByImporterAsync_WithMatchingReports_ReturnsAll()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer);
        await _context.SaveChangesAsync();

        await _repository.AddAsync(MakeReport(importer.Id, "r1.csv"));
        await _repository.AddAsync(MakeReport(importer.Id, "r2.csv"));

        var result = await _repository.GetByImporterAsync(importer.Id);

        result.Should().HaveCount(2);
        result.Select(r => r.ReportName).Should().BeEquivalentTo(["r1.csv", "r2.csv"]);
    }

    [Fact]
    public async Task GetByImporterAsync_WithNoMatch_ReturnsEmpty()
    {
        var result = await _repository.GetByImporterAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    // ── UpdateAsync tests ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WithExistingReport_PersistsChanges()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer);
        await _context.SaveChangesAsync();

        var report = MakeReport(importer.Id);
        await _repository.AddAsync(report);

        report.SetStatus(ReportStatus.Processed);
        await _repository.UpdateAsync(report);

        var result = await _repository.GetByIdAsync(report.Id);
        result!.Status.Should().Be(ReportStatus.Processed);
    }

    // ── DeleteAsync tests ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WithExistingReport_RemovesIt()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer);
        await _context.SaveChangesAsync();

        var report = MakeReport(importer.Id);
        await _repository.AddAsync(report);

        await _repository.DeleteAsync(report.Id);

        var all = await _repository.GetAllAsync();
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
        await _context.Importers.AddAsync(importer);
        await _context.SaveChangesAsync();

        var r1 = MakeReport(importer.Id, "r1.csv");
        var r2 = MakeReport(importer.Id, "r2.csv");
        var r3 = MakeReport(importer.Id, "r3.csv");
        await _repository.AddAsync(r1);
        await _repository.AddAsync(r2);
        await _repository.AddAsync(r3);

        await _repository.DeleteManyAsync([r1.Id, r2.Id]);

        var remaining = await _repository.GetAllAsync();
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
        await _context.Importers.AddAsync(importer);
        for (int i = 0; i < 5; i++)
            await _context.Reports.AddAsync(MakeReport(importer.Id, $"r{i}.csv"));
        await _context.SaveChangesAsync();

        var (items, total) = await _repository.GetPagedAsync(null, 0, 3, true);

        total.Should().Be(5);
        items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPagedAsync_NameContains_ReturnsMatchingRows()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer);
        await _context.Reports.AddAsync(MakeReport(importer.Id, "ibkr_2024.csv"));
        await _context.Reports.AddAsync(MakeReport(importer.Id, "schwab_2024.csv"));
        await _context.SaveChangesAsync();

        var filter = new ReportColumnFilter(NameContains: "ibkr");
        var (items, total) = await _repository.GetPagedAsync(filter, 0, 10, true);

        total.Should().Be(1);
        items.Should().HaveCount(1);
        items[0].ReportName.Should().Be("ibkr_2024.csv");
    }

    [Fact]
    public async Task GetPagedAsync_StatusFilter_ReturnsMatchingRows()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer);
        var r1 = MakeReport(importer.Id, "r1.csv");
        var r2 = MakeReport(importer.Id, "r2.csv");
        await _context.Reports.AddRangeAsync(r1, r2);
        await _context.SaveChangesAsync();

        // Update r2 status
        r2.SetStatus(Rentier.Domain.Enums.ReportStatus.Processed);
        _context.Reports.Update(r2);
        await _context.SaveChangesAsync();

        var filter = new ReportColumnFilter(StatusFilter: Rentier.Domain.Enums.ReportStatus.Init);
        var (items, total) = await _repository.GetPagedAsync(filter, 0, 10, true);

        total.Should().Be(1);
        items[0].ReportName.Should().Be("r1.csv");
    }

    [Fact]
    public async Task GetPagedAsync_SkipTake_PaginatesCorrectly()
    {
        var importer = MakeImporter();
        await _context.Importers.AddAsync(importer);
        for (int i = 0; i < 10; i++)
            await _context.Reports.AddAsync(MakeReport(importer.Id, $"r{i}.csv"));
        await _context.SaveChangesAsync();

        var (page1, total1) = await _repository.GetPagedAsync(null, 0, 5, true);
        var (page2, total2) = await _repository.GetPagedAsync(null, 5, 5, true);

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
        await _context.Importers.AddAsync(importer);
        await _context.Reports.AddAsync(MakeReport(importer.Id, "ibkr_q1.csv"));
        await _context.Reports.AddAsync(MakeReport(importer.Id, "ibkr_q2.csv"));
        await _context.Reports.AddAsync(MakeReport(importer.Id, "schwab.csv"));
        await _context.SaveChangesAsync();

        var filter = new ReportColumnFilter(NameContains: "ibkr");
        var (items, total) = await _repository.GetPagedAsync(filter, 0, 1, true); // take only 1

        total.Should().Be(2); // total is 2, not 1
        items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedAsync_ImporterIds_FiltersToMatchingImporters()
    {
        var imp1 = MakeImporter();
        var imp2 = Importer.Create("Other Importer");
        await _context.Importers.AddRangeAsync(imp1, imp2);
        await _context.Reports.AddAsync(MakeReport(imp1.Id, "imp1_report.csv"));
        await _context.Reports.AddAsync(MakeReport(imp2.Id, "imp2_report.csv"));
        await _context.SaveChangesAsync();

        var filter = new ReportColumnFilter(ImporterIds: new List<Guid> { imp1.Id }.AsReadOnly());
        var (items, total) = await _repository.GetPagedAsync(filter, 0, 10, true);

        total.Should().Be(1);
        items[0].ReportName.Should().Be("imp1_report.csv");
    }
}
