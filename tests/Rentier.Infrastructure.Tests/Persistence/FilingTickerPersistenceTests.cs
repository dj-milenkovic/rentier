using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;

namespace Rentier.Infrastructure.Tests.Persistence;

[Trait("Category", "Integration")]
public class FilingTickerPersistenceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private FilingRepository _repository = null!;

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
        _repository = new FilingRepository(_context);
    }

    [Fact]
    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static TaxpayerProfile MakeProfile()
        => new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "John Doe", "Belgrade", "018");

    [Fact]
    public async Task Filing_WithTicker_SurvivesPersistenceRoundTrip()
    {
        var profile = MakeProfile();
        _context.Set<TaxpayerProfile>().Add(profile);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var filing = Filing.CreateFromIncome(
            new FilingInfo(IncomeType.Dividend, "ACME Corp", new DateOnly(2025, 3, 15), 1000m, 150m, 150m, 0m),
            profile.Id, new DateOnly(2025, 4, 30), new FilingProvenance(Ticker: "AAPL"));

        await _repository.AddAsync(filing, CancellationToken.None);

        // Reload from a fresh context
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        await using var freshContext = new AppDbContext(options);
        var freshRepo = new FilingRepository(freshContext);

        var loaded = await freshRepo.GetByIdAsync(filing.Id, CancellationToken.None);
        loaded.Should().NotBeNull();
        loaded!.Ticker.Should().Be("AAPL");
    }

    [Fact]
    public async Task Filing_WithNullTicker_SurvivesPersistenceRoundTrip()
    {
        var profile = MakeProfile();
        _context.Set<TaxpayerProfile>().Add(profile);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var filing = Filing.CreateFromIncome(
            new FilingInfo(IncomeType.Dividend, "ACME Corp", new DateOnly(2025, 3, 15), 1000m, 150m, 150m, 0m),
            profile.Id, new DateOnly(2025, 4, 30), new FilingProvenance(Ticker: null));

        await _repository.AddAsync(filing, CancellationToken.None);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        await using var freshContext = new AppDbContext(options);
        var freshRepo = new FilingRepository(freshContext);

        var loaded = await freshRepo.GetByIdAsync(filing.Id, CancellationToken.None);
        loaded.Should().NotBeNull();
        loaded!.Ticker.Should().BeNull();
    }
}
