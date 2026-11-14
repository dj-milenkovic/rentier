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
/// Reads through these repositories query the database and therefore do <b>not</b> see
/// changes that are staged but not yet saved — with one deliberate exception:
/// <see cref="IFilingRepository.GetByIncomeEventAsync"/> includes staged filings, because
/// duplicate detection must hold within a single operation as well as across operations.
/// </remarks>
public interface IUnitOfWork : IAsyncDisposable
{
    IReportRepository Reports { get; }

    IFilingRepository Filings { get; }

    /// <summary>Atomically persists every change staged through this unit of work.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
