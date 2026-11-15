using Rentier.Application.Repositories;

namespace Rentier.Application.Interfaces;

/// <summary>
/// A single logical write operation spanning more than one repository call.
/// Repositories exposed here share one underlying session and only <b>stage</b> their
/// changes; nothing reaches the database until <see cref="SaveChangesAsync"/> is called,
/// which persists every staged change atomically (all or nothing).
/// Disposing without saving discards everything that was staged.
/// </summary>
/// <remarks>
/// Read visibility of staged (unsaved) changes differs per method:
/// <list type="bullet">
/// <item><b>Queries</b> (lists, paging, existence checks) read the database and do <b>not</b>
/// see staged changes.</item>
/// <item><b>GetByIdAsync</b> returns the instance this unit of work already tracks, if any —
/// including one that has been modified or staged for deletion. Do not use it to check
/// whether a staged delete took effect.</item>
/// <item><see cref="IFilingRepository.GetByIncomeEventAsync"/> deliberately reflects staged
/// changes (inserts and updates matched on their new values, deletes excluded), because
/// duplicate detection must hold within a single operation as well as across operations.</item>
/// </list>
/// </remarks>
public interface IUnitOfWork : IAsyncDisposable
{
    IReportRepository Reports { get; }

    IFilingRepository Filings { get; }

    /// <summary>Atomically persists every change staged through this unit of work.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
