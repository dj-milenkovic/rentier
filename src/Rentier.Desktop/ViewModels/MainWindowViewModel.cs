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

    /// <summary>
    /// Tracks the last content entry that was navigated to (for restoring selection after
    /// a group header is clicked).
    /// </summary>
    private NavigationEntry? _lastContentEntry;

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

        // ── Settings sub-ViewModels (singleton — resolved once per app session) ───
        var profileVm    = provider.GetRequiredService<ProfileSettingsViewModel>();
        var holidayVm    = provider.GetRequiredService<HolidaySettingsViewModel>();
        var mailboxVm    = provider.GetRequiredService<MailboxSettingsViewModel>();
        var importerVm   = provider.GetRequiredService<ImporterSettingsViewModel>();
        var appearanceVm = provider.GetRequiredService<AppearanceSettingsViewModel>();

        // ── Settings group: group header + 5 child entries ────────────────────
        // Build the group header first (no children yet — set after children are created).
        var settingsGroup = new NavigationEntry(localizationService["Nav_Settings"], viewModel: null, Icon: NavIcon("NavSettingsGroupIcon"))
        {
            IsGroup    = true,
            IsExpanded = true,
        };

        // Build child entries with ParentGroup referencing the group header.
        var profileChild   = new NavigationEntry(localizationService["Nav_Settings_Profile"],    profileVm,    Icon: NavIcon("NavProfileIcon"))    { IndentLevel = 1, ParentGroup = settingsGroup };
        var holidaysChild  = new NavigationEntry(localizationService["Nav_Settings_Holidays"],   holidayVm,    Icon: NavIcon("NavHolidaysIcon"))    { IndentLevel = 1, ParentGroup = settingsGroup };
        var mailboxesChild = new NavigationEntry(localizationService["Nav_Settings_Mailboxes"],  mailboxVm,    Icon: NavIcon("NavMailboxesIcon"))   { IndentLevel = 1, ParentGroup = settingsGroup };
        var importersChild = new NavigationEntry(localizationService["Nav_Settings_Importers"],  importerVm,   Icon: NavIcon("NavImportersIcon"))   { IndentLevel = 1, ParentGroup = settingsGroup };
        var languageChild  = new NavigationEntry(localizationService["Nav_Settings_Language"],   appearanceVm, Icon: NavIcon("NavLanguageIcon"))    { IndentLevel = 1, ParentGroup = settingsGroup };

        // Wire children into the group header.
        settingsGroup.Children = new[] { profileChild, holidaysChild, mailboxesChild, importersChild, languageChild };

        // ── Flat navigation list (group header + children are all siblings) ────
        NavigationEntries = new List<NavigationEntry>
        {
            new(localizationService["Nav_Dashboard"], dashboardVm, Icon: NavIcon("NavHomeIcon")),
            new(localizationService["Nav_Filings"],   filingsVm,   Icon: NavIcon("NavFilingsIcon")),
            new(localizationService["Nav_Reports"],   reportsVm,   Icon: NavIcon("NavReportsIcon")),
            new(localizationService["Nav_Sync"],      syncVm,      Icon: NavIcon("NavSyncIcon")),
            settingsGroup,
            profileChild,    // index 5
            holidaysChild,   // index 6
            mailboxesChild,  // index 7
            importersChild,  // index 8
            languageChild,   // index 9
        };

        _selectedEntry     = NavigationEntries[0];
        _currentViewModel  = dashboardVm;
        _lastContentEntry  = NavigationEntries[0];

        // M1: subscription moved into WhenActivated so it is disposed on deactivation
        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(x => x.SelectedEntry)
                .Subscribe(entry =>
                {
                    if (entry is null) return;

                    if (entry.IsGroup)
                    {
                        // Toggle the group expand/collapse state; do NOT change CurrentViewModel.
                        entry.ToggleExpanded();
                        // Immediately restore selection to the last real content entry so the
                        // ListBox does not leave a stale highlight on the group header row.
                        SelectedEntry = _lastContentEntry ?? NavigationEntries[0];
                    }
                    else if (entry.ViewModel is not null)
                    {
                        _lastContentEntry = entry;
                        CurrentViewModel  = entry.ViewModel;
                    }
                })
                .DisposeWith(disposables);

            localizationService.CultureChanged
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdateNavigationLabels(localizationService))
                .DisposeWith(disposables);
        });
    }

    private void UpdateNavigationLabels(ILocalizationService loc)
    {
        foreach (var entry in NavigationEntries)
        {
            if (entry.IsGroup)
            {
                entry.Label = loc["Nav_Settings"];
                continue;
            }

            var key = entry.ViewModel switch
            {
                DashboardViewModel          => "Nav_Dashboard",
                FilingsViewModel            => "Nav_Filings",
                ReportsViewModel            => "Nav_Reports",
                SyncViewModel               => "Nav_Sync",
                ProfileSettingsViewModel    => "Nav_Settings_Profile",
                HolidaySettingsViewModel    => "Nav_Settings_Holidays",
                MailboxSettingsViewModel    => "Nav_Settings_Mailboxes",
                ImporterSettingsViewModel   => "Nav_Settings_Importers",
                AppearanceSettingsViewModel => "Nav_Settings_Language",
                _                           => null,
            };

            if (key is not null)
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
