using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Xunit;

namespace Rentier.Infrastructure.Tests;

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
}
