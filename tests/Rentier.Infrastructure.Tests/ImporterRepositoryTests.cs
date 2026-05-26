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
public sealed class ImporterRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private ImporterRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new ImporterRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static Importer MakeImporter(string name = "Test")
        => Importer.Create(name, ReportType.IbkrCsv);

    [Fact]
    public async Task GetAllAsync_Empty_ReturnsEmptyList()
    {
        var result = await _repository.GetAllAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_NewImporter_PersistsCorrectly()
    {
        var importer = MakeImporter("My Importer");
        await _repository.AddAsync(importer);

        var all = await _repository.GetAllAsync();
        all.Should().HaveCount(1);

        var saved = all[0];
        saved.Id.Should().Be(importer.Id);
        saved.DisplayName.Should().Be("My Importer");
        saved.ReportType.Should().Be(ReportType.IbkrCsv);
        saved.TaxpayerProfileId.Should().BeNull();
        saved.MailboxId.Should().BeNull();
        saved.FromFilter.Should().Be(string.Empty);
        saved.SubjectFilter.Should().Be(string.Empty);
        saved.AttachmentRegex.Should().Be(string.Empty);
        saved.PaymentNotes.Should().Be(string.Empty);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsImporter()
    {
        var importer = MakeImporter();
        await _repository.AddAsync(importer);

        var found = await _repository.GetByIdAsync(importer.Id);
        found.Should().NotBeNull();
        found!.Id.Should().Be(importer.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var found = await _repository.GetByIdAsync(Guid.NewGuid());
        found.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ExistingImporter_UpdatesAllFields()
    {
        var importer = MakeImporter("Original");
        await _repository.AddAsync(importer);

        importer.UpdateDetails("Updated", ReportType.IbkrCsv, null, null, "from@x.com", "Subject:", @"\d+", "Notes");
        await _repository.UpdateAsync(importer);

        var found = await _repository.GetByIdAsync(importer.Id);
        found!.DisplayName.Should().Be("Updated");
        found.FromFilter.Should().Be("from@x.com");
        found.SubjectFilter.Should().Be("Subject:");
        found.AttachmentRegex.Should().Be(@"\d+");
        found.PaymentNotes.Should().Be("Notes");
    }

    [Fact]
    public async Task DeleteAsync_ExistingImporter_RemovesEntity()
    {
        var importer = MakeImporter();
        await _repository.AddAsync(importer);

        await _repository.DeleteAsync(importer.Id);

        var all = await _repository.GetAllAsync();
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_DoesNotThrow()
    {
        var act = async () => await _repository.DeleteAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }
}
