using Microsoft.EntityFrameworkCore;
using Rentier.Application.Enums;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure.Repositories;

public sealed class FilingRepository : IFilingRepository
{
    private readonly AppDbContext _db;

    public FilingRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Filing?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Filings.FindAsync([id], ct);

    public async Task<IReadOnlyList<Filing>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.Filings.AsNoTracking().ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<Filing?> GetByTaxPeriodAsync(Guid taxpayerProfileId, DateOnly taxPeriod, CancellationToken ct = default)
        => await _db.Filings
            .AsNoTracking()
            .FirstOrDefaultAsync(f =>
                f.TaxpayerProfileId == taxpayerProfileId &&
                f.TaxPeriod == taxPeriod, ct);

    public async Task<bool> ExistsByIncomeAsync(
        Guid taxpayerProfileId,
        string payingEntity,
        DateOnly incomeDate,
        decimal grossIncomeRsd,
        CancellationToken ct = default)
        => await _db.Filings
            .AsNoTracking()
            .AnyAsync(f =>
                f.TaxpayerProfileId == taxpayerProfileId &&
                f.PayingEntity == payingEntity &&
                f.IncomeDate == incomeDate &&
                f.GrossIncomeRsd == grossIncomeRsd, ct);

    public async Task<IReadOnlyList<Filing>> GetByReportIdAsync(Guid reportId, CancellationToken ct = default)
    {
        var list = await _db.Filings
            .AsNoTracking()
            .Where(f => f.ReportId == reportId)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task AddAsync(Filing filing, CancellationToken ct = default)
    {
        _db.Filings.Add(filing);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Filing filing, CancellationToken ct = default)
    {
        var stale = _db.ChangeTracker.Entries<Filing>()
            .FirstOrDefault(e => e.Entity.Id == filing.Id);
        if (stale != null)
            stale.State = EntityState.Detached;

        _db.Filings.Update(filing);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Filings.FindAsync([id], ct);
        if (entity is not null)
        {
            _db.Filings.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<(IReadOnlyList<Filing> Items, int TotalCount)> GetPagedAsync(
        FilingFilterMode filter, int skip, int take,
        FilingSortColumn sortColumn = FilingSortColumn.FilingDeadline,
        bool sortDescending = true,
        CancellationToken ct = default)
    {
        var query = _db.Filings.AsNoTracking();

        if (filter == FilingFilterMode.Unpaid)
            query = query.Where(f =>
                f.Status == FilingStatus.Init || f.Status == FilingStatus.Filed);

        var total = await query.CountAsync(ct);

        IOrderedQueryable<Filing> ordered = (sortColumn, sortDescending) switch
        {
            (FilingSortColumn.FilingDeadline,   true)  => query.OrderByDescending(f => f.FilingDeadline),
            (FilingSortColumn.FilingDeadline,   false) => query.OrderBy(f => f.FilingDeadline),
            (FilingSortColumn.Status,           true)  => query.OrderByDescending(f => (int)f.Status),
            (FilingSortColumn.Status,           false) => query.OrderBy(f => (int)f.Status),
            (FilingSortColumn.IncomeType,       true)  => query.OrderByDescending(f => (int)f.IncomeType),
            (FilingSortColumn.IncomeType,       false) => query.OrderBy(f => (int)f.IncomeType),
            (FilingSortColumn.PayingEntity,     true)  => query.OrderByDescending(f => f.PayingEntity),
            (FilingSortColumn.PayingEntity,     false) => query.OrderBy(f => f.PayingEntity),
            (FilingSortColumn.TaxPayable,       true)  => query.OrderByDescending(f => f.TaxPayableRsd),
            (FilingSortColumn.TaxPayable,       false) => query.OrderBy(f => f.TaxPayableRsd),
            (FilingSortColumn.PaymentReference, true)  => query.OrderByDescending(f => f.PaymentReference),
            (FilingSortColumn.PaymentReference, false) => query.OrderBy(f => f.PaymentReference),
            _ => throw new ArgumentOutOfRangeException(nameof(sortColumn))
        };

        var items = await ordered
            .ThenBy(f => f.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items.AsReadOnly(), total);
    }

    public async Task<int> GetFilingCountByReportIdAsync(Guid reportId, CancellationToken ct = default)
        => await _db.Filings.AsNoTracking().CountAsync(f => f.ReportId == reportId, ct);

    public async Task DeleteByReportIdAsync(Guid reportId, CancellationToken ct = default)
    {
        var filings = await _db.Filings
            .Where(f => f.ReportId == reportId)
            .ToListAsync(ct);
        if (filings.Count == 0)
            return;
        _db.Filings.RemoveRange(filings);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Filing>> GetUpcomingAsync(DateOnly today, int days, CancellationToken ct = default)
    {
        var limit = today.AddDays(days);
        var list = await _db.Filings.AsNoTracking()
            .Where(f => (f.Status == FilingStatus.Init || f.Status == FilingStatus.Filed)
                     && f.FilingDeadline >= today
                     && f.FilingDeadline <= limit)
            .OrderBy(f => f.FilingDeadline)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<Filing>> GetOverdueAsync(DateOnly today, CancellationToken ct = default)
    {
        var list = await _db.Filings.AsNoTracking()
            .Where(f => f.Status != FilingStatus.Paid && f.FilingDeadline < today)
            .OrderBy(f => f.FilingDeadline)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<(int InitCount, int FiledCount, int PaidCount, decimal TotalUnpaidRsd)> GetFilingStatsAsync(CancellationToken ct = default)
    {
        var counts = await _db.Filings.AsNoTracking()
            .GroupBy(f => f.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // SQLite cannot SUM decimal columns server-side; fetch only the relevant
        // values and aggregate in memory.
        var unpaidAmounts = await _db.Filings.AsNoTracking()
            .Where(f => f.Status != FilingStatus.Paid)
            .Select(f => f.TaxPayableRsd)
            .ToListAsync(ct);

        var initCount   = counts.FirstOrDefault(g => g.Status == FilingStatus.Init)?.Count  ?? 0;
        var filedCount  = counts.FirstOrDefault(g => g.Status == FilingStatus.Filed)?.Count ?? 0;
        var paidCount   = counts.FirstOrDefault(g => g.Status == FilingStatus.Paid)?.Count  ?? 0;
        var totalUnpaid = unpaidAmounts.Sum();
        return (initCount, filedCount, paidCount, totalUnpaid);
    }

    public async Task<DateOnly?> GetEarliestIncomeDateByReportIdAsync(Guid reportId, CancellationToken ct = default)
    {
        var dates = await _db.Filings.AsNoTracking()
            .Where(f => f.ReportId == reportId)
            .Select(f => (DateOnly?)f.IncomeDate)
            .ToListAsync(ct);
        return dates.Count == 0 ? null : dates.Min();
    }

    public async Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return;
        var filings = await _db.Filings
            .Where(f => ids.Contains(f.Id))
            .ToListAsync(ct);
        if (filings.Count == 0) return;
        _db.Filings.RemoveRange(filings);
        await _db.SaveChangesAsync(ct);
    }
}
