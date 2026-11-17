using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Rentier.Application.Enums;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure.Repositories;

public sealed class FilingRepository : UnitOfWorkAwareRepository, IFilingRepository
{
    /// <summary>Standalone repository (the DI registration): every write commits immediately.</summary>
    public FilingRepository(AppDbContext db) : base(db, isStaging: false)
    {
    }

    /// <summary>
    /// With <paramref name="isStaging"/> true the repository only stages changes on the
    /// shared context; the owning <see cref="IUnitOfWork"/> commits them atomically.
    /// </summary>
    internal FilingRepository(AppDbContext db, bool isStaging) : base(db, isStaging)
    {
    }

    public async Task<Filing?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await Db.Filings.FindAsync([id], ct);

    public async Task<IReadOnlyList<Filing>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await Db.Filings.AsNoTracking().ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<Filing?> GetByTaxPeriodAsync(Guid taxpayerProfileId, DateOnly taxPeriod, CancellationToken ct = default)
        => await Db.Filings
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
        => await Db.Filings
            .AsNoTracking()
            .AnyAsync(f =>
                f.TaxpayerProfileId == taxpayerProfileId &&
                f.PayingEntity == payingEntity &&
                f.IncomeDate == incomeDate &&
                f.GrossIncomeRsd == grossIncomeRsd, ct);

    public async Task<IReadOnlyList<Filing>> GetByIncomeEventAsync(
        Guid taxpayerProfileId,
        string payingEntity,
        DateOnly incomeDate,
        CancellationToken ct = default)
    {
        var key = new IncomeEventKey(taxpayerProfileId, payingEntity, incomeDate);

        var persisted = await Db.Filings
            .AsNoTracking()
            .Where(SameIncomeEventAs(key))
            .ToListAsync(ct);

        if (!IsStaging)
            return persisted.AsReadOnly();

        // Inside a unit of work, changes staged earlier in the same operation must count.
        // A tracked filing's current (staged) values win over its database row: staged
        // inserts and updates are matched on their new values, staged deletes drop out.
        // Entry states are set explicitly by Add/Update/Remove, so change detection (a scan
        // of every tracked entity) is unnecessary here and is skipped.
        var tracked = TrackedEntriesWithoutDetectingChanges<Filing>();
        var trackedIds = tracked.Select(e => e.Entity.Id).ToHashSet();

        var fromDatabase = persisted.Where(f => !trackedIds.Contains(f.Id));
        var fromTracker = tracked
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Unchanged)
            .Select(e => e.Entity)
            .Where(f => MATCHES_INCOME_EVENT(f, key));

