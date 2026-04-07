using Microsoft.EntityFrameworkCore;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
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
}
