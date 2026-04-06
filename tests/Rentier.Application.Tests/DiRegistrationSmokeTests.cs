using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Xunit;

namespace Rentier.Application.Tests;

public class DiRegistrationSmokeTests
{
    [Fact]
    public void ServiceCollection_WithSubstituteStubs_ResolvesAllApplicationInterfaces()
    {
        var services = new ServiceCollection();

        services.AddSingleton(Substitute.For<IFilingRepository>());
        services.AddSingleton(Substitute.For<IReportRepository>());
        services.AddSingleton(Substitute.For<IMailboxRepository>());
        services.AddSingleton(Substitute.For<IImporterRepository>());
        services.AddSingleton(Substitute.For<ITaxpayerProfileRepository>());
        services.AddSingleton(Substitute.For<IExchangeRateCacheRepository>());
        services.AddSingleton(Substitute.For<ICredentialStore>());

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IFilingRepository>().Should().NotBeNull();
        provider.GetRequiredService<IReportRepository>().Should().NotBeNull();
        provider.GetRequiredService<IMailboxRepository>().Should().NotBeNull();
        provider.GetRequiredService<IImporterRepository>().Should().NotBeNull();
        provider.GetRequiredService<ITaxpayerProfileRepository>().Should().NotBeNull();
        provider.GetRequiredService<IExchangeRateCacheRepository>().Should().NotBeNull();
        provider.GetRequiredService<ICredentialStore>().Should().NotBeNull();
    }
}