        return fromDatabase.Concat(fromTracker).ToList().AsReadOnly();
    }

    /// <summary>The identity of an income event for duplicate/correction detection.</summary>
    private sealed record IncomeEventKey(Guid TaxpayerProfileId, string PayingEntity, DateOnly IncomeDate);

    // One predicate for both the database query and the staged-change lookup, so duplicate
    // detection within a unit of work can never drift from detection across statements.
    // SQLite's default BINARY collation matches ordinal C# string equality. Compiled once.
    private static readonly Expression<Func<Filing, IncomeEventKey, bool>> SAME_INCOME_EVENT =
        (f, key) =>
            f.TaxpayerProfileId == key.TaxpayerProfileId &&
            f.PayingEntity == key.PayingEntity &&
            f.IncomeDate == key.IncomeDate;

    private static readonly Func<Filing, IncomeEventKey, bool> MATCHES_INCOME_EVENT = SAME_INCOME_EVENT.Compile();

    /// <summary>
    /// Binds <paramref name="key"/> into <see cref="SAME_INCOME_EVENT"/> for LINQ-to-Entities.
    /// The key's members are accessed on a constant, which EF Core extracts as SQL parameters,
    /// so the query shape (and its cached plan) is the same for every key.
    /// </summary>
    private static Expression<Func<Filing, bool>> SameIncomeEventAs(IncomeEventKey key)
    {
        var filing = SAME_INCOME_EVENT.Parameters[0];
        var body = new ParameterReplacer(SAME_INCOME_EVENT.Parameters[1], Expression.Constant(key))
            .Visit(SAME_INCOME_EVENT.Body);
        return Expression.Lambda<Func<Filing, bool>>(body, filing);
    }

    private sealed class ParameterReplacer(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == parameter ? replacement : base.VisitParameter(node);
    }

    public async Task<IReadOnlyList<Filing>> GetByReportIdAsync(Guid reportId, CancellationToken ct = default)
    {
        var list = await Db.Filings
            .AsNoTracking()
            .Where(f => f.ReportId == reportId)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task AddAsync(Filing filing, CancellationToken ct = default)
    {
        Db.Filings.Add(filing);
        await CommitIfStandaloneAsync(ct);
    }

    public async Task UpdateAsync(Filing filing, CancellationToken ct = default)
    {
        StageUpdate(Db.Filings, filing, tracked => tracked.Id == filing.Id);
        await CommitIfStandaloneAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await Db.Filings.FindAsync([id], ct);
        if (entity is not null)
        {
            Db.Filings.Remove(entity);
            await CommitIfStandaloneAsync(ct);
        }
    }

    public async Task<(IReadOnlyList<Filing> Items, int TotalCount)> GetPagedAsync(
        FilingFilterMode filter, int skip, int take,
        FilingSortColumn sortColumn = FilingSortColumn.FilingDeadline,
        bool sortDescending = true,
        FilingColumnFilter? columnFilter = null,
        CancellationToken ct = default)
    {
        var query = Db.Filings.AsNoTracking();

        if (filter == FilingFilterMode.Unpaid)
            query = query.Where(f =>
                f.Status == FilingStatus.Init || f.Status == FilingStatus.Filed);

        query = ApplyColumnFilters(query, columnFilter);

        var total = await query.CountAsync(ct);

        var ordered = ApplySort(query, sortColumn, sortDescending);

        var items = await ordered
            .ThenBy(f => f.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items.AsReadOnly(), total);
    }

    private static IQueryable<Filing> ApplyColumnFilters(IQueryable<Filing> query, FilingColumnFilter? columnFilter)
    {
        if (columnFilter is null)
            return query;

        if (columnFilter.Status.HasValue)
            query = query.Where(f => f.Status == columnFilter.Status.Value);
        if (columnFilter.IncomeType.HasValue)
            query = query.Where(f => f.IncomeType == columnFilter.IncomeType.Value);
        if (!string.IsNullOrEmpty(columnFilter.PayingEntity))
            query = query.Where(f => EF.Functions.Like(f.PayingEntity, $"%{columnFilter.PayingEntity}%"));
        if (columnFilter.FilingDeadline.HasValue)
            query = query.Where(f => f.FilingDeadline == columnFilter.FilingDeadline.Value);
        if (!string.IsNullOrEmpty(columnFilter.PaymentReference))
            query = query.Where(f => f.PaymentReference != null && EF.Functions.Like(f.PaymentReference, $"%{columnFilter.PaymentReference}%"));

        // Feature 050: multi-select enum filters and text-based deadline search
        if (columnFilter.Statuses is { Count: > 0 } statuses)
            query = query.Where(f => statuses.Contains(f.Status));
        if (columnFilter.IncomeTypes is { Count: > 0 } incomeTypes)
            query = query.Where(f => incomeTypes.Contains(f.IncomeType));
        if (!string.IsNullOrEmpty(columnFilter.FilingDeadlineText))
        {
            var dlPattern = $"%{columnFilter.FilingDeadlineText}%";
            query = query.Where(f => EF.Functions.Like(f.FilingDeadline.ToString(), dlPattern));
        }

        return query;
    }

    private static IOrderedQueryable<Filing> ApplySort(
        IQueryable<Filing> query, FilingSortColumn sortColumn, bool sortDescending)
        => (sortColumn, sortDescending) switch
        {
            (FilingSortColumn.FilingDeadline, true) => query.OrderByDescending(f => f.FilingDeadline),
            (FilingSortColumn.FilingDeadline, false) => query.OrderBy(f => f.FilingDeadline),
            (FilingSortColumn.Status, true) => query.OrderByDescending(f => (int)f.Status),
            (FilingSortColumn.Status, false) => query.OrderBy(f => (int)f.Status),
            (FilingSortColumn.IncomeType, true) => query.OrderByDescending(f => (int)f.IncomeType),
            (FilingSortColumn.IncomeType, false) => query.OrderBy(f => (int)f.IncomeType),
            (FilingSortColumn.PayingEntity, true) => query.OrderByDescending(f => f.PayingEntity),
            (FilingSortColumn.PayingEntity, false) => query.OrderBy(f => f.PayingEntity),
            (FilingSortColumn.TaxPayable, true) => query.OrderByDescending(f => f.TaxPayableRsd),
            (FilingSortColumn.TaxPayable, false) => query.OrderBy(f => f.TaxPayableRsd),
            (FilingSortColumn.PaymentReference, true) => query.OrderByDescending(f => f.PaymentReference),
            (FilingSortColumn.PaymentReference, false) => query.OrderBy(f => f.PaymentReference),
            _ => throw new ArgumentOutOfRangeException(nameof(sortColumn))
        };

    public async Task<int> GetFilingCountByReportIdAsync(Guid reportId, CancellationToken ct = default)
        => await Db.Filings.AsNoTracking().CountAsync(f => f.ReportId == reportId, ct);

    public async Task DeleteByReportIdsAsync(IReadOnlyList<Guid> reportIds, CancellationToken ct = default)
    {
        if (reportIds.Count == 0)
            return;
        var filings = await Db.Filings
            .Where(f => f.ReportId != null && reportIds.Contains(f.ReportId.Value))
            .ToListAsync(ct);
        if (filings.Count == 0)
            return;
        Db.Filings.RemoveRange(filings);
        await CommitIfStandaloneAsync(ct);
    }

    public async Task DeleteByReportIdAsync(Guid reportId, CancellationToken ct = default)
    {
        var filings = await Db.Filings
            .Where(f => f.ReportId == reportId)
            .ToListAsync(ct);
        if (filings.Count == 0)
            return;
        Db.Filings.RemoveRange(filings);
        await CommitIfStandaloneAsync(ct);
    }

    public async Task<IReadOnlyList<Filing>> GetUpcomingAsync(DateOnly today, int days, CancellationToken ct = default)
    {
        var limit = today.AddDays(days);
        var list = await Db.Filings.AsNoTracking()
            .Where(f => (f.Status == FilingStatus.Init || f.Status == FilingStatus.Filed)
                     && f.FilingDeadline >= today
                     && f.FilingDeadline <= limit)
            .OrderBy(f => f.FilingDeadline)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<Filing>> GetOverdueAsync(DateOnly today, CancellationToken ct = default)
    {
        var list = await Db.Filings.AsNoTracking()
            .Where(f => f.Status != FilingStatus.Paid && f.FilingDeadline < today)
            .OrderBy(f => f.FilingDeadline)
            .ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task<(int InitCount, int FiledCount, int PaidCount, decimal TotalUnpaidRsd)> GetFilingStatsAsync(CancellationToken ct = default)
    {
        var counts = await Db.Filings.AsNoTracking()
            .GroupBy(f => f.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // SQLite cannot SUM decimal columns server-side; fetch only the relevant
        // values and aggregate in memory.
        var unpaidAmounts = await Db.Filings.AsNoTracking()
            .Where(f => f.Status != FilingStatus.Paid)
            .Select(f => f.TaxPayableRsd)
            .ToListAsync(ct);

        var initCount = counts.FirstOrDefault(g => g.Status == FilingStatus.Init)?.Count ?? 0;
        var filedCount = counts.FirstOrDefault(g => g.Status == FilingStatus.Filed)?.Count ?? 0;
        var paidCount = counts.FirstOrDefault(g => g.Status == FilingStatus.Paid)?.Count ?? 0;
        var totalUnpaid = unpaidAmounts.Sum();
        return (initCount, filedCount, paidCount, totalUnpaid);
    }

    public async Task<DateOnly?> GetEarliestIncomeDateByReportIdAsync(Guid reportId, CancellationToken ct = default)
        => await Db.Filings.AsNoTracking()
            .Where(f => f.ReportId == reportId)
            .Select(f => (DateOnly?)f.IncomeDate)
            .MinAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, (int Count, DateOnly? EarliestDate)>> GetAggregatesByReportIdsAsync(
        IReadOnlyList<Guid> reportIds, CancellationToken ct = default)
    {
        if (reportIds.Count == 0)
            return new Dictionary<Guid, (int, DateOnly?)>();

        var rows = await Db.Filings.AsNoTracking()
            .Where(f => f.ReportId.HasValue && reportIds.Contains(f.ReportId.Value))
            .GroupBy(f => f.ReportId!.Value)
            .Select(g => new
            {
                ReportId = g.Key,
                Count = g.Count(),
                EarliestDate = g.Min(f => (DateOnly?)f.IncomeDate),
            })
            .ToListAsync(ct);

        return rows.ToDictionary(x => x.ReportId, x => (x.Count, x.EarliestDate));
    }

    public async Task DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return;
        var filings = await Db.Filings
            .Where(f => ids.Contains(f.Id))
            .ToListAsync(ct);
        if (filings.Count == 0) return;
        Db.Filings.RemoveRange(filings);
        await CommitIfStandaloneAsync(ct);
    }
}
