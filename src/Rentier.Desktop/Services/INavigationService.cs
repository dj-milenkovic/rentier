namespace Rentier.Desktop.Services;

/// <summary>
/// Cross-page navigation requested by content ViewModels. The shell (MainWindowViewModel)
/// still owns the sidebar and the current page; this service only carries the request to it,
/// so pages never reference each other.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Shows the Filings page. With a <paramref name="reportId"/> the list is narrowed to that
    /// report's filings; without one any report filter is cleared.
    /// </summary>
    void NavigateToFilings(Guid? reportId = null);

    /// <summary>Opens a fresh Manual Filing form (hidden sub-page of Filings).</summary>
    void NavigateToManualFiling();
}

/// <summary>Where a navigation request raised through <see cref="INavigationService"/> leads.</summary>
public enum NavigationTarget
{
    Filings,
    ManualFiling,
}

/// <summary>A navigation request; <see cref="ReportId"/> only applies to <see cref="NavigationTarget.Filings"/>.</summary>
public sealed record NavigationRequest(NavigationTarget Target, Guid? ReportId = null);

/// <summary>Shell-facing side of navigation: the shell subscribes and performs the page switch.</summary>
public interface INavigationRequestSource
{
    event Action<NavigationRequest>? NavigationRequested;
}
