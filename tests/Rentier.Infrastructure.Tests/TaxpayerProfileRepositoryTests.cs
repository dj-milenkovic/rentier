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
        _repository = new TaxpayerProfileRepository(_context);
    }

    [Fact]
    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetAsync_EmptyDb_ReturnsNull()
    {
        var result = await _repository.GetAsync(TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_NewProfile_CanBeRetrieved()
    {
        var profile = new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "Marko", "Knez 1", "7101");
        await _repository.SaveAsync(profile, TestContext.Current.CancellationToken);

        var retrieved = await _repository.GetAsync(TestContext.Current.CancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.Jmbg.Should().Be("1234567890123");
        retrieved.FullName.Should().Be("Marko");
    }

    [Fact]
    public async Task SaveAsync_ExistingProfile_UpdatesPreservesId()
    {
        var id = Guid.NewGuid();
        var original = new TaxpayerProfile(id, "1234567890123", "Original Name", "Addr", "7101");
        await _repository.SaveAsync(original, TestContext.Current.CancellationToken);

        var updated = new TaxpayerProfile(id, "1234567890123", "Updated Name", "Addr", "7101");
        await _repository.SaveAsync(updated, TestContext.Current.CancellationToken);

        var retrieved = await _repository.GetAsync(TestContext.Current.CancellationToken);
        retrieved!.Id.Should().Be(id);
        retrieved.FullName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteAsync_AfterSave_ReturnsNullOnGet()
    {
        var profile = new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "Test", "Addr", "7101");
        await _repository.SaveAsync(profile, TestContext.Current.CancellationToken);
        await _repository.DeleteAsync(TestContext.Current.CancellationToken);

        var result = await _repository.GetAsync(TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }
}
