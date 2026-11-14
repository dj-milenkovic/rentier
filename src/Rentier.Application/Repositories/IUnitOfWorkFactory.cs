namespace Rentier.Application.Repositories;

/// <summary>
/// Creates a fresh <see cref="IUnitOfWork"/> per logical operation. Handlers that write
/// through more than one repository call must go through a unit of work so the writes
/// commit (or fail) together. The caller owns the returned instance and must dispose it.
/// </summary>
public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();
}
