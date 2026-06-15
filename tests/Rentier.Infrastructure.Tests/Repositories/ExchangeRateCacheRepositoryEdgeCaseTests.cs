using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;

namespace Rentier.Infrastructure.Tests.Repositories;

/// <summary>
/// Covers edge-case paths in <see cref="ExchangeRateCacheRepository"/> not exercised
/// by the root-level <c>ExchangeRateCacheRepositoryTests</c>:
/// the empty-batch early return, range queries with no matching currency or date,
/// and the currency normalisation in batch operations.
/// </summary>
[Trait("Category", "Integration")]
public class ExchangeRateCacheRepositoryEdgeCaseTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private ExchangeRateCacheRepository _repository = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        _repository = new ExchangeRateCacheRepository(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ── SaveBatchAsync empty-list early return ──────────────────────────────

    [Fact]
    public async Task SaveBatchAsync_EmptyList_IsNoOpAndDoesNotThrow()
    {
        var act = async () => await _repository.SaveBatchAsync(
            [], TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        _context.ExchangeRateCache.Count().Should().Be(0);
    }

    // ── GetByDateRangeAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetByDateRangeAsync_NoRecordsForRequestedCurrency_ReturnsEmpty()
    {
        // Seed EUR only; querying USD must return nothing
        var date = new DateOnly(2024, 1, 15);
        await _repository.SaveAsync(new ExchangeRate(date, "EUR", 117m), TestContext.Current.CancellationToken);

        var result = await _repository.GetByDateRangeAsync(date, date, "USD", TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByDateRangeAsync_DateOutsideRequestedRange_ReturnsEmpty()
    {
        // Rate on Jan 10 — query range is Jan 15-20
        await _repository.SaveAsync(
            new ExchangeRate(new DateOnly(2024, 1, 10), "EUR", 117m),
            TestContext.Current.CancellationToken);

        var result = await _repository.GetByDateRangeAsync(
            new DateOnly(2024, 1, 15),
            new DateOnly(2024, 1, 20),
            "EUR",
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByDateRangeAsync_CurrencyNormalisedToUppercase_MatchesStoredRecord()
    {
        // Saved with uppercase "EUR"; queried with lowercase "eur" — must still match
        var date = new DateOnly(2024, 3, 1);
        await _repository.SaveAsync(new ExchangeRate(date, "EUR", 118m), TestContext.Current.CancellationToken);

        var result = await _repository.GetByDateRangeAsync(date, date, "eur", TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result[0].Currency.Should().Be("EUR");
        result[0].RateToRsd.Should().Be(118m);
    }

    [Fact]
    public async Task GetByDateRangeAsync_RatesOnRangeBoundaries_IncludesBothEnds()
    {
        var from = new DateOnly(2024, 1, 1);
        var to = new DateOnly(2024, 1, 3);

        await _repository.SaveAsync(new ExchangeRate(from, "EUR", 110m), TestContext.Current.CancellationToken);
        await _repository.SaveAsync(new ExchangeRate(new DateOnly(2024, 1, 2), "EUR", 111m), TestContext.Current.CancellationToken);
        await _repository.SaveAsync(new ExchangeRate(to, "EUR", 112m), TestContext.Current.CancellationToken);

        var result = await _repository.GetByDateRangeAsync(from, to, "EUR", TestContext.Current.CancellationToken);

        result.Should().HaveCount(3);
        result[0].Date.Should().Be(from);
        result[2].Date.Should().Be(to);
    }

    // ── SaveBatchAsync with multiple currencies ─────────────────────────────

    [Fact]
    public async Task SaveBatchAsync_MultipleCurrencies_UpsertEachIndependently()
    {
        var date = new DateOnly(2024, 6, 1);

        // Initial save
        await _repository.SaveBatchAsync(
            new[]
            {
                new ExchangeRate(date, "EUR", 117m),
                new ExchangeRate(date, "USD", 108m),
            },
            TestContext.Current.CancellationToken);

        // Upsert only EUR
        await _repository.SaveBatchAsync(
            new[] { new ExchangeRate(date, "EUR", 120m) },
            TestContext.Current.CancellationToken);

        var eur = await _repository.GetAsync(date, "EUR", TestContext.Current.CancellationToken);
        var usd = await _repository.GetAsync(date, "USD", TestContext.Current.CancellationToken);

        eur!.RateToRsd.Should().Be(120m);  // updated
        usd!.RateToRsd.Should().Be(108m);  // unchanged
        _context.ExchangeRateCache.Count().Should().Be(2);
    }

    // ── decimal precision round-trip ────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_HighPrecisionRate_PreservesExactDecimalValue()
    {
        // Verifies the decimal value converter for SQLite TEXT storage
        var date = new DateOnly(2024, 7, 15);
        var preciseRate = 117.5952m;
        await _repository.SaveAsync(new ExchangeRate(date, "EUR", preciseRate), TestContext.Current.CancellationToken);

        var result = await _repository.GetAsync(date, "EUR", TestContext.Current.CancellationToken);

        result!.RateToRsd.Should().Be(preciseRate);
    }
}
