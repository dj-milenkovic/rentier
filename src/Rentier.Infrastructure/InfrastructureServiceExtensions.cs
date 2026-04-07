using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Rentier.Infrastructure.Scraping;
using Rentier.Infrastructure.Security;

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
        return services;
    }
}
