using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Xunit;

namespace Rentier.Infrastructure.Tests;

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
}
