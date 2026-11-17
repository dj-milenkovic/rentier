namespace Rentier.Desktop.Services;

/// <summary>
/// Relays navigation requests from content ViewModels to the shell. Holds no page state itself.
/// </summary>
public sealed class NavigationService : INavigationService, INavigationRequestSource
{
    public event Action<NavigationRequest>? NavigationRequested;

    public void NavigateToFilings(Guid? reportId = null) =>
        NavigationRequested?.Invoke(new NavigationRequest(NavigationTarget.Filings, reportId));

    public void NavigateToManualFiling() =>
        NavigationRequested?.Invoke(new NavigationRequest(NavigationTarget.ManualFiling));
}
