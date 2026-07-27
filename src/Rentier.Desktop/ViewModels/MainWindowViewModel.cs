using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Reactive;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Desktop.Services;

namespace Rentier.Desktop.ViewModels;

public sealed class MainWindowViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    private ReactiveObject _currentViewModel;
    private NavigationEntry? _selectedEntry;
    private NavigationEntry? _lastContentEntry;

    public IReadOnlyList<NavigationEntry> NavigationEntries { get; }

    public ReactiveObject CurrentViewModel
    {
        get => _currentViewModel;
        set => this.RaiseAndSetIfChanged(ref _currentViewModel, value);
    }

    public NavigationEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => this.RaiseAndSetIfChanged(ref _selectedEntry, value);
    }

    // ── Update state ──────────────────────────────────────────────────────────

    private UpdateState _currentUpdateState = UpdateState.Idle;
    private string? _availableVersion;
    private int _downloadProgress;
    private string? _updateErrorMessage;

    public UpdateState CurrentUpdateState
    {
        get => _currentUpdateState;
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentUpdateState, value);
            this.RaisePropertyChanged(nameof(UpdateBarVisible));
            this.RaisePropertyChanged(nameof(IsUpdateAvailableState));
            this.RaisePropertyChanged(nameof(IsDownloadingState));
            this.RaisePropertyChanged(nameof(IsDownloadedState));
            this.RaisePropertyChanged(nameof(IsErrorState));
        }
    }

    public string? AvailableVersion
    {
        get => _availableVersion;
        private set
        {
            this.RaiseAndSetIfChanged(ref _availableVersion, value);
            this.RaisePropertyChanged(nameof(UpdateAvailableText));
        }
    }

    public int DownloadProgress
    {
        get => _downloadProgress;
        private set
        {
            this.RaiseAndSetIfChanged(ref _downloadProgress, value);
            this.RaisePropertyChanged(nameof(DownloadingText));
        }
    }

    public string? UpdateErrorMessage
    {
        get => _updateErrorMessage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _updateErrorMessage, value);
            this.RaisePropertyChanged(nameof(UpdateFailedText));
        }
    }

    // ── Localized format-string properties for the update notification bar ───

    public string UpdateAvailableText =>
        string.Format(_localizationService["Update_Available_Message"], AvailableVersion);

    public string DownloadingText =>
        string.Format(_localizationService["Update_Downloading_Message"], DownloadProgress);

    public string UpdateFailedText =>
        string.Format(_localizationService["Update_Error_Message"], UpdateErrorMessage);

    public bool UpdateBarVisible =>
        _currentUpdateState is UpdateState.UpdateAvailable
                            or UpdateState.Downloading
                            or UpdateState.Downloaded
                            or UpdateState.Error;

    public bool IsUpdateAvailableState => _currentUpdateState == UpdateState.UpdateAvailable;
    public bool IsDownloadingState => _currentUpdateState == UpdateState.Downloading;
    public bool IsDownloadedState => _currentUpdateState == UpdateState.Downloaded;
    public bool IsErrorState => _currentUpdateState == UpdateState.Error;

    // ── Update commands ───────────────────────────────────────────────────────

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> CheckForUpdateCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> DismissUpdateCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> BeginUpdateCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RestartNowCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> DismissRestartCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RetryDownloadCommand { get; }

    private readonly IUpdateService _updateService;
    private readonly ILocalizationService _localizationService;
    private readonly IScheduler _outputScheduler;

    public MainWindowViewModel(
        IServiceProvider provider,
        ILocalizationService localizationService,
        IUpdateService updateService,
        IScheduler? outputScheduler = null)
    {
        _updateService = updateService;
        _localizationService = localizationService;
        _outputScheduler = outputScheduler ?? RxSchedulers.MainThreadScheduler;

        // ── Update commands setup ─────────────────────────────────────────────
        var canCheck = this.WhenAnyValue(x => x.CurrentUpdateState)
            .Select(s => s == UpdateState.Idle);

        var canDismiss = this.WhenAnyValue(x => x.CurrentUpdateState)
            .Select(s => s == UpdateState.UpdateAvailable || s == UpdateState.Error);

        var canBeginUpdate = this.WhenAnyValue(x => x.CurrentUpdateState)
            .Select(s => s == UpdateState.UpdateAvailable);

        var canRestart = this.WhenAnyValue(x => x.CurrentUpdateState)
            .Select(s => s == UpdateState.Downloaded);

        var canDismissRestart = this.WhenAnyValue(x => x.CurrentUpdateState)
            .Select(s => s == UpdateState.Downloaded);

        var canRetry = this.WhenAnyValue(x => x.CurrentUpdateState)
            .Select(s => s == UpdateState.Error);

        CheckForUpdateCommand = ReactiveCommand.CreateFromTask(
            RunCheckForUpdateAsync, canCheck, outputScheduler: _outputScheduler);

        DismissUpdateCommand = ReactiveCommand.CreateFromTask(
            OnDismissUpdateAsync,
            canDismiss, outputScheduler: _outputScheduler);

        BeginUpdateCommand = ReactiveCommand.CreateFromTask(
            RunDownloadUpdateAsync, canBeginUpdate, outputScheduler: _outputScheduler);

        RestartNowCommand = ReactiveCommand.Create(
            () => updateService.ApplyUpdateAndRestart(),
            canRestart, outputScheduler: _outputScheduler);

        DismissRestartCommand = ReactiveCommand.CreateFromTask(
            OnDismissRestartAsync,
            canDismissRestart, outputScheduler: _outputScheduler);

        RetryDownloadCommand = ReactiveCommand.CreateFromTask(
            RunDownloadUpdateAsync, canRetry, outputScheduler: _outputScheduler);

        NavigationEntries = BuildNavigationEntries(provider, localizationService);

        _selectedEntry = NavigationEntries[0];
        _currentViewModel = NavigationEntries[0].ViewModel!;
        _lastContentEntry = NavigationEntries[0];
        NavigationEntries[0].IsActive = true;

        this.WhenActivated(RegisterActivation);
    }

    private async Task OnDismissUpdateAsync()
    {
        CurrentUpdateState = UpdateState.Dismissed;
        await Task.CompletedTask;
    }

    private async Task OnDismissRestartAsync()
    {
        _updateService.ScheduleUpdateOnExit();
        CurrentUpdateState = UpdateState.Idle;
        await Task.CompletedTask;
    }

    private List<NavigationEntry> BuildNavigationEntries(
        IServiceProvider provider, ILocalizationService localizationService)
    {
        // ── Dashboard navigation ──────────────────────────────────────────────
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

        // ── Manual filing navigation ──────────────────────────────────────────
        Action navigateToManualFiling = () =>
        {
            var manualVm = ActivatorUtilities.CreateInstance<ManualFilingViewModel>(
                provider, (Action)(() =>
                {
                    if (filingsVm is not null) filingsVm.ReportIdFilter = null;
                    var filingsEntry = NavigationEntries?.FirstOrDefault(e => e.ViewModel is FilingsViewModel);
                    if (filingsEntry is not null) SelectedEntry = filingsEntry;
                }));
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

        var syncVm = ActivatorUtilities.CreateInstance<SyncViewModel>(provider);

        // ── Settings sub-ViewModels ───────────────────────────────────────────
        var profileVm = provider.GetRequiredService<ProfileSettingsViewModel>();
        var holidayVm = provider.GetRequiredService<HolidaySettingsViewModel>();
        var mailboxVm = provider.GetRequiredService<MailboxSettingsViewModel>();
        var importerVm = provider.GetRequiredService<ImporterSettingsViewModel>();
        var appearanceVm = provider.GetRequiredService<AppearanceSettingsViewModel>();

        var settingsGroup = new NavigationEntry(localizationService["Nav_Settings"], viewModel: null, Icon: NavIcon("NavSettingsIcon"))
        {
            IsGroup = true,
            IsExpanded = true,
        };

        var profileChild = new NavigationEntry(localizationService["Nav_Settings_Profile"], profileVm, Icon: NavIcon("NavProfileIcon")) { IndentLevel = 1, ParentGroup = settingsGroup };
        var holidaysChild = new NavigationEntry(localizationService["Nav_Settings_Holidays"], holidayVm, Icon: NavIcon("NavHolidaysIcon")) { IndentLevel = 1, ParentGroup = settingsGroup };
        var mailboxesChild = new NavigationEntry(localizationService["Nav_Settings_Mailboxes"], mailboxVm, Icon: NavIcon("NavMailboxesIcon")) { IndentLevel = 1, ParentGroup = settingsGroup };
        var importersChild = new NavigationEntry(localizationService["Nav_Settings_Importers"], importerVm, Icon: NavIcon("NavImportersIcon")) { IndentLevel = 1, ParentGroup = settingsGroup };
        var languageChild = new NavigationEntry(localizationService["Nav_Settings_Appearance"], appearanceVm, Icon: NavIcon("NavLanguageIcon")) { IndentLevel = 1, ParentGroup = settingsGroup };

        settingsGroup.Children = new[] { profileChild, holidaysChild, mailboxesChild, importersChild, languageChild };

        return new List<NavigationEntry>
        {
            new(localizationService["Nav_Dashboard"], dashboardVm, Icon: NavIcon("NavHomeIcon")),
            new(localizationService["Nav_Filings"],   filingsVm,   Icon: NavIcon("NavFilingsIcon")),
            new(localizationService["Nav_Reports"],   reportsVm,   Icon: NavIcon("NavReportsIcon")),
            new(localizationService["Nav_Sync"],      syncVm,      Icon: NavIcon("NavSyncIcon")),
            settingsGroup,
            profileChild,
            holidaysChild,
            mailboxesChild,
            importersChild,
            languageChild,
        };
    }

    private void RegisterActivation(MultipleDisposable disposables)
    {
        this.WhenAnyValue(x => x.SelectedEntry)
            .Subscribe(entry =>
            {
                if (entry is null) return;

                if (entry.IsGroup)
                {
                    entry.ToggleExpanded();
                }
                else if (entry.ViewModel is not null)
                {
                    if (_lastContentEntry is not null)
                        _lastContentEntry.IsActive = false;
                    entry.IsActive = true;
                    _lastContentEntry = entry;
                    CurrentViewModel = entry.ViewModel;
                }

                SelectedEntry = null;
            })
            .DisposeWith(disposables);

        _localizationService.CultureChanged
            .ObserveOn(_outputScheduler)
            .Subscribe(_ =>
            {
                UpdateNavigationLabels(_localizationService);
                this.RaisePropertyChanged(nameof(UpdateAvailableText));
                this.RaisePropertyChanged(nameof(DownloadingText));
                this.RaisePropertyChanged(nameof(UpdateFailedText));
            })
            .DisposeWith(disposables);

        // Auto-check for updates in background on activation
        Observable.StartAsync(() => RunCheckForUpdateAsync(), RxSchedulers.TaskpoolScheduler)
            .Subscribe()
            .DisposeWith(disposables);
    }

    private async Task RunCheckForUpdateAsync()
    {
        CurrentUpdateState = UpdateState.Checking;
        var result = await _updateService.CheckForUpdatesAsync().ConfigureAwait(false);
        if (result.IsUpdateAvailable)
        {
            AvailableVersion = result.TargetVersion;
            CurrentUpdateState = UpdateState.UpdateAvailable;
        }
        else
        {
            CurrentUpdateState = UpdateState.Idle;
        }
    }

    private async Task RunDownloadUpdateAsync()
    {
        CurrentUpdateState = UpdateState.Downloading;
        try
        {
            await _updateService.DownloadUpdateAsync(
                progress => RxSchedulers.MainThreadScheduler.Schedule(
                    () => DownloadProgress = progress))
                .ConfigureAwait(false);

            CurrentUpdateState = UpdateState.Downloaded;
        }
        catch (Exception ex)
        {
            UpdateErrorMessage = ex.Message;
            CurrentUpdateState = UpdateState.Error;
        }
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
                DashboardViewModel => "Nav_Dashboard",
                FilingsViewModel => "Nav_Filings",
                ReportsViewModel => "Nav_Reports",
                SyncViewModel => "Nav_Sync",
                ProfileSettingsViewModel => "Nav_Settings_Profile",
                HolidaySettingsViewModel => "Nav_Settings_Holidays",
                MailboxSettingsViewModel => "Nav_Settings_Mailboxes",
                ImporterSettingsViewModel => "Nav_Settings_Importers",
                AppearanceSettingsViewModel => "Nav_Settings_Appearance",
                _ => null,
            };

            if (key is not null)
                entry.Label = loc[key];
        }
    }

    private static StreamGeometry? NavIcon(string key)
    {
        try
        {
            if (Avalonia.Application.Current?.TryGetResource(key, Avalonia.Styling.ThemeVariant.Default, out var resource) == true)
                return resource as StreamGeometry;
        }
        catch (InvalidOperationException)
        {
            // Returns null when called from a non-UI thread (e.g., tests running alongside
            // Avalonia headless tests that have initialized Application.Current).
        }
        return null;
    }
}

