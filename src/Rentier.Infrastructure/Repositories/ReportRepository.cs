using Microsoft.EntityFrameworkCore;
using Rentier.Application.DTOs;
using Rentier.Application.Enums;
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

    public async Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return;
        var reports = await _db.Reports
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(ct);
        if (reports.Count == 0) return;
        _db.Reports.RemoveRange(reports);
        await _db.SaveChangesAsync(ct);
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

            if (filter.ImportDateValue.HasValue)
                query = ApplyImportDateFilter(query, filter.ImportDateOperator, filter.ImportDateValue.Value);

            if (filter.EmailDateValue.HasValue)
            {
                query = query.Where(r => r.EmailDate != null);
                query = ApplyEmailDateFilter(query, filter.EmailDateOperator, filter.EmailDateValue.Value);
            }

            if (filter.StatusFilter.HasValue)
                query = query.Where(r => r.Status == filter.StatusFilter.Value);

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

    private static IQueryable<Report> ApplyImportDateFilter(
        IQueryable<Report> query,
        ComparisonOperator op,
        DateOnly value) => op switch
    {
        ComparisonOperator.GreaterThan => query.Where(r => r.ImportDate > value),
        ComparisonOperator.LessThan    => query.Where(r => r.ImportDate < value),
        _                              => query.Where(r => r.ImportDate == value),
    };

    private static IQueryable<Report> ApplyEmailDateFilter(
        IQueryable<Report> query,
        ComparisonOperator op,
        DateOnly value) => op switch
    {
        ComparisonOperator.GreaterThan => query.Where(r => r.EmailDate > value),
        ComparisonOperator.LessThan    => query.Where(r => r.EmailDate < value),
        _                              => query.Where(r => r.EmailDate == value),
    };
}
