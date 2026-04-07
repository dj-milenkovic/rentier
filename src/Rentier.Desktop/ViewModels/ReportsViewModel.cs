using System.Reactive;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;

namespace Rentier.Desktop.ViewModels;

public sealed class ReportsViewModel : ReactiveObject
{
    private readonly ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>> _handler;
    private bool _isSyncing;
    private string? _statusMessage;
    private int _progressValue;

    public bool IsSyncing
    {
        get => _isSyncing;
        private set => this.RaiseAndSetIfChanged(ref _isSyncing, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public int ProgressValue
    {
        get => _progressValue;
        private set => this.RaiseAndSetIfChanged(ref _progressValue, value);
    }

    public ReactiveCommand<Unit, Unit> SyncCommand { get; }

    public ReportsViewModel(ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>> handler)
    {
        _handler = handler;
        SyncCommand = ReactiveCommand.CreateFromTask(HandleSyncAsync);
        SyncCommand.IsExecuting.Subscribe(v => IsSyncing = v);
    }

    private async Task HandleSyncAsync(CancellationToken ct)
    {
        StatusMessage = null;
        ProgressValue = 0;

        var progress = new Progress<SyncProgress>(p =>
        {
            if (p.Total > 0)
                ProgressValue = (int)((double)p.Processed / p.Total * 100);
            if (p.CurrentFile != null)
                StatusMessage = p.CurrentFile;
        });

        var result = await _handler.HandleAsync(new SyncMailboxCommand(progress), ct);

        if (result.IsSuccess)
        {
            ProgressValue = 100;
            var r = result.Value;
            StatusMessage = r.Errors.Count > 0
                ? $"Sync complete: {r.ReportsCreated} reports created, {r.Errors.Count} error(s)"
                : $"Sync complete: {r.ReportsCreated} reports created";
        }
        else
        {
            StatusMessage = result.Error.Message;
        }
    }
}
