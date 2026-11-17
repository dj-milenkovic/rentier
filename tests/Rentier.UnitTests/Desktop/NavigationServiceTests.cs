using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rentier.Application.Repositories;
using Rentier.Desktop.Composition;
using Rentier.Desktop.Services;
using Xunit;

namespace Rentier.UnitTests;

public class NavigationServiceTests
{
    private static (NavigationService Service, List<NavigationRequest> Requests) CreateObserved()
    {
        var service = new NavigationService();
        var requests = new List<NavigationRequest>();
        service.NavigationRequested += requests.Add;
        return (service, requests);
    }

    [Fact]
    public void NavigateToFilings_WithReportId_RaisesFilingsRequestForThatReport()
    {
        var (service, requests) = CreateObserved();
        var reportId = Guid.NewGuid();

        service.NavigateToFilings(reportId);

        requests.Should().Equal(new NavigationRequest(NavigationTarget.Filings, reportId));
    }

    [Fact]
    public void NavigateToFilings_WithoutReportId_RaisesUnfilteredFilingsRequest()
    {
        var (service, requests) = CreateObserved();

        service.NavigateToFilings();

        requests.Should().Equal(new NavigationRequest(NavigationTarget.Filings, null));
    }

    [Fact]
    public void NavigateToManualFiling_Called_RaisesManualFilingRequest()
    {
        var (service, requests) = CreateObserved();

        service.NavigateToManualFiling();

        requests.Should().Equal(new NavigationRequest(NavigationTarget.ManualFiling));
    }

    [Fact]
    public void NavigateToFilings_NoSubscriber_DoesNotThrow()
    {
        var service = new NavigationService();

        var act = () => service.NavigateToFilings(Guid.NewGuid());

        act.Should().NotThrow();
    }

    [Fact]
    public void AddDesktopServices_NavigationFaces_ResolveToSameSingleton()
    {
        using var provider = new ServiceCollection().AddDesktopServices().BuildServiceProvider();

        var pageFacing = provider.GetRequiredService<INavigationService>();
        var shellFacing = provider.GetRequiredService<INavigationRequestSource>();

        pageFacing.Should().BeSameAs(shellFacing);
    }

    [Fact]
    public void AddDesktopServices_DialogServices_AllResolve()
    {
        var services = new ServiceCollection().AddDesktopServices();
        // ReportImportDialogService loads importers through the Application handler.
        services.AddSingleton(Substitute.For<IImporterRepository>());
        using var provider = services.BuildServiceProvider();

        var resolved = new object[]
        {
            provider.GetRequiredService<IConfirmationService>(),
            provider.GetRequiredService<IFileSaveService>(),
            provider.GetRequiredService<IReportImportDialogService>(),
        };

        resolved.Should().AllSatisfy(s => s.Should().NotBeNull());
    }
}
