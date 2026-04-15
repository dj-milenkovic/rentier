using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Rentier.Tests.Common.Fakes;

namespace Rentier.Scenarios.Tests;

/// <summary>
/// Bootstraps real DI with SQLite in-memory for scenario tests.
/// Each test should create a fresh instance to ensure data isolation.
/// </summary>
public sealed class ScenarioFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    /// <summary>The configured service provider for resolving dependencies.</summary>
    public IServiceProvider Services { get; }

    public ScenarioFixture()
    {
        // Keep connection open for SQLite in-memory DB lifetime
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();

        // Register DbContext with SQLite in-memory provider
        services.AddDbContext<AppDbContext>(
            o => o.UseSqlite(_connection),
            ServiceLifetime.Transient);

        // Register real repositories
        services.AddTransient<ITaxpayerProfileRepository, TaxpayerProfileRepository>();
        services.AddTransient<IFilingRepository, FilingRepository>();
        services.AddTransient<IReportRepository, ReportRepository>();
        services.AddTransient<IImporterRepository, ImporterRepository>();
        services.AddTransient<IExchangeRateCacheRepository, ExchangeRateCacheRepository>();
        services.AddTransient<IHolidayRepository, HolidayRepository>();
        services.AddTransient<IMailboxRepository, MailboxRepository>();

        // Mock external dependencies (credential store, mailbox sync)
        services.AddSingleton<ICredentialStore>(new FakeCredentialStore());
        services.AddSingleton(NSubstitute.Substitute.For<IMailboxSyncService>());

        Services = services.BuildServiceProvider();

        // Create database schema
        using var ctx = Services.GetRequiredService<AppDbContext>();
        ctx.Database.EnsureCreated();
    }

    /// <summary>Resolves a service from the DI container.</summary>
    public T GetService<T>() where T : notnull
        => Services.GetRequiredService<T>();

    public void Dispose()
    {
        _connection.Dispose();
    }
}
