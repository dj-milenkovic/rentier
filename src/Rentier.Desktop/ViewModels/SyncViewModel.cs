using System.Collections.ObjectModel;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using Rentier.Desktop.Extensions;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;

namespace Rentier.Desktop.ViewModels;

public sealed class SyncViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    private readonly ISyncAllCommandHandler _handler;
    private readonly ISequencer _scheduler;
    private readonly TimeProvider _timeProvider;
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

    // ── Sync mode selection ─────────────────────────────────────────────────
    public SyncMode[] AvailableSyncModes { get; } = Enum.GetValues<SyncMode>();

    private SyncMode _selectedSyncMode = SyncMode.Incremental;
    public SyncMode SelectedSyncMode
    {
        get => _selectedSyncMode;
        set => this.RaiseAndSetIfChanged(ref _selectedSyncMode, value);
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

    /// <summary>
    /// Upper bound for the DatePicker — today's <b>local</b> calendar date (the picker shows a
    /// local calendar), carried as a zero-offset value so its date part is never shifted.
    /// Evaluated on every read so it never goes stale if the app stays open past midnight.
    /// </summary>
    public DateTimeOffset TodayOffset =>
        new(_timeProvider.GetLocalToday().ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    // ── Derived observables ──────────────────────────────────────────────────
    private readonly ObservableAsPropertyHelper<bool> _isReplayFromDateMode;
    public bool IsReplayFromDateMode => _isReplayFromDateMode.Value;

    private readonly ObservableAsPropertyHelper<bool> _isReplayMode;
    public bool IsReplayMode => _isReplayMode.Value;

    private readonly ObservableAsPropertyHelper<bool> _isFullReplayMode;
    public bool IsFullReplayMode => _isFullReplayMode.Value;

    private readonly ObservableAsPropertyHelper<string> _impactSummary;
    public string ImpactSummary => _impactSummary.Value;

    /// <summary>
    /// Describes what a sync in <paramref name="mode"/> will fetch. Already-imported reports are
    /// always skipped, so the text must not promise anything else. <paramref name="text"/> resolves
    /// a string key, so the view can render the same summary in the user's language.
    /// </summary>
    public static string BuildImpactSummary(SyncMode mode, DateTimeOffset? replayFrom, Func<string, string> text)
    {
        var dateStr = replayFrom.HasValue
            ? DateOnly.FromDateTime(replayFrom.Value.DateTime).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : "...";
        return mode switch
        {
            SyncMode.Incremental => text(nameof(Resources.Strings.Sync_Impact_Incremental)),
            SyncMode.ReplayFromDate => string.Format(text(nameof(Resources.Strings.Sync_Impact_ReplayFromDate)), dateStr),
            SyncMode.FullReplay => text(nameof(Resources.Strings.Sync_Impact_FullReplay)),
            _ => string.Empty
        };
    }

    private static string EnglishText(string key) =>
        typeof(Resources.Strings).GetProperty(key)?.GetValue(null) as string ?? key;

    private readonly ObservableAsPropertyHelper<string?> _validationError;
    public string? ValidationError => _validationError.Value;

    // ── Commands ─────────────────────────────────────────────────────────────
    public ReactiveCommand<RxVoid, RxVoid> SyncCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> CancelCommand { get; }

    public SyncViewModel(
        ISyncAllCommandHandler handler, TimeProvider timeProvider, ISequencer? scheduler = null)
    {
        _handler = handler;
        _timeProvider = timeProvider;
        var effectiveScheduler = scheduler ?? RxSchedulers.MainThreadScheduler;
        _scheduler = effectiveScheduler;

        var modeStream = this.WhenAnyValue(x => x.SelectedSyncMode);
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
                if (date > _timeProvider.GetLocalToday())
                    return Resources.Strings.Sync_Validation_DateNotFuture;
            }
            return (string?)null;
        }).ToProperty(this, x => x.ValidationError);

        // Already-imported reports are always skipped: the summary must not promise anything else.
        _impactSummary = modeStream.CombineLatest(dateStream, (m, d) => BuildImpactSummary(m, d, EnglishText))
            .ToProperty(this, x => x.ImpactSummary, initialValue: string.Empty);

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

        this.WhenActivated((MultipleDisposable disposables) =>
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
            // Use scheduler-dispatched progress so ImmediateScheduler in tests runs callbacks
            // synchronously, and MainThreadScheduler in production stays on the UI thread.
            // System.Threading.Progress<T> posts via SynchronizationContext.Post() which is
            // async and causes a race condition on macOS where the callback may arrive after
            // the await returns.
            var progress = new SchedulerProgress<SyncProgressEntry>(
                entry => LogEntries.Add(new SyncProgressEntryViewModel(entry)),
                _scheduler);

            var parameters = SelectedSyncMode == SyncMode.ReplayFromDate
                ? SyncParameters.ReplayFrom(
                    ReplayFromDate ?? throw new DomainException("ReplayFromDate is required for ReplayFromDate mode"),
                    _timeProvider.GetLocalToday())
                : new SyncParameters(SelectedSyncMode);

            var result = await _handler.HandleAsync(new SyncAllCommand(parameters), progress, _cts.Token);

            if (result.IsSuccess)
            {
                HasErrors = result.Value.Errors.Count > 0;
                SummaryMessage = $"Sync complete: {result.Value.FilingsCreated} filing(s) created, {result.Value.Errors.Count} error(s).";
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
                new SyncProgressEntry(_timeProvider.GetLocalNow(), "Sync cancelled by user.", SyncProgressSeverity.Warning)));
            SummaryMessage = "Sync cancelled by user.";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }
}

/// <summary>
/// An <see cref="IProgress{T}"/> implementation that dispatches each report through
/// an <see cref="IScheduler"/> rather than via <see cref="System.Threading.SynchronizationContext"/>.
/// This keeps progress callbacks on the UI thread in production (MainThreadScheduler)
/// while making them synchronous in tests (ImmediateScheduler), eliminating the
/// async-post race that <see cref="Progress{T}"/> exhibits on macOS.
/// </summary>
file sealed class SchedulerProgress<T>(Action<T> handler, ISequencer scheduler) : IProgress<T>
{
    public void Report(T value) => scheduler.Schedule(() => handler(value));
}
