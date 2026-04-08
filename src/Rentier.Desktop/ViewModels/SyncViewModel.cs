using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Domain.ValueObjects;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;

namespace Rentier.Desktop.ViewModels;

public sealed class SyncViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    private readonly ISyncAllCommandHandler _handler;
    private readonly Action _navigateToFilings;
    private readonly IScheduler _scheduler;
    private CancellationTokenSource? _cts;

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

    private ObservableCollection<SyncProgressEntryViewModel> _logEntries = new();
    public ObservableCollection<SyncProgressEntryViewModel> LogEntries
    {
        get => _logEntries;
        private set => this.RaiseAndSetIfChanged(ref _logEntries, value);
    }

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

    public ReactiveCommand<Unit, Unit> SyncCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public SyncViewModel(ISyncAllCommandHandler handler, Action navigateToFilings, IScheduler? scheduler = null)
    {
        _handler = handler;
        _navigateToFilings = navigateToFilings;
        _scheduler = scheduler ?? RxApp.MainThreadScheduler;

        SyncCommand = ReactiveCommand.CreateFromTask(RunSyncAsync, outputScheduler: _scheduler);

        CancelCommand = ReactiveCommand.Create(
            () => _cts?.Cancel(),
            this.WhenAnyValue(x => x.IsRunning),
            outputScheduler: _scheduler);

        this.WhenActivated(disposables =>
        {
            SyncCommand.IsExecuting
                .Subscribe(v => IsRunning = v)
                .DisposeWith(disposables);

            // Swallow unhandled exceptions — errors are surfaced via SummaryMessage
            SyncCommand.ThrownExceptions
                .Subscribe(_ => { })
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

            var result = await _handler.HandleAsync(new SyncAllCommand(SyncParameters.Default), progress, _cts.Token);

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
