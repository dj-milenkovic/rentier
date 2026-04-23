using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Rentier.Desktop.Services;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Main window ViewModel with sidebar navigation.
/// FilingsViewModel and ManualFilingViewModel are created via ActivatorUtilities so that
/// navigation delegates (closures) can be injected at construction time — the same pattern
/// used by DashboardViewModel, ReportsViewModel and SyncViewModel.
/// </summary>
public sealed class MainWindowViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    private ReactiveObject _currentViewModel;
    private NavigationEntry _selectedEntry;

    public IReadOnlyList<NavigationEntry> NavigationEntries { get; }

    public ReactiveObject CurrentViewModel
    {
        get => _currentViewModel;
        set => this.RaiseAndSetIfChanged(ref _currentViewModel, value);
    }

    public NavigationEntry SelectedEntry
    {
        get => _selectedEntry;
        set => this.RaiseAndSetIfChanged(ref _selectedEntry, value);
    }

    public MainWindowViewModel(
        IServiceProvider provider,
        SettingsViewModel settingsVm,
        ILocalizationService localizationService)
    {
        // ── Dashboard navigation ──────────────────────────────────────────────
        // filingsVm is declared here (before navigateToDashboardFilings) so all
        // navigation callbacks can reference it via closure.
        FilingsViewModel? filingsVm = null;
        Action navigateToDashboardFilings = () =>
        {
            if (filingsVm is not null) filingsVm.ReportIdFilter = null;
            var filingsEntry = NavigationEntries?.FirstOrDefault(e => e.ViewModel is FilingsViewModel);
            if (filingsEntry is not null)
                SelectedEntry = filingsEntry;
        };

        var dashboardVm = ActivatorUtilities.CreateInstance<DashboardViewModel>(
            provider, navigateToDashboardFilings);

        // ── Manual filing navigation (back to filings) ────────────────────────
        Action navigateToManualFiling = () =>
        {
            var manualVm = ActivatorUtilities.CreateInstance<ManualFilingViewModel>(
                provider, (Action)(() =>
                {
                    if (filingsVm is not null) filingsVm.ReportIdFilter = null;
                    var filingsEntry = NavigationEntries?.FirstOrDefault(e => e.ViewModel is FilingsViewModel);
                    if (filingsEntry is not null) SelectedEntry = filingsEntry;
                }));

            // Use a transient hidden entry (not in NavigationEntries) so the ListBox shows
            // no sidebar highlight while the ManualFiling sub-page is active. The
            // WhenAnyValue(SelectedEntry) subscription handles setting CurrentViewModel.
            SelectedEntry = new NavigationEntry(string.Empty, manualVm, IsVisible: false);
        };

        filingsVm = ActivatorUtilities.CreateInstance<FilingsViewModel>(
            provider, navigateToManualFiling);

        // ── Reports navigation ────────────────────────────────────────────────
        Action<Guid> navigateToFilings = reportId =>
        {
            filingsVm.ReportIdFilter = reportId;
            var filingsEntry = NavigationEntries?.FirstOrDefault(e => e.ViewModel is FilingsViewModel);
            if (filingsEntry is not null)
                SelectedEntry = filingsEntry;
        };

        var reportsVm = ActivatorUtilities.CreateInstance<ReportsViewModel>(
            provider, navigateToFilings);

        // ── Sync navigation ───────────────────────────────────────────────────
        Action navigateToFilings_sync = () =>
        {
            if (filingsVm is not null) filingsVm.ReportIdFilter = null;
            var filingsEntry = NavigationEntries?.FirstOrDefault(e => e.ViewModel is FilingsViewModel);
            if (filingsEntry is not null)
                SelectedEntry = filingsEntry;
        };

        var syncVm = ActivatorUtilities.CreateInstance<SyncViewModel>(
            provider, navigateToFilings_sync);

        NavigationEntries = new List<NavigationEntry>
        {
            new(localizationService["Nav_Dashboard"], dashboardVm, Icon: NavIcon("NavHomeIcon")),
            new(localizationService["Nav_Filings"],   filingsVm,   Icon: NavIcon("NavFilingsIcon")),
            new(localizationService["Nav_Reports"],   reportsVm,   Icon: NavIcon("NavReportsIcon")),
            new(localizationService["Nav_Sync"],      syncVm,      Icon: NavIcon("NavSyncIcon")),
            new(localizationService["Nav_Settings"],  settingsVm,  Icon: NavIcon("NavSettingsIcon"))
        };

        _selectedEntry = NavigationEntries[0];
        _currentViewModel = dashboardVm;

        // M1: subscription moved into WhenActivated so it is disposed on deactivation
        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.SelectedEntry)
                .Subscribe(entry => { if (entry is not null) CurrentViewModel = entry.ViewModel; })
                .DisposeWith(disposables);

            localizationService.CultureChanged
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdateNavigationLabels(localizationService))
                .DisposeWith(disposables);
        });
    }

    private void UpdateNavigationLabels(ILocalizationService loc)
    {
        var keyMap = new Dictionary<Type, string>
        {
            [typeof(DashboardViewModel)] = "Nav_Dashboard",
            [typeof(FilingsViewModel)]   = "Nav_Filings",
            [typeof(ReportsViewModel)]   = "Nav_Reports",
            [typeof(SyncViewModel)]      = "Nav_Sync",
            [typeof(SettingsViewModel)]  = "Nav_Settings",
        };

        foreach (var entry in NavigationEntries.Where(e => e.IsVisible))
        {
            if (keyMap.TryGetValue(entry.ViewModel.GetType(), out var key))
                entry.Label = loc[key];
        }
    }

    private static StreamGeometry? NavIcon(string key)
    {
        if (Avalonia.Application.Current?.TryGetResource(key, Avalonia.Styling.ThemeVariant.Default, out var resource) == true)
            return resource as StreamGeometry;
        return null;
    }
}
