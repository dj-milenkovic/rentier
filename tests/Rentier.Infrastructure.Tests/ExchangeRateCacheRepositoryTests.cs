using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Xunit;

namespace Rentier.Infrastructure.Tests;

[Trait("Category", "Integration")]
public class ExchangeRateCacheRepositoryTests
{
    private static async Task<(AppDbContext db, SqliteConnection conn)> CreateDbAsync()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return (db, conn);
    }

    [Fact]
    public async Task GetAsync_NoCachedRate_ReturnsNull()
    {
        var (db, conn) = await CreateDbAsync();
        await using (db) await using (conn)
        {
            var repo = new ExchangeRateCacheRepository(db);
            var result = await repo.GetAsync(new DateOnly(2024, 1, 15), "EUR");
            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task SaveAsync_NewRate_PersistsToDb()
    {
        var (db, conn) = await CreateDbAsync();
        await using (db) await using (conn)
        {
            var repo = new ExchangeRateCacheRepository(db);
            var rate = new ExchangeRate(new DateOnly(2024, 1, 15), "EUR", 117.5952m);
            await repo.SaveAsync(rate);

            var result = await repo.GetAsync(new DateOnly(2024, 1, 15), "EUR");
            result.Should().NotBeNull();
            result!.RateToRsd.Should().Be(117.5952m);
        }
    }

    [Fact]
    public async Task SaveAsync_ExistingRate_UpdatesRateToRsd()
    {
        var (db, conn) = await CreateDbAsync();
        await using (db) await using (conn)
        {
            var repo = new ExchangeRateCacheRepository(db);
            var date = new DateOnly(2024, 1, 15);

            await repo.SaveAsync(new ExchangeRate(date, "EUR", 100m));
            await repo.SaveAsync(new ExchangeRate(date, "EUR", 117m));

            var result = await repo.GetAsync(date, "EUR");
            result.Should().NotBeNull();
            result!.RateToRsd.Should().Be(117m);
        }
    }

    [Fact]
    public async Task SaveBatchAsync_NewRates_PersistsAll()
    {
        var (db, conn) = await CreateDbAsync();
        await using (db) await using (conn)
        {
            var repo = new ExchangeRateCacheRepository(db);
            var date = new DateOnly(2024, 1, 15);
            var batch = new List<ExchangeRate>
            {
                new ExchangeRate(date, "EUR", 117.5952m),
                new ExchangeRate(date, "USD", 108.4321m),
                new ExchangeRate(date, "GBP", 136.8765m),
            };

            await repo.SaveBatchAsync(batch);

            db.ExchangeRateCache.Count().Should().Be(3);
        }
    }

    [Fact]
    public async Task SaveBatchAsync_DuplicateRates_Upserts()
    {
        var (db, conn) = await CreateDbAsync();
        await using (db) await using (conn)
        {
            var repo = new ExchangeRateCacheRepository(db);
            var date = new DateOnly(2024, 1, 15);

            await repo.SaveAsync(new ExchangeRate(date, "EUR", 100m));

            var batch = new List<ExchangeRate>
            {
                new ExchangeRate(date, "EUR", 117m),
            };

            var act = async () => await repo.SaveBatchAsync(batch);
            await act.Should().NotThrowAsync();

            var result = await repo.GetAsync(date, "EUR");
            result.Should().NotBeNull();
            result!.RateToRsd.Should().Be(117m);
        }
    }

    [Fact]
    public async Task GetByDateRangeAsync_FiltersCorrectly()
    {
        var (db, conn) = await CreateDbAsync();
        await using (db) await using (conn)
        {
            var repo = new ExchangeRateCacheRepository(db);
            var dates = new[]
            {
                new DateOnly(2024, 1, 13),
                new DateOnly(2024, 1, 14),
                new DateOnly(2024, 1, 15),
                new DateOnly(2024, 1, 16),
                new DateOnly(2024, 1, 17),
            };

            foreach (var d in dates)
                await repo.SaveAsync(new ExchangeRate(d, "EUR", 117m));

            var result = await repo.GetByDateRangeAsync(
                new DateOnly(2024, 1, 14), new DateOnly(2024, 1, 16), "EUR");

            result.Should().HaveCount(3);
            result.Should().OnlyContain(r => r.Currency == "EUR");
            result.Select(r => r.Date).Should().BeInAscendingOrder();
        }
    }

    [Fact]
    public async Task GetAsync_CurrencyNormalisedUppercase()
    {
        var (db, conn) = await CreateDbAsync();
        await using (db) await using (conn)
        {
            var repo = new ExchangeRateCacheRepository(db);
            var date = new DateOnly(2024, 1, 15);

            await repo.SaveAsync(new ExchangeRate(date, "EUR", 117.5952m));

            // lowercase lookup should still find the record
            var result = await repo.GetAsync(date, "eur");
            result.Should().NotBeNull();
            result!.Currency.Should().Be("EUR");
        }
    }
}
