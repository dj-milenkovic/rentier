using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
}
