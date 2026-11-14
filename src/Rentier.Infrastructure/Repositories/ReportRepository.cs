using Microsoft.EntityFrameworkCore;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure.Repositories;

public sealed class ReportRepository : IReportRepository
{
    private readonly AppDbContext _db;
    private readonly bool _autoSave;

    /// <summary>
    /// Standalone repository (the DI registration): it owns its context, so every write
    /// method is a complete single-statement operation and commits immediately.
    /// </summary>
    public ReportRepository(AppDbContext db) : this(db, autoSave: true)
    {
    }

    /// <summary>
    /// With <paramref name="autoSave"/> false the repository only stages changes on the
    /// shared context; the owning <see cref="IUnitOfWork"/> commits them atomically.
    /// </summary>
    internal ReportRepository(AppDbContext db, bool autoSave)
    {
        _db = db;
        _autoSave = autoSave;
    }

    private Task CommitIfStandaloneAsync(CancellationToken ct)
        => _autoSave ? _db.SaveChangesAsync(ct) : Task.CompletedTask;

    public async Task<Report?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Reports.FindAsync([id], ct);

    public async Task<IReadOnlyList<Report>> GetAllAsync(bool sortDescending = true, CancellationToken ct = default)
    {
        var query = _db.Reports.AsNoTracking();
        var list = sortDescending
            ? await query
                .OrderByDescending(r => r.EmailDate ?? r.ImportDate)
                .ThenByDescending(r => r.ImportDate)
                .ThenByDescending(r => r.Id)
                .ToListAsync(ct)
            : await query
                .OrderBy(r => r.EmailDate ?? r.ImportDate)
                .ThenBy(r => r.ImportDate)
                .ThenBy(r => r.Id)
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
        await CommitIfStandaloneAsync(ct);
    }

    public async Task UpdateAsync(Report report, CancellationToken ct = default)
    {
        var stale = _db.ChangeTracker.Entries<Report>()
            .FirstOrDefault(e => e.Entity.Id == report.Id);
        if (stale != null)
            stale.State = EntityState.Detached;

        _db.Reports.Update(report);
        await CommitIfStandaloneAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Reports.FindAsync([id], ct);
        if (entity is not null)
        {
            _db.Reports.Remove(entity);
            await CommitIfStandaloneAsync(ct);
        }
    }

    public async Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return;
        var reports = await _db.Reports
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(ct);
        if (reports.Count == 0) return;
        _db.Reports.RemoveRange(reports);
        await CommitIfStandaloneAsync(ct);
    }

    public async Task<(IReadOnlyList<Report> Items, int TotalCount)> GetPagedAsync(
        ReportColumnFilter? filter,
        int skip,
        int take,
        bool sortDescending,
        CancellationToken ct = default)
    {
        var query = _db.Reports.AsNoTracking().AsQueryable();

        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.NameContains))
                query = query.Where(r => r.ReportName.Contains(filter.NameContains));

            if (!string.IsNullOrWhiteSpace(filter.ImportDateContains))
            {
                var term = filter.ImportDateContains;
                query = query.Where(r => EF.Functions.Like(r.ImportDate.ToString(), $"%{term}%"));
            }

            if (!string.IsNullOrWhiteSpace(filter.EmailDateContains))
            {
                var term = filter.EmailDateContains;
                query = query.Where(r => r.EmailDate != null &&
                    EF.Functions.Like(r.EmailDate.Value.ToString(), $"%{term}%"));
            }

            if (filter.StatusFilters is { Count: > 0 })
                query = query.Where(r => filter.StatusFilters.Contains(r.Status));

            if (filter.ImporterIds is { Count: > 0 })
                query = query.Where(r => filter.ImporterIds.Contains(r.ImporterId));
        }

        query = sortDescending
            ? query.OrderByDescending(r => r.EmailDate ?? r.ImportDate)
                   .ThenByDescending(r => r.ImportDate)
                   .ThenByDescending(r => r.Id)
            : query.OrderBy(r => r.EmailDate ?? r.ImportDate)
                   .ThenBy(r => r.ImportDate)
                   .ThenBy(r => r.Id);

        var totalCount = await query.CountAsync(ct);
        var items = await query.Skip(skip).Take(take).ToListAsync(ct);
        return (items.AsReadOnly(), totalCount);
    }

}
