namespace Rentier.Application.Repositories;

/// <summary>
/// A single logical write operation spanning more than one repository call.
/// Repositories exposed here share one underlying session and only <b>stage</b> their
/// changes; nothing reaches the database until <see cref="SaveChangesAsync"/> is called,
/// which persists every staged change atomically (all or nothing).
/// Disposing without saving discards everything that was staged.
/// </summary>
/// <remarks>
/// Reads through these repositories query the database and therefore do <b>not</b> see
/// changes that are staged but not yet saved.
/// </remarks>
public interface IUnitOfWork : IAsyncDisposable
{
    IReportRepository Reports { get; }

    IFilingRepository Filings { get; }

    /// <summary>Atomically persists every change staged through this unit of work.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
