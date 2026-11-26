using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.Primitives.Disposables;
using Rentier.Desktop.Extensions;
using ReactiveUI;
using Rentier.Application.Common;
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

    /// <summary>Fixed for the process lifetime — set once from <see cref="IAppVersionService"/>.</summary>
    public string AppVersionText { get; }

    // ── Update state ──────────────────────────────────────────────────────────

    private UpdateState _currentUpdateState = UpdateState.Idle;
    private string? _availableVersion;
    private int _downloadProgress;
    private string? _updateErrorCode;

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

    /// <summary>
    /// Localized reason for the last update failure, chosen by error code. Never the service's
    /// <see cref="Error.Message"/>: that is English-only and may quote an exception (URLs, paths).
    /// </summary>
    public string? UpdateErrorMessage => _updateErrorCode switch
    {
        null => null,
        ErrorCodes.UPDATE_NOT_INSTALLED => _localizationService["Update_Error_NotInstalled"],
        ErrorCodes.UPDATE_NO_PENDING => _localizationService["Update_Error_NoPending"],
        ErrorCodes.UPDATE_DOWNLOAD_FAILED => _localizationService["Update_Error_DownloadFailed"],
        ErrorCodes.UPDATE_DOWNLOAD_CANCELLED => _localizationService["Update_Error_DownloadCancelled"],
        ErrorCodes.UPDATE_APPLY_FAILED => _localizationService["Update_Error_ApplyFailed"],
        _ => _localizationService["Update_Error_Unknown"],
    };

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

    public ReactiveCommand<RxVoid, RxVoid> CheckForUpdateCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> DismissUpdateCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> BeginUpdateCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> RestartNowCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> DismissRestartCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> RetryDownloadCommand { get; }

    private readonly IUpdateService _updateService;
    private readonly ILocalizationService _localizationService;
    private readonly ISequencer _outputScheduler;
    private readonly IServiceProvider _provider;

    public MainWindowViewModel(
        IServiceProvider provider,
        ILocalizationService localizationService,
        IUpdateService updateService,
        IAppVersionService appVersionService,
        INavigationRequestSource navigationRequests,
        ISequencer? outputScheduler = null)
    {
        _updateService = updateService;
        _localizationService = localizationService;
        _outputScheduler = outputScheduler ?? RxSchedulers.MainThreadScheduler;
        AppVersionText = appVersionService.DisplayVersion;

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
            RestartNow, canRestart, outputScheduler: _outputScheduler);

        DismissRestartCommand = ReactiveCommand.CreateFromTask(
            OnDismissRestartAsync,
            canDismissRestart, outputScheduler: _outputScheduler);

        RetryDownloadCommand = ReactiveCommand.CreateFromTask(
            RunDownloadUpdateAsync, canRetry, outputScheduler: _outputScheduler);

        _provider = provider;
        NavigationEntries = BuildNavigationEntries(provider, localizationService);

        _selectedEntry = NavigationEntries[0];
        _currentViewModel = NavigationEntries[0].ViewModel!;
        _lastContentEntry = NavigationEntries[0];
        NavigationEntries[0].IsActive = true;

        // Both live for the whole app session (singletons), so the handler is never detached.
        navigationRequests.NavigationRequested += OnNavigationRequested;

        this.WhenActivated(RegisterActivation);
    }

    private void OnNavigationRequested(NavigationRequest request)
    {
        switch (request.Target)
        {
            case NavigationTarget.Filings:
                // Runs synchronously inside the requesting page's command: never throw from here.
                var filingsEntry = NavigationEntries.FirstOrDefault(e => e.ViewModel is FilingsViewModel);
                if (filingsEntry?.ViewModel is not FilingsViewModel filingsVm)
                    return;
                filingsVm.ReportIdFilter = request.ReportId;
                SelectedEntry = filingsEntry;
                break;

            case NavigationTarget.ManualFiling:
                // A fresh form each time; it is a hidden sub-page, not a sidebar entry.
                var manualVm = ActivatorUtilities.CreateInstance<ManualFilingViewModel>(_provider);
                SelectedEntry = new NavigationEntry(string.Empty, manualVm, IsVisible: false);
                break;
        }
    }

    private async Task OnDismissUpdateAsync()
    {
        CurrentUpdateState = UpdateState.Dismissed;
        await Task.CompletedTask;
    }

    private async Task OnDismissRestartAsync()
    {
        // Nothing to schedule: Velopack applies a downloaded update on the next launch by default.
        // (WaitExitThenApplyUpdates is not an option — it kills the app if it is still running
        // 60 seconds later.)
        CurrentUpdateState = UpdateState.Idle;
        await Task.CompletedTask;
    }

    private void RestartNow()
    {
        // Returns only on failure; on success the process exits and relaunches.
        var result = _updateService.ApplyUpdateAndRestart();
        if (!result.IsSuccess)
            ShowUpdateError(result.Error);
    }

    private void ShowUpdateError(Error error)
    {
        _updateErrorCode = error.Code;
        this.RaisePropertyChanged(nameof(UpdateErrorMessage));
        this.RaisePropertyChanged(nameof(UpdateFailedText));
        CurrentUpdateState = UpdateState.Error;
    }

    private List<NavigationEntry> BuildNavigationEntries(
        IServiceProvider provider, ILocalizationService localizationService)
    {
        // Pages raise cross-page jumps through INavigationService; OnNavigationRequested performs them.
        var dashboardVm = ActivatorUtilities.CreateInstance<DashboardViewModel>(provider);
        var filingsVm = ActivatorUtilities.CreateInstance<FilingsViewModel>(provider);
        var reportsVm = ActivatorUtilities.CreateInstance<ReportsViewModel>(provider);
        var syncVm = ActivatorUtilities.CreateInstance<SyncViewModel>(provider);

        // ── Settings sub-ViewModels ───────────────────────────────────────────
        var profileVm = provider.GetRequiredService<ProfileSettingsViewModel>();
        var holidayVm = provider.GetRequiredService<HolidaySettingsViewModel>();
        var mailboxVm = provider.GetRequiredService<MailboxSettingsViewModel>();
        var importerVm = provider.GetRequiredService<ImporterSettingsViewModel>();
        var appearanceVm = provider.GetRequiredService<AppearanceSettingsViewModel>();
        var diagnosticsVm = provider.GetRequiredService<DiagnosticsSettingsViewModel>();

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

        var diagnosticsChild = new NavigationEntry(localizationService["Nav_Settings_Diagnostics"], diagnosticsVm, Icon: NavIcon("NavDiagnosticsIcon")) { IndentLevel = 1, ParentGroup = settingsGroup };

        settingsGroup.Children = new[] { profileChild, holidaysChild, mailboxesChild, importersChild, languageChild, diagnosticsChild };

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
            diagnosticsChild,
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

                Dispatcher.UIThread.Post(() => SelectedEntry = null);
            })
            .DisposeWith(disposables);

        _localizationService.CultureChanged
            .ObserveOn(_outputScheduler)
            .Subscribe(_ =>
            {
                UpdateNavigationLabels(_localizationService);
                this.RaisePropertyChanged(nameof(UpdateAvailableText));
                this.RaisePropertyChanged(nameof(DownloadingText));
                this.RaisePropertyChanged(nameof(UpdateErrorMessage));
                this.RaisePropertyChanged(nameof(UpdateFailedText));
            })
            .DisposeWith(disposables);

        // Auto-check for updates on activation. Activation runs on the UI thread and the check
        // awaits without ConfigureAwait(false), so every bound update-state change happens on the
        // UI thread; the service does its own I/O off it.
        // Execute() ignores CanExecute, so guard explicitly: a re-activation must not restart the
        // check from UpdateAvailable/Downloading/Downloaded/Dismissed and wipe that state.
        if (CurrentUpdateState == UpdateState.Idle)
            CheckForUpdateCommand.Execute().Subscribe().DisposeWith(disposables);
    }

    private async Task RunCheckForUpdateAsync()
    {
        CurrentUpdateState = UpdateState.Checking;
        var result = await _updateService.CheckForUpdatesAsync();
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
        var result = await _updateService.DownloadUpdateAsync(
            progress => RxSchedulers.MainThreadScheduler.Schedule(
                () => DownloadProgress = progress));

        if (result.IsSuccess)
            CurrentUpdateState = UpdateState.Downloaded;
        else
            ShowUpdateError(result.Error);
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
                DiagnosticsSettingsViewModel => "Nav_Settings_Diagnostics",
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

