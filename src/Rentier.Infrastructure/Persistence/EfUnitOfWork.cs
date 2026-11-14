using Rentier.Application.Repositories;
using Rentier.Infrastructure.Repositories;

namespace Rentier.Infrastructure.Persistence;

/// <summary>
/// EF Core unit of work: owns one <see cref="AppDbContext"/> shared by staging-only
/// repositories. <see cref="SaveChangesAsync"/> issues a single EF Core SaveChanges, which
/// SQLite executes inside one transaction — every staged change commits or none does.
/// </summary>
internal sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public EfUnitOfWork(AppDbContext db)
    {
        _db = db;
        Reports = new ReportRepository(db, autoSave: false);
        Filings = new FilingRepository(db, autoSave: false);
    }

    public IReportRepository Reports { get; }

    public IFilingRepository Filings { get; }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}
