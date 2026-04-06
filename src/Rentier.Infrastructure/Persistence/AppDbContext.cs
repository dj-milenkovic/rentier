using Microsoft.EntityFrameworkCore;

namespace Rentier.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Rentier application.
/// DbSet properties and entity configurations are added in future features.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
