using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
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

namespace Rentier.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, string dbPath)
    {
        services.AddDbContext<AppDbContext>(
            opt => opt.UseSqlite($"Data Source={dbPath}"),
            ServiceLifetime.Transient);
        services.AddTransient<ITaxpayerProfileRepository, TaxpayerProfileRepository>();
        services.AddTransient<IHolidayRepository, HolidayRepository>();
        services.AddHttpClient<IHolidayImporter, TimeAndDateHolidayScraper>();
        services.AddTransient<IMailboxRepository, MailboxRepository>();
        services.AddTransient<IImporterRepository, ImporterRepository>();
#pragma warning disable CA1416
        services.AddTransient<ICredentialStore, OsCredentialStore>();
#pragma warning restore CA1416
        services.AddTransient<IExchangeRateCacheRepository, ExchangeRateCacheRepository>();
        services.AddHttpClient<NbsExchangeRateFetcher>();
        services.AddHttpClient<NbsWebScraper>();
        services.AddTransient<IExchangeRateFetcher, CompositeExchangeRateFetcher>();
        services.AddTransient<ExchangeRateResolver>();
        services.AddTransient<IStatementParser, IbkrCsvParser>();
        services.AddTransient<IReportRepository, ReportRepository>();
        services.AddTransient<IFilingRepository, FilingRepository>();
        services.AddTransient<IXmlFilingSerializer, PpOpoXmlSerializer>();
        services.AddTransient<IMailboxSyncService, ImapMailboxSyncService>();
        services.AddTransient<
            ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>,
            SyncMailboxCommandHandler>();
        services.AddTransient<
            ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>,
            ProcessReportsCommandHandler>();
        return services;
    }
}