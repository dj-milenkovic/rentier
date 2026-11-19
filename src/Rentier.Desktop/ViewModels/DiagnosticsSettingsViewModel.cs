using ReactiveUI.Primitives;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Desktop.Resources;
using Rentier.Desktop.Services;

namespace Rentier.Desktop.ViewModels;

/// <summary>
/// Settings page for the diagnostic log: a Debug-detail toggle and a shortcut to the log folder.
/// The toggle applies immediately and is remembered across restarts as a user preference.
/// </summary>
public sealed class DiagnosticsSettingsViewModel : ReactiveObject
{
    private readonly IDiagnosticsService _diagnostics;
    private readonly IFolderLauncher _folderLauncher;
    private readonly ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>> _setPreferenceHandler;
    private bool _isDebugLoggingEnabled;
    private string _errorMessage = string.Empty;

    public DiagnosticsSettingsViewModel(
        IDiagnosticsService diagnostics,
        IFolderLauncher folderLauncher,
        ICommandHandler<SetUserPreferenceCommand, Result<VoidResult, Error>> setPreferenceHandler)
    {
        _diagnostics = diagnostics;
        _folderLauncher = folderLauncher;
        _setPreferenceHandler = setPreferenceHandler;
        _isDebugLoggingEnabled = diagnostics.IsDebugLoggingEnabled;

        OpenLogsFolderCommand = ReactiveCommand.CreateFromTask(ExecuteOpenLogsFolderAsync);
        SetDebugLoggingCommand = ReactiveCommand.CreateFromTask<bool>(ExecuteSetDebugLoggingAsync);

        // Auto-apply, like the Appearance page: no Save button. InvokeCommand keeps the
        // subscription owned by the property pipeline instead of an ad-hoc Subscribe().
        this.WhenAnyValue(x => x.IsDebugLoggingEnabled)
            .Skip(1)
            .DistinctUntilChanged()
            .InvokeCommand(SetDebugLoggingCommand);
    }

    public bool IsDebugLoggingEnabled
    {
        get => _isDebugLoggingEnabled;
        set => this.RaiseAndSetIfChanged(ref _isDebugLoggingEnabled, value);
    }

    /// <summary>Where the daily log files are written.</summary>
    public string LogDirectory => _diagnostics.LogDirectory;

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public ReactiveCommand<RxVoid, RxVoid> OpenLogsFolderCommand { get; }

    public ReactiveCommand<bool, RxVoid> SetDebugLoggingCommand { get; }

    private async Task ExecuteOpenLogsFolderAsync()
    {
        ErrorMessage = string.Empty;

        var opened = await _folderLauncher.OpenAsync(LogDirectory);
        if (!opened)
            ErrorMessage = $"{Strings.Diagnostics_OpenFolderFailed} {LogDirectory}";
    }

    private async Task ExecuteSetDebugLoggingAsync(bool enabled)
    {
        ErrorMessage = string.Empty;

        // Apply first: the change takes effect for this session even if it cannot be remembered.
        _diagnostics.SetDebugLogging(enabled);

        var result = await _setPreferenceHandler.HandleAsync(
            new SetUserPreferenceCommand(UserPreferenceKeys.DEBUG_LOGGING, enabled ? "true" : "false"));

        if (!result.IsSuccess)
            ErrorMessage = Strings.Diagnostics_SaveFailed;
    }
}
