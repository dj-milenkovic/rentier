using Microsoft.EntityFrameworkCore;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure.Repositories;

public sealed class ImporterRepository : IImporterRepository
{
    private readonly AppDbContext _db;

    public ImporterRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Importer?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // The context outlives one operation: never serve a copy cached by an earlier call (#119).
        _db.StartFromCurrentState();
        return await _db.Importers.FindAsync([id], ct);
    }

    public async Task<IReadOnlyList<Importer>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.Importers.AsNoTracking().ToListAsync(ct);
        return list.AsReadOnly();
    }

    public async Task AddAsync(Importer importer, CancellationToken ct = default)
    {
        _db.Importers.Add(importer);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Importer importer, CancellationToken ct = default)
    {
        // Forget any copy cached by an earlier call so it can't conflict with the incoming one.
        _db.StartFromCurrentState();
        _db.Importers.Update(importer);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _db.StartFromCurrentState();
        var entity = await _db.Importers.FindAsync([id], ct);
        if (entity is not null)
        {
            _db.Importers.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }
}
