using Microsoft.EntityFrameworkCore;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence.Configurations;

namespace Rentier.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Rentier application.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaxpayerProfile> TaxpayerProfiles => Set<TaxpayerProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
