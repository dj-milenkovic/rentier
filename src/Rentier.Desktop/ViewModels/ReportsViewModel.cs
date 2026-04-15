using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Domain.ValueObjects;
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
    private readonly ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>> _bulkDeleteReports;
    private readonly Func<string, string, Task<bool>> _confirmDelete;
    private readonly Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>> _showImportDialog;
    private readonly Action<Guid> _navigateToFilings;
    private readonly IScheduler _scheduler;

    private bool _isLoading;
    private bool _isSyncing;
    private string? _errorMessage;
    private string? _syncStatusMessage;
    private int _syncProgressValue;
    private int _selectedCount;
    private readonly ObservableAsPropertyHelper<bool> _hasSelection;
    private readonly ObservableAsPropertyHelper<string> _deleteSelectedLabel;

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

    public int SelectedCount
    {
        get => _selectedCount;
        private set => this.RaiseAndSetIfChanged(ref _selectedCount, value);
    }

    public bool HasSelection => _hasSelection.Value;
    public string DeleteSelectedLabel => _deleteSelectedLabel.Value;

    public bool IsEmpty => Rows.Count == 0 && !IsLoading;
    public bool HasItems => Rows.Count > 0;

    public ObservableCollection<ReportRowViewModel> Rows { get; } = new();

    public ReactiveCommand<Unit, Unit> LoadReportsCommand { get; }
    public ReactiveCommand<Unit, Unit> SyncCommand { get; }
    public ReactiveCommand<Unit, Unit> ImportCommand { get; }
    public ReactiveCommand<Guid, Unit> DeleteCommand { get; }
    public ReactiveCommand<Guid, Unit> ViewFilingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearErrorCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> BulkDeleteCommand { get; }

    private readonly CompositeDisposable _rowSubscriptions = new();

    public ReportsViewModel(
        ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>> syncHandler,
        IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>> getReports,
        ICommandHandler<ImportReportCommand, Result<Guid, Error>> importReport,
        ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>> deleteReport,
        ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>> bulkDeleteReports,
        Func<string, string, Task<bool>> confirmDelete,
        Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>> showImportDialog,
        Action<Guid> navigateToFilings,
        IScheduler? scheduler = null)
    {
        _syncHandler       = syncHandler;
        _getReports        = getReports;
        _importReport      = importReport;
        _deleteReport      = deleteReport;
        _bulkDeleteReports = bulkDeleteReports;
        _confirmDelete     = confirmDelete;
        _showImportDialog  = showImportDialog;
        _navigateToFilings = navigateToFilings;
        _scheduler         = scheduler ?? RxApp.MainThreadScheduler;

        _hasSelection = this.WhenAnyValue(x => x.SelectedCount)
            .Select(c => c > 0)
            .ToProperty(this, x => x.HasSelection, scheduler: _scheduler);

        _deleteSelectedLabel = this.WhenAnyValue(x => x.SelectedCount)
            .Select(c => string.Format(Strings.BulkDelete_Button_Template, c))
            .ToProperty(this, x => x.DeleteSelectedLabel,
                initialValue: string.Format(Strings.BulkDelete_Button_Template, 0),
                scheduler: _scheduler);

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

        var hasItemsObservable = this.WhenAnyValue(x => x.HasItems);
        var hasSelectionObservable = this.WhenAnyValue(x => x.HasSelection);

        SelectAllCommand = ReactiveCommand.Create(
            () =>
            {
                foreach (var row in Rows)
                    row.IsSelected = true;
            },
            hasItemsObservable,
            outputScheduler: _scheduler);

        ClearSelectionCommand = ReactiveCommand.Create(
            () =>
            {
                foreach (var row in Rows)
                    row.IsSelected = false;
            },
            hasSelectionObservable,
            outputScheduler: _scheduler);

        BulkDeleteCommand = ReactiveCommand.CreateFromTask(
            async (CancellationToken ct) =>
            {
                var selectedIds = Rows
                    .Where(r => r.IsSelected)
                    .Select(r => r.Id)
                    .ToList();

                if (selectedIds.Count == 0) return;

                var message = string.Format(
                    Strings.BulkDelete_Reports_Confirmation_Message, selectedIds.Count);
                var confirmed = await _confirmDelete(
                    Strings.BulkDelete_Reports_Confirmation_Title, message);
                if (!confirmed) return;

                var result = await _bulkDeleteReports.HandleAsync(
                    new BulkDeleteReportsCommand(selectedIds), ct);

                if (!result.IsSuccess)
                {
                    ErrorMessage = Strings.BulkDelete_Error_Failed;
                    return;
                }

                await LoadReportsAsync(ct);
            },
            outputScheduler: _scheduler);

        this.WhenActivated(disposables =>
        {
            LoadReportsCommand.Execute().Subscribe().DisposeWith(disposables);
            LoadReportsCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            SyncCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            ImportCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            DeleteCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            BulkDeleteCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
        });
    }

    private void RebuildRowSubscriptions()
    {
        _rowSubscriptions.Clear();
        foreach (var row in Rows)
        {
            row.WhenAnyValue(r => r.IsSelected)
                .Subscribe(_ => SelectedCount = Rows.Count(r => r.IsSelected))
                .DisposeWith(_rowSubscriptions);
        }
        SelectedCount = Rows.Count(r => r.IsSelected);
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
            this.RaisePropertyChanged(nameof(HasItems));

            RebuildRowSubscriptions();
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
            var errorToPreserve = result.IsSuccess ? null : result.Error.Message;
            await LoadReportsAsync(ct);
            if (errorToPreserve is not null)
                ErrorMessage = errorToPreserve;
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
            var errorToPreserve = result.IsSuccess ? null : result.Error.Message;
            await LoadReportsAsync(ct);
            if (errorToPreserve is not null)
                ErrorMessage = errorToPreserve;
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

        var result = await _syncHandler.HandleAsync(new SyncMailboxCommand(SyncParameters.Default, progress), ct);

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
