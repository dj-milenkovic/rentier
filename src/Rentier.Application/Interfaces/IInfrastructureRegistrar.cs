using Microsoft.Extensions.DependencyInjection;

namespace Rentier.Application.Interfaces;

/// <summary>
/// Contract for registering infrastructure-layer services into the DI container.
/// Implemented in Rentier.Infrastructure and discovered at runtime by the Desktop layer.
/// </summary>
public interface IInfrastructureRegistrar
{
    /// <summary>
    /// Registers all infrastructure services (repositories, DbContext, HTTP clients,
    /// credential store, parsers, serializers, sync services) into the service collection.
    /// </summary>
    /// <param name="services">The DI service collection to register into.</param>
    /// <param name="dbPath">Absolute path to the SQLite database file.</param>
    Task RegisterServicesAsync(IServiceCollection services, string dbPath);
}
