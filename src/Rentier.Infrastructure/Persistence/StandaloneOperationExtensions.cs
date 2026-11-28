using Microsoft.EntityFrameworkCore;

namespace Rentier.Infrastructure.Persistence;

/// <summary>
/// Repositories resolved from DI get a transient <see cref="AppDbContext"/>, but the handlers that
/// own them are often kept for as long as a page is open (the Filings page keeps its handlers in
/// <c>FilingsHandlers</c>). The context therefore outlives a single operation, and its change
/// tracker would keep serving the copies it cached during earlier calls — even after another
/// context changed or deleted those rows. A tracked query (<c>FindAsync</c>, or a tracking
/// <c>FirstOrDefaultAsync</c>) returns the cached instance without refreshing its values, and
/// saving it back writes every stale column over the newer ones (issue #119).
/// </summary>
internal static class StandaloneOperationExtensions
{
    /// <summary>
    /// Starts a standalone repository operation from the database's current state by forgetting
    /// every entity this context cached earlier. Only for contexts that commit each write on its
    /// own: each earlier call already saved (or deliberately abandoned) its changes, so nothing
    /// pending is lost. Never call it on a context shared by a unit of work, whose staged changes
    /// live in the change tracker until the unit of work commits.
    /// </summary>
    public static void StartFromCurrentState(this DbContext db) => db.ChangeTracker.Clear();
}
