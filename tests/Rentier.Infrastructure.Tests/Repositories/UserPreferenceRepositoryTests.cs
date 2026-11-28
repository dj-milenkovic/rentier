using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;

namespace Rentier.Infrastructure.Tests.Repositories;

[Trait("Category", "Integration")]
public class UserPreferenceRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private UserPreferenceRepository _repository = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        _repository = new UserPreferenceRepository(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetAsync_EmptyTable_ReturnsNull()
    {
        var result = await _repository.GetAsync("Language", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ExistingKey_ReturnsEntity()
    {
        var pref = new UserPreference("Language", "en");
        await _repository.SaveAsync(pref, TestContext.Current.CancellationToken);

        var retrieved = await _repository.GetAsync("Language", TestContext.Current.CancellationToken);

        retrieved.Should().NotBeNull();
        retrieved!.Key.Should().Be("Language");
        retrieved.Value.Should().Be("en");
    }

    [Fact]
    public async Task GetAsync_DifferentKey_ReturnsNull()
    {
        var pref = new UserPreference("Language", "en");
        await _repository.SaveAsync(pref, TestContext.Current.CancellationToken);

        var result = await _repository.GetAsync("Theme", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_NewKey_InsertsRowInDatabase()
    {
        var pref = new UserPreference("Language", "en");

        await _repository.SaveAsync(pref, TestContext.Current.CancellationToken);

        var rowCount = await _context.UserPreferences.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        rowCount.Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_ExistingKey_UpsertsPersistsNewValue()
    {
        var pref = new UserPreference("Language", "en");
        await _repository.SaveAsync(pref, TestContext.Current.CancellationToken);

        // Update via domain method then save again
        pref.UpdateValue("sr-Latn");
        await _repository.SaveAsync(pref, TestContext.Current.CancellationToken);

        var retrieved = await _repository.GetAsync("Language", TestContext.Current.CancellationToken);
        retrieved!.Value.Should().Be("sr-Latn");
    }

    [Fact]
    public async Task SaveAsync_MultipleKeys_StoreIndependently()
    {
        await _repository.SaveAsync(new UserPreference("Language", "en"), TestContext.Current.CancellationToken);
        await _repository.SaveAsync(new UserPreference("Theme", "Dark"), TestContext.Current.CancellationToken);

        var lang = await _repository.GetAsync("Language", TestContext.Current.CancellationToken);
        var theme = await _repository.GetAsync("Theme", TestContext.Current.CancellationToken);

        lang!.Value.Should().Be("en");
        theme!.Value.Should().Be("Dark");
    }

    // ── Long-lived repository (issue #119) ──────────────────────────────────
    // Pages keep their handlers — and the context behind each repository — for as long as they
    // are open, so a repository must never serve a copy cached by one of its earlier calls.

    private AppDbContext CreateOtherContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task GetAsync_ValueSavedThroughAnotherContext_ReturnsCurrentValue()
    {
        var ct = TestContext.Current.CancellationToken;
        await _repository.SaveAsync(new UserPreference("Language", "en"), ct);
        await _repository.GetAsync("Language", ct); // cache the old copy in this context
        await using var otherContext = CreateOtherContext();
        var other = new UserPreferenceRepository(otherContext);
        var changed = (await other.GetAsync("Language", ct))!;
        changed.UpdateValue("sr");
        await other.SaveAsync(changed, ct);

        var result = await _repository.GetAsync("Language", ct);

        result!.Value.Should().Be("sr");
    }

    [Fact]
    public async Task SaveAsync_RowDeletedThroughAnotherContext_InsertsItAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var preference = new UserPreference("Language", "en");
        await _repository.SaveAsync(preference, ct);
        await using var otherContext = CreateOtherContext();
        await otherContext.UserPreferences.ExecuteDeleteAsync(ct);

        await _repository.SaveAsync(new UserPreference("Language", "sr"), ct);

        var stored = await otherContext.UserPreferences.AsNoTracking().SingleAsync(ct);
        stored.Value.Should().Be("sr");
    }
}
