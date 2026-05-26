using Rentier.Domain.Entities;

namespace Rentier.Application.Repositories;

public interface ITaxpayerProfileRepository
{
    Task<TaxpayerProfile?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(TaxpayerProfile profile, CancellationToken ct = default);
    Task DeleteAsync(CancellationToken ct = default);
}
