using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Application.Services;
using Rentier.Infrastructure.ExchangeRates;
using Rentier.Infrastructure.Parsing;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Rentier.Infrastructure.Scraping;
using Rentier.Infrastructure.Security;
using Rentier.Infrastructure.Serialization;
using Rentier.Infrastructure.Sync;
using Rentier.Infrastructure.Updates;

namespace Rentier.Infrastructure;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers all infrastructure services (DbContext, repositories, HTTP clients, credential
    /// store, parsers, serializers, sync, updates, database initializer). This is the single
    /// entry point the composition root (Rentier.Desktop bootstrap) calls. Async because the
    /// platform credential store is probed during registration.
    /// </summary>
    /// <param name="services">The DI service collection to register into.</param>
    /// <param name="dbPath">Absolute path to the SQLite database file.</param>
    /// <param name="startupLogger">
    /// Optional logger for registration-time facts (which credential store was selected). The
    /// container does not exist yet at this point, so the caller supplies one.
    /// </param>
    public static async Task AddInfrastructureServicesAsync(
        this IServiceCollection services, string dbPath, ILogger? startupLogger = null)
    {
        // Transient lifetime is intentional: this is a desktop (Avalonia) app with no DI request scope.
        // Each service gets its own DbContext, so a repository resolved from DI commits each write on its own.
        // Operations that must commit several writes atomically go through IUnitOfWorkFactory instead.
        services.AddDbContext<AppDbContext>(
            opt => opt.UseSqlite($"Data Source={dbPath}"),
            ServiceLifetime.Transient);
        services.AddTransient<ITaxpayerProfileRepository, TaxpayerProfileRepository>();
        services.AddTransient<IHolidayRepository, HolidayRepository>();
        services.AddHttpClient<IHolidayImporter, TimeAndDateHolidayScraper>();
        services.AddTransient<IMailboxRepository, MailboxRepository>();
        services.AddTransient<IImporterRepository, ImporterRepository>();

        // Credential store: resolved at startup via platform factory
        var factoryResult = await CredentialStoreFactory.CreateAsync();
        if (factoryResult.IsSuccess)
        {
            var (store, info) = factoryResult.Value;
            services.AddSingleton<ICredentialStore>(store);
            startupLogger?.LogInformation("Credential store provider: {Provider}", info.ProviderName);
        }
        else
        {
            // Code only: the message can embed environment details from the platform exception.
            startupLogger?.LogWarning(
                "Credential store unavailable ({Code}); mailbox credentials cannot be saved or read",
                factoryResult.Error.Code);
            services.AddSingleton<ICredentialStore>(new NullCredentialStore(factoryResult.Error));
        }

        services.AddTransient<IExchangeRateCacheRepository, ExchangeRateCacheRepository>();
        services.AddHttpClient<NbsExchangeRateFetcher>();
        services.AddHttpClient<NbsWebScraper>();
        services.AddTransient<IExchangeRateFetcher>(sp =>
            new CompositeExchangeRateFetcher(
                sp.GetRequiredService<NbsExchangeRateFetcher>(),
                sp.GetRequiredService<NbsWebScraper>()));
        services.AddTransient<IExchangeRateResolver, ExchangeRateResolver>();
        services.AddTransient<IStatementParser, IbkrCsvParser>();
        services.AddTransient<IReportRepository, ReportRepository>();
        services.AddTransient<IFilingRepository, FilingRepository>();
        services.AddTransient<IUnitOfWorkFactory, EfUnitOfWorkFactory>();
        services.AddTransient<IUserPreferenceRepository, UserPreferenceRepository>();
        services.AddTransient<IXmlFilingSerializer, PpOpoXmlSerializer>();
        services.AddTransient<IMailboxSyncService, ImapMailboxSyncService>();
        services.AddSingleton<IUpdateService, VelopackUpdateService>();
        services.AddTransient<IDatabaseInitializer, DatabaseInitializer>();
    }
}
