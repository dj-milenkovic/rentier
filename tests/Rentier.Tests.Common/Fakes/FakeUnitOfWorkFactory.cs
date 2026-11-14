using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;

namespace Rentier.Tests.Common.Fakes;

/// <summary>
/// In-memory <see cref="IUnitOfWorkFactory"/> for handler unit tests. Every unit of work it
/// creates exposes the supplied (usually NSubstitute) repositories, so existing
/// <c>Received()</c> assertions keep working, while the factory records how many units of
/// work were created, committed and disposed.
/// </summary>
public sealed class FakeUnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly IReportRepository _reports;
    private readonly IFilingRepository _filings;

    public FakeUnitOfWorkFactory(IReportRepository reports, IFilingRepository filings)
    {
        _reports = reports;
        _filings = filings;
    }

    /// <summary>Number of units of work handed out so far.</summary>
    public int CreatedCount { get; private set; }

    /// <summary>Number of successful <see cref="IUnitOfWork.SaveChangesAsync"/> calls.</summary>
    public int CommittedCount { get; private set; }

    /// <summary>Number of units of work that were disposed.</summary>
    public int DisposedCount { get; private set; }

    /// <summary>
    /// Optional commit failure: receives the 1-based ordinal of the unit of work being saved
    /// and returns the exception to throw, or null to let the commit succeed.
    /// </summary>
    public Func<int, Exception?>? SaveFailure { get; set; }

    public IUnitOfWork Create()
    {
        CreatedCount++;
        return new FakeUnitOfWork(this, CreatedCount);
    }

    private sealed class FakeUnitOfWork(FakeUnitOfWorkFactory owner, int ordinal) : IUnitOfWork
    {
        public IReportRepository Reports => owner._reports;

        public IFilingRepository Filings => owner._filings;

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var failure = owner.SaveFailure?.Invoke(ordinal);
            if (failure is not null)
                return Task.FromException(failure);

            owner.CommittedCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            owner.DisposedCount++;
            return ValueTask.CompletedTask;
        }
    }
}
