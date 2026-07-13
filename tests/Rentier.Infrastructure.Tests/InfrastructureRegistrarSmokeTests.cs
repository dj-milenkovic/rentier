using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;

namespace Rentier.Infrastructure.Tests;

/// <summary>
/// Verifies that InfrastructureRegistrar wires all required application interfaces
/// into the DI container. Uses descriptor-level assertions to avoid instantiating
/// DB-connected services in a bare ServiceCollection.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InfrastructureRegistrarSmokeTests : IAsyncLifetime
{
    private IServiceCollection _services = new ServiceCollection();

    public async ValueTask InitializeAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"rentier_smoke_{Guid.NewGuid():N}.db");
        await new InfrastructureRegistrar().RegisterServicesAsync(_services, dbPath);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData(typeof(IDatabaseInitializer))]
    [InlineData(typeof(IFilingRepository))]
    [InlineData(typeof(IReportRepository))]
    [InlineData(typeof(IMailboxRepository))]
    [InlineData(typeof(IImporterRepository))]
    [InlineData(typeof(ITaxpayerProfileRepository))]
    [InlineData(typeof(IHolidayRepository))]
    [InlineData(typeof(IExchangeRateCacheRepository))]
    [InlineData(typeof(IExchangeRateResolver))]
    [InlineData(typeof(IStatementParser))]
    [InlineData(typeof(IXmlFilingSerializer))]
    [InlineData(typeof(ICredentialStore))]
    [InlineData(typeof(IMailboxSyncService))]
    [InlineData(typeof(IUpdateService))]
    [InlineData(typeof(IUserPreferenceRepository))]
    public void RegisterServicesAsync_AllRequiredInterfaces_AreRegistered(Type serviceType)
    {
        _services.Should().Contain(
            d => d.ServiceType == serviceType,
            because: $"{serviceType.Name} must be registered by InfrastructureRegistrar");
    }
}
