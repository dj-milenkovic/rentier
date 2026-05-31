using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Xunit;

namespace Rentier.Infrastructure.Tests;

[Trait("Category", "Integration")]
public class TaxpayerProfileRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private TaxpayerProfileRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new TaxpayerProfileRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetAsync_EmptyDb_ReturnsNull()
    {
        var result = await _repository.GetAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_NewProfile_CanBeRetrieved()
    {
        var profile = new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "Marko", "Knez 1", "049");
        await _repository.SaveAsync(profile);

        var retrieved = await _repository.GetAsync();
        retrieved.Should().NotBeNull();
        retrieved!.Jmbg.Should().Be("1234567890123");
        retrieved.FullName.Should().Be("Marko");
    }

    [Fact]
    public async Task SaveAsync_ExistingProfile_UpdatesPreservesId()
    {
        var id = Guid.NewGuid();
        var original = new TaxpayerProfile(id, "1234567890123", "Original Name", "Addr", "049");
        await _repository.SaveAsync(original);

        var updated = new TaxpayerProfile(id, "1234567890123", "Updated Name", "Addr", "049");
        await _repository.SaveAsync(updated);

        var retrieved = await _repository.GetAsync();
        retrieved!.Id.Should().Be(id);
        retrieved.FullName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteAsync_AfterSave_ReturnsNullOnGet()
    {
        var profile = new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "Test", "Addr", "049");
        await _repository.SaveAsync(profile);
        await _repository.DeleteAsync();

        var result = await _repository.GetAsync();
        result.Should().BeNull();
    }
}
