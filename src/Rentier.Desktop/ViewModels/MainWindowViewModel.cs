using Microsoft.Extensions.DependencyInjection;
using System.Reactive.Disposables;
using ReactiveUI;
using Rentier.Desktop.Resources;

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
        SettingsViewModel settingsVm)
    {
        // ── Dashboard navigation ──────────────────────────────────────────────
        Action navigateToDashboardFilings = () =>
        {
            var filingsEntry = NavigationEntries?.FirstOrDefault(e => e.ViewModel is FilingsViewModel);
            if (filingsEntry is not null)
                SelectedEntry = filingsEntry;
        };

        var dashboardVm = ActivatorUtilities.CreateInstance<DashboardViewModel>(
            provider, navigateToDashboardFilings);

        // ── Manual filing navigation (back to filings) ────────────────────────
        // Declared here so the closure can reference filingsVm after it is created below.
        FilingsViewModel? filingsVm = null;
        Action navigateToManualFiling = () =>
        {
            var manualVm = ActivatorUtilities.CreateInstance<ManualFilingViewModel>(
                provider, (Action)(() =>
                {
                    if (filingsVm is not null) filingsVm.ShowAll = true;
                    var filingsEntry = NavigationEntries?.FirstOrDefault(e => e.ViewModel is FilingsViewModel);
                    if (filingsEntry is not null) SelectedEntry = filingsEntry;
                }));
            CurrentViewModel = manualVm;
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
            var filingsEntry = NavigationEntries?.FirstOrDefault(e => e.ViewModel is FilingsViewModel);
            if (filingsEntry is not null)
                SelectedEntry = filingsEntry;
        };

        var syncVm = ActivatorUtilities.CreateInstance<SyncViewModel>(
            provider, navigateToFilings_sync);

        NavigationEntries = new List<NavigationEntry>
        {
            new(Strings.Nav_Dashboard, dashboardVm),
            new(Strings.Nav_Filings, filingsVm),
            new(Strings.Nav_Reports, reportsVm),
            new(Strings.Nav_Sync, syncVm),
            new(Strings.Nav_Settings, settingsVm)
        };

        _selectedEntry = NavigationEntries[0];
        _currentViewModel = dashboardVm;

        // M1: subscription moved into WhenActivated so it is disposed on deactivation
        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.SelectedEntry)
                .Subscribe(entry => { if (entry is not null) CurrentViewModel = entry.ViewModel; })
                .DisposeWith(disposables);
        });
    }
}
