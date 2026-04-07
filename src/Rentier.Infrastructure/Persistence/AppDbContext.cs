using Microsoft.EntityFrameworkCore;
using Rentier.Domain.Entities;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.Persistence.Configurations;

namespace Rentier.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Rentier application.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaxpayerProfile> TaxpayerProfiles => Set<TaxpayerProfile>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();
    public DbSet<HolidayYearRange> HolidayYearRange => Set<HolidayYearRange>();
    public DbSet<Mailbox> Mailboxes => Set<Mailbox>();
    public DbSet<Importer> Importers => Set<Importer>();
    public DbSet<ExchangeRate> ExchangeRateCache => Set<ExchangeRate>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Filing> Filings => Set<Filing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
