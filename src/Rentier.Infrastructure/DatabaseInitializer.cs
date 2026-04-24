using Microsoft.EntityFrameworkCore;
using Rentier.Application.Interfaces;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure;

public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly AppDbContext _db;

    public DatabaseInitializer(AppDbContext db) => _db = db;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _db.Database.MigrateAsync(ct);
    }
}
