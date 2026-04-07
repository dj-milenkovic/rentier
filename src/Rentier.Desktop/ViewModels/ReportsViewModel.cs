using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Resources;

namespace Rentier.Desktop.ViewModels;

public sealed class ReportsViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    private readonly ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>> _syncHandler;
    private readonly IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>> _getReports;
    private readonly ICommandHandler<ImportReportCommand, Result<Guid, Error>> _importReport;
    private readonly ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>> _deleteReport;
    private readonly Func<string, string, Task<bool>> _confirmDelete;
    private readonly Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>> _showImportDialog;
    private readonly Action<Guid> _navigateToFilings;
    private readonly IScheduler _scheduler;

    private bool _isLoading;
    private bool _isSyncing;
    private string? _errorMessage;
    private string? _syncStatusMessage;
    private int _syncProgressValue;

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public bool IsSyncing
    {
        get => _isSyncing;
        private set => this.RaiseAndSetIfChanged(ref _isSyncing, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public string? SyncStatusMessage
    {
        get => _syncStatusMessage;
        private set => this.RaiseAndSetIfChanged(ref _syncStatusMessage, value);
    }

    public int SyncProgressValue
    {
        get => _syncProgressValue;
        private set => this.RaiseAndSetIfChanged(ref _syncProgressValue, value);
    }

    public bool IsEmpty => Rows.Count == 0 && !IsLoading;

    public ObservableCollection<ReportRowViewModel> Rows { get; } = new();

    public ReactiveCommand<Unit, Unit> LoadReportsCommand { get; }
    public ReactiveCommand<Unit, Unit> SyncCommand { get; }
    public ReactiveCommand<Unit, Unit> ImportCommand { get; }
    public ReactiveCommand<Guid, Unit> DeleteCommand { get; }
    public ReactiveCommand<Guid, Unit> ViewFilingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearErrorCommand { get; }

    public ReportsViewModel(
        ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>> syncHandler,
        IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>> getReports,
        ICommandHandler<ImportReportCommand, Result<Guid, Error>> importReport,
        ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>> deleteReport,
        Func<string, string, Task<bool>> confirmDelete,
        Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>> showImportDialog,
        Action<Guid> navigateToFilings,
        IScheduler? scheduler = null)
    {
        _syncHandler       = syncHandler;
        _getReports        = getReports;
        _importReport      = importReport;
        _deleteReport      = deleteReport;
        _confirmDelete     = confirmDelete;
        _showImportDialog  = showImportDialog;
        _navigateToFilings = navigateToFilings;
        _scheduler         = scheduler ?? RxApp.MainThreadScheduler;

        LoadReportsCommand = ReactiveCommand.CreateFromTask(
            LoadReportsAsync, outputScheduler: _scheduler);

        SyncCommand = ReactiveCommand.CreateFromTask(
            HandleSyncAsync, outputScheduler: _scheduler);

        ImportCommand = ReactiveCommand.CreateFromTask(
            ImportAsync, outputScheduler: _scheduler);

        DeleteCommand = ReactiveCommand.CreateFromTask<Guid>(
            DeleteAsync, outputScheduler: _scheduler);

        ViewFilingsCommand = ReactiveCommand.CreateFromTask<Guid>(
            async (id, _) => _navigateToFilings(id), outputScheduler: _scheduler);

        ClearErrorCommand = ReactiveCommand.Create(
            () => { ErrorMessage = null; }, outputScheduler: _scheduler);

        SyncCommand.IsExecuting.Subscribe(v => IsSyncing = v);

        this.WhenActivated(disposables =>
        {
            LoadReportsCommand.Execute().Subscribe().DisposeWith(disposables);
        });
    }

    private async Task LoadReportsAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await _getReports.HandleAsync(new GetReportsQuery(), ct);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error.Message;
                return;
            }

            Rows.Clear();
            foreach (var dto in result.Value)
                Rows.Add(ReportRowViewModel.From(dto));

            this.RaisePropertyChanged(nameof(IsEmpty));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ImportAsync(CancellationToken ct = default)
    {
        var dialogResult = await _showImportDialog();
        if (dialogResult is null)
            return;

        var (importerId, fileName, content) = dialogResult.Value;
        IsLoading = true;
        try
        {
            var result = await _importReport.HandleAsync(
                new ImportReportCommand(importerId, fileName, content), ct);
            if (!result.IsSuccess)
                ErrorMessage = result.Error.Message;

            await LoadReportsAsync(ct);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteAsync(Guid reportId, CancellationToken ct = default)
    {
        var confirmed = await _confirmDelete(
            Strings.Reports_Delete_Confirmation_Title,
            Strings.Reports_Delete_Confirmation_Message);
        if (!confirmed)
            return;

        IsLoading = true;
        try
        {
            var result = await _deleteReport.HandleAsync(new DeleteReportCommand(reportId), ct);
            if (!result.IsSuccess)
                ErrorMessage = result.Error.Message;

            await LoadReportsAsync(ct);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Existing IMAP sync logic — preserved verbatim
    private async Task HandleSyncAsync(CancellationToken ct)
    {
        SyncStatusMessage = null;
        SyncProgressValue = 0;

        var progress = new Progress<SyncProgress>(p =>
        {
            if (p.Total > 0)
                SyncProgressValue = (int)((double)p.Processed / p.Total * 100);
            if (p.CurrentFile != null)
                SyncStatusMessage = p.CurrentFile;
        });

        var result = await _syncHandler.HandleAsync(new SyncMailboxCommand(progress), ct);

        if (result.IsSuccess)
        {
            SyncProgressValue = 100;
            var r = result.Value;
            SyncStatusMessage = r.Errors.Count > 0
                ? $"Sync complete: {r.ReportsCreated} reports created, {r.Errors.Count} error(s)"
                : $"Sync complete: {r.ReportsCreated} reports created";
        }
        else
        {
            SyncStatusMessage = result.Error.Message;
        }
    }
}

