using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Xunit;

namespace Rentier.Infrastructure.Tests.Repositories;

[Trait("Category", "Integration")]
public class UserPreferenceRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private UserPreferenceRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new UserPreferenceRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetAsync_EmptyTable_ReturnsNull()
    {
        var result = await _repository.GetAsync("Language");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ExistingKey_ReturnsEntity()
    {
        var pref = new UserPreference("Language", "en");
        await _repository.SaveAsync(pref);

        var retrieved = await _repository.GetAsync("Language");

        retrieved.Should().NotBeNull();
        retrieved!.Key.Should().Be("Language");
        retrieved.Value.Should().Be("en");
    }

    [Fact]
    public async Task GetAsync_DifferentKey_ReturnsNull()
    {
        var pref = new UserPreference("Language", "en");
        await _repository.SaveAsync(pref);

        var result = await _repository.GetAsync("Theme");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_NewKey_InsertsRowInDatabase()
    {
        var pref = new UserPreference("Language", "en");

        await _repository.SaveAsync(pref);

        var rowCount = await _context.UserPreferences.CountAsync();
        rowCount.Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_ExistingKey_UpsertsPersistsNewValue()
    {
        var pref = new UserPreference("Language", "en");
        await _repository.SaveAsync(pref);

        // Update via domain method then save again
        pref.UpdateValue("sr-Latn");
        await _repository.SaveAsync(pref);

        var retrieved = await _repository.GetAsync("Language");
        retrieved!.Value.Should().Be("sr-Latn");
    }

    [Fact]
    public async Task SaveAsync_MultipleKeys_StoreIndependently()
    {
        await _repository.SaveAsync(new UserPreference("Language", "en"));
        await _repository.SaveAsync(new UserPreference("Theme", "Dark"));

        var lang = await _repository.GetAsync("Language");
        var theme = await _repository.GetAsync("Theme");

        lang!.Value.Should().Be("en");
        theme!.Value.Should().Be("Dark");
    }
}
