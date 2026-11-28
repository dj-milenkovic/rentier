using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Rentier.Application.Interfaces;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure.Repositories;

/// <summary>
/// Shared commit behaviour for repositories that can take part in an <see cref="IUnitOfWork"/>.
/// <list type="bullet">
/// <item><b>Standalone</b> (the DI registration): the repository owns its context and every
/// write method commits immediately as a complete single-statement operation.</item>
/// <item><b>Staging</b> (created by <see cref="EfUnitOfWork"/>): writes are only staged on the
/// shared context; the owning unit of work commits them atomically.</item>
/// </list>
/// </summary>
public abstract class UnitOfWorkAwareRepository
{
    // private protected: only Infrastructure repositories can derive, and only EfUnitOfWork
    // (via the derived types' internal constructors) can create a staging instance.
    private protected UnitOfWorkAwareRepository(AppDbContext db, bool isStaging)
    {
        Db = db;
        IsStaging = isStaging;
    }

    private protected AppDbContext Db { get; }

    /// <summary>True when owned by a unit of work, i.e. writes are staged rather than committed.</summary>
    private protected bool IsStaging { get; }

    /// <summary>Commits immediately when standalone; a no-op while staging.</summary>
    private protected Task CommitIfStandaloneAsync(CancellationToken ct)
        => IsStaging ? Task.CompletedTask : Db.SaveChangesAsync(ct);

    /// <summary>
    /// Loads an entity by key as it is <b>now</b>, for a caller that may go on to change it.
    /// <list type="bullet">
    /// <item><b>Staging</b>: served from the shared context first, so a change staged earlier in
    /// the same unit of work is visible.</item>
    /// <item><b>Standalone</b>: the context outlives a single operation (the Filings page keeps
    /// its handlers, and so their contexts, for as long as it is open), and <c>FindAsync</c>
    /// would return the copy cached by an earlier call even after another context changed the
    /// row. Saving that copy back writes every stale column over the newer values (issue #119).
    /// Each standalone call is a complete, already-committed operation, so nothing pending is
    /// lost by clearing the tracker; the row is then read fresh from the database.</item>
    /// </list>
    /// </summary>
    private protected async Task<TEntity?> FindCurrentAsync<TEntity>(
        DbSet<TEntity> set, object[] key, CancellationToken ct)
        where TEntity : class
    {
        if (!IsStaging)
            Db.ChangeTracker.Clear();
        return await set.FindAsync(key, ct);
    }

    /// <summary>
    /// The tracked entries of <typeparamref name="TEntity"/> without running DetectChanges,
    /// which would otherwise scan every tracked entity on each call. Safe for repository
    /// code because staged states are set explicitly by Add/Update/Remove/Attach.
    /// </summary>
    private protected List<EntityEntry<TEntity>> TrackedEntriesWithoutDetectingChanges<TEntity>()
        where TEntity : class
    {
        var autoDetect = Db.ChangeTracker.AutoDetectChangesEnabled;
        Db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            return Db.ChangeTracker.Entries<TEntity>().ToList();
        }
        finally
        {
            Db.ChangeTracker.AutoDetectChangesEnabled = autoDetect;
        }
    }

    /// <summary>
    /// Stages a full update of <paramref name="entity"/> without clobbering a change already
    /// staged for the same key on the shared context:
    /// <list type="bullet">
    /// <item>staged insert: stays an insert, now carrying <paramref name="entity"/>'s values;</item>
    /// <item>staged delete: rejected — updating a row this operation is removing is a bug;</item>
    /// <item>otherwise: any stale tracked copy is replaced and the entity is marked modified.</item>
    /// </list>
    /// </summary>
    private protected void StageUpdate<TEntity>(DbSet<TEntity> set, TEntity entity, Func<TEntity, bool> hasSameKey)
        where TEntity : class
    {
        var tracked = TrackedEntriesWithoutDetectingChanges<TEntity>().FirstOrDefault(e => hasSameKey(e.Entity));

        switch (tracked?.State)
        {
            case null:
                set.Update(entity);
                return;
            case EntityState.Deleted:
                throw new InvalidOperationException(
                    $"{typeof(TEntity).Name} is staged for deletion in this unit of work and cannot be updated.");
            case EntityState.Added:
                if (!ReferenceEquals(tracked.Entity, entity))
                {
                    tracked.State = EntityState.Detached;
                    set.Add(entity);
                }
                return;
            default:
                if (!ReferenceEquals(tracked.Entity, entity))
                    tracked.State = EntityState.Detached;
                set.Update(entity);
                return;
        }
    }

    /// <summary>
    /// Stages a change to only the named properties of <paramref name="entity"/>, so the UPDATE
    /// writes just those columns (e.g. a status change must not rewrite a report's attachment
    /// BLOB). A staged insert already carries the new values and is left as is.
    /// </summary>
    private protected void StagePropertyUpdate<TEntity>(
        TEntity entity,
        Func<TEntity, bool> hasSameKey,
        params string[] propertyNames)
        where TEntity : class
    {
        var tracked = TrackedEntriesWithoutDetectingChanges<TEntity>().FirstOrDefault(e => hasSameKey(e.Entity));

        switch (tracked?.State)
        {
            case EntityState.Deleted:
                throw new InvalidOperationException(
                    $"{typeof(TEntity).Name} is staged for deletion in this unit of work and cannot be updated.");
            case EntityState.Added:
                if (!ReferenceEquals(tracked.Entity, entity))
                    CopyValues(tracked, entity, propertyNames, markModified: false);
                return;
            case null:
                var attached = Db.Attach(entity);
                foreach (var name in propertyNames)
                    attached.Property(name).IsModified = true;
                return;
            default:
                if (ReferenceEquals(tracked.Entity, entity))
                {
                    foreach (var name in propertyNames)
                        tracked.Property(name).IsModified = true;
                }
                else
                {
                    CopyValues(tracked, entity, propertyNames, markModified: true);
                }
                return;
        }
    }

    private static void CopyValues<TEntity>(
        EntityEntry<TEntity> target, TEntity source, string[] propertyNames, bool markModified)
        where TEntity : class
    {
        foreach (var name in propertyNames)
        {
            var property = target.Property(name);
            property.CurrentValue = property.Metadata.GetGetter().GetClrValue(source);
            if (markModified)
                property.IsModified = true;
        }
    }
}
