using Microsoft.EntityFrameworkCore;
using Rentier.Application.Interfaces;

namespace Rentier.Infrastructure.Persistence;

internal sealed class EfUnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly DbContextOptions<AppDbContext> _options;

    public EfUnitOfWorkFactory(DbContextOptions<AppDbContext> options) => _options = options;

    // The context is created directly (not resolved from DI) so the unit of work owns its
    // lifetime: the root container would otherwise keep every transient context alive
    // until application shutdown.
    public IUnitOfWork Create() => new EfUnitOfWork(new AppDbContext(_options));
}
