using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rentier.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations tooling.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=rentier.db")
            .Options;

        return new AppDbContext(options);
    }
}
