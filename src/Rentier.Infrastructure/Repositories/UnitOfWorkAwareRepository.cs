using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
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
    private readonly bool _isStaging;

    // private protected: only Infrastructure repositories can derive, and only EfUnitOfWork
    // (via the derived types' internal constructors) can create a staging instance.
    private protected UnitOfWorkAwareRepository(AppDbContext db, bool isStaging)
    {
        Db = db;
        _isStaging = isStaging;
    }

    private protected AppDbContext Db { get; }

    /// <summary>True when owned by a unit of work, i.e. writes are staged rather than committed.</summary>
    private protected bool IsStaging => _isStaging;

    /// <summary>Commits immediately when standalone; a no-op while staging.</summary>
    private protected Task CommitIfStandaloneAsync(CancellationToken ct)
        => _isStaging ? Task.CompletedTask : Db.SaveChangesAsync(ct);

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
        var tracked = Db.ChangeTracker.Entries<TEntity>().FirstOrDefault(e => hasSameKey(e.Entity));

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
    /// Stages a change to a single property of <paramref name="entity"/>, so the UPDATE only
    /// writes that column (e.g. a status change must not rewrite a report's attachment BLOB).
    /// A staged insert already carries the new value and is left as is.
    /// </summary>
    private protected void StagePropertyUpdate<TEntity, TProperty>(
        TEntity entity,
        Func<TEntity, bool> hasSameKey,
        Expression<Func<TEntity, TProperty>> property)
        where TEntity : class
    {
        var tracked = Db.ChangeTracker.Entries<TEntity>().FirstOrDefault(e => hasSameKey(e.Entity));

        switch (tracked?.State)
        {
            case EntityState.Deleted:
                throw new InvalidOperationException(
                    $"{typeof(TEntity).Name} is staged for deletion in this unit of work and cannot be updated.");
            case EntityState.Added:
                if (!ReferenceEquals(tracked.Entity, entity))
                    tracked.Property(property).CurrentValue = property.Compile()(entity);
                return;
            case null:
                var attached = Db.Attach(entity);
                attached.Property(property).IsModified = true;
                return;
            default:
                if (ReferenceEquals(tracked.Entity, entity))
                {
                    tracked.Property(property).IsModified = true;
                }
                else
                {
                    var propertyEntry = tracked.Property(property);
                    propertyEntry.CurrentValue = property.Compile()(entity);
                    propertyEntry.IsModified = true;
                }
                return;
        }
    }
}
