// contracts/IImporterRepository.cs
// Feature 005 — Importer Configuration
//
// STATUS: PRE-EXISTING — this interface already exists at:
//   src/Rentier.Application/Repositories/IImporterRepository.cs
//
// It was created as part of the initial Application scaffolding and is used
// as-is by Feature 005. No changes are required to this interface.
//
// The repository implementation (ImporterRepository.cs) is NEW in Feature 005.

using Rentier.Domain.Entities;

namespace Rentier.Application.Repositories;

/// <summary>
/// Repository contract for CRUD operations on <see cref="Importer"/> entities.
/// Defined in Application layer; implemented in Infrastructure layer.
/// </summary>
public interface IImporterRepository
{
    /// <summary>
    /// Returns the importer with the given <paramref name="id"/>, or <c>null</c> if not found.
    /// </summary>
    Task<Importer?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all importers as a read-only list.
    /// Returns an empty list when no importers exist.
    /// </summary>
    Task<IReadOnlyList<Importer>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists a new <paramref name="importer"/> entity to the database.
    /// The entity must have been created via <see cref="Importer.Create"/>.
    /// </summary>
    Task AddAsync(Importer importer, CancellationToken ct = default);

    /// <summary>
    /// Persists updates to an existing <paramref name="importer"/> entity.
    /// The entity's <see cref="Importer.Id"/> must match an existing database row.
    /// </summary>
    Task UpdateAsync(Importer importer, CancellationToken ct = default);

    /// <summary>
    /// Deletes the importer with the given <paramref name="id"/>.
    /// This is a no-op if no row with that ID exists.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

// ─── Implementation Notes ────────────────────────────────────────────────────
//
// ImporterRepository (Infrastructure) implementation guidance:
//
//   GetByIdAsync:
//     return await _context.Importers.FindAsync([id], ct);
//     ⚠ DO NOT use FirstOrDefaultAsync — use FindAsync for PK lookups (identity cache + correct semantics).
//
//   GetAllAsync:
//     return await _context.Importers
//         .AsNoTracking()
//         .ToListAsync(ct);  // cast return to IReadOnlyList<Importer>
//
//   AddAsync:
//     _context.Importers.Add(importer);
//     await _context.SaveChangesAsync(ct);
//
//   UpdateAsync:
//     // Detach any tracked instance to avoid conflicts
//     var tracked = _context.ChangeTracker.Entries<Importer>()
//         .FirstOrDefault(e => e.Entity.Id == importer.Id);
//     if (tracked is not null) tracked.State = EntityState.Detached;
//     _context.Importers.Update(importer);
//     await _context.SaveChangesAsync(ct);
//
//   DeleteAsync:
//     ⚠ DO NOT use ExecuteDeleteAsync — the EF Core SQLite in-memory provider used in
//     infrastructure tests does not support it.
//     var entity = await _context.Importers.FindAsync([id], ct);
//     if (entity is not null)
//     {
//         _context.Importers.Remove(entity);
//         await _context.SaveChangesAsync(ct);
//     }
//     // FindAsync returns null when no matching row exists — this is a safe no-op.
