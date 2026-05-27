using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;

namespace Rentier.Desktop.ViewModels;

public sealed class SyncViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    private readonly ISyncAllCommandHandler _handler;
    private readonly Action _navigateToFilings;
    private CancellationTokenSource? _cts;

    // ── Running state ────────────────────────────────────────────────────────
    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set => this.RaiseAndSetIfChanged(ref _isRunning, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    private readonly ObservableCollection<SyncProgressEntryViewModel> _logEntries = new();
    public ObservableCollection<SyncProgressEntryViewModel> LogEntries => _logEntries;

    private string _summaryMessage = string.Empty;
    public string SummaryMessage
    {
        get => _summaryMessage;
        private set => this.RaiseAndSetIfChanged(ref _summaryMessage, value);
    }

    private bool _hasErrors;
    public bool HasErrors
    {
        get => _hasErrors;
        private set => this.RaiseAndSetIfChanged(ref _hasErrors, value);
    }

    /// <summary>
    /// Returns the specific ErrorMessage when sync failed completely, or a generic partial-success
    /// message when individual item errors occurred but no overall failure message was set.
    /// </summary>
    private readonly ObservableAsPropertyHelper<string?> _errorSummaryMessage;
    public string? ErrorSummaryMessage => _errorSummaryMessage.Value;

    // ── Sync mode / strategy selection ──────────────────────────────────────
    public SyncMode[] AvailableSyncModes { get; } = Enum.GetValues<SyncMode>();
    public DuplicateStrategy[] AvailableDuplicateStrategies { get; } = Enum.GetValues<DuplicateStrategy>();

    private SyncMode _selectedSyncMode = SyncMode.Incremental;
    public SyncMode SelectedSyncMode
    {
        get => _selectedSyncMode;
        set => this.RaiseAndSetIfChanged(ref _selectedSyncMode, value);
    }

    private DuplicateStrategy _selectedDuplicateStrategy = DuplicateStrategy.SkipExisting;
    public DuplicateStrategy SelectedDuplicateStrategy
    {
        get => _selectedDuplicateStrategy;
        set => this.RaiseAndSetIfChanged(ref _selectedDuplicateStrategy, value);
    }

    private DateTimeOffset? _replayFromDateOffset;
    public DateTimeOffset? ReplayFromDateOffset
    {
        get => _replayFromDateOffset;
        set => this.RaiseAndSetIfChanged(ref _replayFromDateOffset, value);
    }

    public DateOnly? ReplayFromDate =>
        ReplayFromDateOffset.HasValue
            ? DateOnly.FromDateTime(ReplayFromDateOffset.Value.DateTime)
            : null;

    /// <summary>Upper bound for the DatePicker — today in local time as UTC-offset.</summary>
    public DateTimeOffset TodayOffset { get; } = new DateTimeOffset(
        DateTime.UtcNow.Date, TimeSpan.Zero);

    // ── Derived observables ──────────────────────────────────────────────────
    private readonly ObservableAsPropertyHelper<bool> _isReplayFromDateMode;
    public bool IsReplayFromDateMode => _isReplayFromDateMode.Value;

    private readonly ObservableAsPropertyHelper<bool> _isReplayMode;
    public bool IsReplayMode => _isReplayMode.Value;

    private readonly ObservableAsPropertyHelper<bool> _isFullReplayMode;
    public bool IsFullReplayMode => _isFullReplayMode.Value;

    private readonly ObservableAsPropertyHelper<string> _impactSummary;
    public string ImpactSummary => _impactSummary.Value;

    private readonly ObservableAsPropertyHelper<string?> _validationError;
    public string? ValidationError => _validationError.Value;

    // ── Commands ─────────────────────────────────────────────────────────────
    public ReactiveCommand<Unit, Unit> SyncCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public SyncViewModel(ISyncAllCommandHandler handler, Action navigateToFilings, IScheduler? scheduler = null)
    {
        _handler = handler;
        _navigateToFilings = navigateToFilings;
        var effectiveScheduler = scheduler ?? RxApp.MainThreadScheduler;

        var modeStream = this.WhenAnyValue(x => x.SelectedSyncMode);
        var strategyStream = this.WhenAnyValue(x => x.SelectedDuplicateStrategy);
        var dateStream = this.WhenAnyValue(x => x.ReplayFromDateOffset);

        _isReplayFromDateMode = modeStream
            .Select(m => m == SyncMode.ReplayFromDate)
            .ToProperty(this, x => x.IsReplayFromDateMode);

        _isReplayMode = modeStream
            .Select(m => m is SyncMode.ReplayFromDate or SyncMode.FullReplay)
            .ToProperty(this, x => x.IsReplayMode);

        _isFullReplayMode = modeStream
            .Select(m => m == SyncMode.FullReplay)
            .ToProperty(this, x => x.IsFullReplayMode);

        _validationError = modeStream.CombineLatest(dateStream, (m, d) =>
        {
            if (m == SyncMode.ReplayFromDate)
            {
                if (d == null) return Resources.Strings.Sync_Validation_DateRequired;
                var date = DateOnly.FromDateTime(d.Value.DateTime);
                if (date > DateOnly.FromDateTime(DateTime.UtcNow))
                    return Resources.Strings.Sync_Validation_DateNotFuture;
            }
            return (string?)null;
        }).ToProperty(this, x => x.ValidationError);

        _impactSummary = modeStream.CombineLatest(strategyStream, dateStream, (m, s, d) =>
        {
            var dateStr = d.HasValue ? DateOnly.FromDateTime(d.Value.DateTime).ToString("yyyy-MM-dd") : "...";
            return (m, s) switch
            {
                (SyncMode.Incremental, DuplicateStrategy.SkipExisting) =>
                    "Fetches new emails since last sync. Duplicates are skipped.",
                (SyncMode.Incremental, DuplicateStrategy.CreateNewRevision) =>
                    "Fetches new emails since last sync. Duplicates create new revisions.",
                (SyncMode.Incremental, DuplicateStrategy.ReprocessInPlace) =>
                    "Fetches new emails since last sync. Existing reports are reprocessed.",
                (SyncMode.ReplayFromDate, DuplicateStrategy.SkipExisting) =>
                    $"Re-fetches emails from {dateStr}. Duplicates are skipped.",
                (SyncMode.ReplayFromDate, DuplicateStrategy.CreateNewRevision) =>
                    $"Re-fetches emails from {dateStr}. Duplicates create new revisions.",
                (SyncMode.ReplayFromDate, DuplicateStrategy.ReprocessInPlace) =>
                    $"Re-fetches emails from {dateStr}. Existing reports are reprocessed.",
                (SyncMode.FullReplay, DuplicateStrategy.SkipExisting) =>
                    "Fetches ALL emails in the mailbox. Duplicates are skipped.",
                (SyncMode.FullReplay, DuplicateStrategy.CreateNewRevision) =>
                    "Fetches ALL emails in the mailbox. Duplicates create new revisions.",
                (SyncMode.FullReplay, DuplicateStrategy.ReprocessInPlace) =>
                    "Fetches ALL emails. Existing reports are reprocessed (safe fallback to revision if filed).",
                _ => string.Empty
            };
        }).ToProperty(this, x => x.ImpactSummary, initialValue: string.Empty);

        _errorSummaryMessage = this
            .WhenAnyValue(x => x.ErrorMessage, x => x.HasErrors,
                (msg, hasErrors) => msg ?? (hasErrors ? Resources.Strings.Sync_PartialSuccess_Message : null))
            .ToProperty(this, x => x.ErrorSummaryMessage);

        var canSync = this.WhenAnyValue(x => x.ValidationError)
            .Select(e => e == null);

        SyncCommand = ReactiveCommand.CreateFromTask(RunSyncAsync, canSync, outputScheduler: effectiveScheduler);

        CancelCommand = ReactiveCommand.Create(
            () => _cts?.Cancel(),
            this.WhenAnyValue(x => x.IsRunning),
            outputScheduler: effectiveScheduler);

        this.WhenActivated(disposables =>
        {
            SyncCommand.IsExecuting
                .Subscribe(v => IsRunning = v)
                .DisposeWith(disposables);

            // Surface unhandled exceptions from SyncCommand via ErrorMessage
            SyncCommand.ThrownExceptions
                .Subscribe(ex =>
                {
                    ErrorMessage = ex.Message;
                    HasErrors = true;
                    SummaryMessage = $"Sync failed unexpectedly: {ex.Message}";
                })
                .DisposeWith(disposables);
        });
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        LogEntries.Clear();
        ErrorMessage = null;
        SummaryMessage = string.Empty;
        HasErrors = false;

        try
        {
            var progress = new Progress<SyncProgressEntry>(entry =>
                LogEntries.Add(new SyncProgressEntryViewModel(entry)));

            var parameters = new SyncParameters(
                SelectedSyncMode,
                SelectedDuplicateStrategy,
                SelectedSyncMode == SyncMode.ReplayFromDate ? ReplayFromDate : null);

            var result = await _handler.HandleAsync(new SyncAllCommand(parameters), progress, _cts.Token);

            if (result.IsSuccess)
            {
                HasErrors = result.Value.Errors.Count > 0;
                SummaryMessage = $"Sync complete: {result.Value.FilingsCreated} filing(s) created, {result.Value.Errors.Count} error(s).";
                if (!HasErrors) _navigateToFilings();
            }
            else
            {
                ErrorMessage = result.Error.Message;
                HasErrors = true;
                SummaryMessage = $"Sync failed: {result.Error.Message}";
            }
        }
        catch (OperationCanceledException)
        {
            LogEntries.Add(new SyncProgressEntryViewModel(
                new SyncProgressEntry(DateTimeOffset.Now, "Sync cancelled by user.", SyncProgressSeverity.Warning)));
            SummaryMessage = "Sync cancelled by user.";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }
}
