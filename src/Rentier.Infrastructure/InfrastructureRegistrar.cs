using Microsoft.Extensions.DependencyInjection;
using Rentier.Application.Interfaces;

namespace Rentier.Infrastructure;

public sealed class InfrastructureRegistrar : IInfrastructureRegistrar
{
    public async Task RegisterServicesAsync(IServiceCollection services, string dbPath)
    {
        await services.AddInfrastructureServicesAsync(dbPath);
        services.AddTransient<IDatabaseInitializer, DatabaseInitializer>();
    }
}
