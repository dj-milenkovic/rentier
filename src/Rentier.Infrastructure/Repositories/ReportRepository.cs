using Microsoft.EntityFrameworkCore;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure.Repositories;

public sealed class ReportRepository : IReportRepository
{
    private readonly AppDbContext _db;

    public ReportRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Report?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Reports.FindAsync([id], ct);

    public async Task<IReadOnlyList<Report>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.Reports.AsNoTracking()
            .OrderByDescending(r => r.ImportDate)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<Report>> GetByImporterAsync(Guid importerId, CancellationToken ct = default)
    {
        var list = await _db.Reports
            .AsNoTracking()
            .Where(r => r.ImporterId == importerId)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<Report>> GetByStatusAsync(ReportStatus status, CancellationToken ct = default)
    {
        var list = await _db.Reports
            .AsNoTracking()
            .Where(r => r.Status == status)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<bool> ExistsByImporterAndNameAsync(Guid importerId, string reportName, CancellationToken ct = default)
        => await _db.Reports
            .AsNoTracking()
            .AnyAsync(r => r.ImporterId == importerId && r.ReportName == reportName, ct);

    public async Task AddAsync(Report report, CancellationToken ct = default)
    {
        _db.Reports.Add(report);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Report report, CancellationToken ct = default)
    {
        var stale = _db.ChangeTracker.Entries<Report>()
            .FirstOrDefault(e => e.Entity.Id == report.Id);
        if (stale != null)
            stale.State = EntityState.Detached;

        _db.Reports.Update(report);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Reports.FindAsync([id], ct);
        if (entity is not null)
        {
            _db.Reports.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }
}
