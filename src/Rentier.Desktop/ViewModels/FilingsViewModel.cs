using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Enums;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Resources;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.ViewModels;

public sealed class FilingsViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    private readonly IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>> _getFilings;
    private readonly ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>> _updateStatus;
    private readonly ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>> _updateReference;
    private readonly ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>> _deleteFiling;
    private readonly ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>> _exportFiling;
    private readonly ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>> _bulkDeleteFilings;
    private readonly Func<string, Task<bool>> _confirmDelete;
    private readonly Func<ExportFilingResult, Task> _saveFile;
    private readonly Action _navigateToManualFiling;
    private readonly IScheduler _scheduler;

    private bool _isLoading;
    private string? _errorMessage;
    private bool _showAll = true;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private int _totalCount;
    private Guid? _reportIdFilter;
    private int _selectedCount;
    private FilingSortColumn? _sortColumn = FilingSortColumn.FilingDeadline;
    private bool _sortDescending = true;
    private bool _isUpdatingSelection;
    private readonly ObservableAsPropertyHelper<bool> _hasReportFilter;
    private readonly ObservableAsPropertyHelper<bool> _hasSelection;
    private readonly ObservableAsPropertyHelper<string> _deleteSelectedLabel;

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool ShowAll
    {
        get => _showAll;
        set
        {
            this.RaiseAndSetIfChanged(ref _showAll, value);
            _currentPage = 1;
            this.RaisePropertyChanged(nameof(CurrentPage));
        }
    }

    public Guid? ReportIdFilter
    {
        get => _reportIdFilter;
        set
        {
            this.RaiseAndSetIfChanged(ref _reportIdFilter, value);
            _currentPage = 1;
            this.RaisePropertyChanged(nameof(CurrentPage));
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public int TotalPages
    {
        get => _totalPages;
        private set => this.RaiseAndSetIfChanged(ref _totalPages, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => this.RaiseAndSetIfChanged(ref _totalCount, value);
    }

    public int SelectedCount
    {
        get => _selectedCount;
        private set => this.RaiseAndSetIfChanged(ref _selectedCount, value);
    }

    public FilingSortColumn? SortColumn
    {
        get => _sortColumn;
        private set => this.RaiseAndSetIfChanged(ref _sortColumn, value);
    }

    public bool SortDescending
    {
        get => _sortDescending;
        private set => this.RaiseAndSetIfChanged(ref _sortDescending, value);
    }

    public bool HasReportFilter => _hasReportFilter.Value;

    public bool HasSelection => _hasSelection.Value;
    public string DeleteSelectedLabel => _deleteSelectedLabel.Value;

    /// <summary>Shows the current sort column and direction in the filter toolbar.</summary>
    // REMOVED: SortIndicatorDisplay — replaced by per-column PathIcon arrows (feature 046, FR-009).

    public bool? IsAllSelected
    {
        get
        {
            if (Rows.Count == 0 || _selectedCount == 0) return false;
            if (_selectedCount == Rows.Count) return true;
            return null; // indeterminate
        }
        set
        {
            if (_isUpdatingSelection) return;
            _isUpdatingSelection = true;
            try
            {
                if (value == true)
                    SelectAllCommand.Execute().Subscribe();
                else if (value == false)
                    ClearSelectionCommand.Execute().Subscribe();
                // null → ignore; reactive pipeline recomputes
            }
            finally
            {
                _isUpdatingSelection = false;
                this.RaisePropertyChanged(nameof(IsAllSelected));
            }
        }
    }

    public bool HasItems => Rows.Count > 0;
    public bool IsEmpty => Rows.Count == 0 && !IsLoading;
    public bool HasPreviousPage => _currentPage > 1 && !IsLoading;
    public bool HasNextPage => _currentPage < _totalPages && !IsLoading;
    public string PageIndicator => string.Format(Strings.Filings_Page_Indicator, _currentPage, _totalPages);

    public ObservableCollection<FilingRowViewModel> Rows { get; } = new();

    public ReactiveCommand<Unit, Unit> ClearReportFilterCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadPageCommand { get; }
    public ReactiveCommand<Unit, Unit> PreviousPageCommand { get; }
    public ReactiveCommand<Unit, Unit> NextPageCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearErrorCommand { get; }
    public ReactiveCommand<Unit, Unit> NewFilingCommand { get; }
    public ReactiveCommand<(Guid Id, FilingStatus NewStatus), Unit> AdvanceStatusCommand { get; }
    public ReactiveCommand<(Guid Id, string? Reference), Unit> SavePaymentRefCommand { get; }
    public ReactiveCommand<Guid, Unit> DeleteCommand { get; }
    public ReactiveCommand<Guid, Unit> ExportCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> BulkDeleteCommand { get; }
    public ReactiveCommand<(string ColumnTag, bool? CurrentDirection), Unit> ApplySortCommand { get; }

    private readonly CompositeDisposable _rowSubscriptions = new();

    public FilingsViewModel(
        IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>> getFilings,
        ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>> updateStatus,
        ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>> updateReference,
        ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>> deleteFiling,
        ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>> exportFiling,
        ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>> bulkDeleteFilings,
        Func<string, Task<bool>> confirmDelete,
        Func<ExportFilingResult, Task> saveFile,
        Action navigateToManualFiling,
        IScheduler? scheduler = null)
    {
        _getFilings = getFilings;
        _updateStatus = updateStatus;
        _updateReference = updateReference;
        _deleteFiling = deleteFiling;
        _exportFiling = exportFiling;
        _bulkDeleteFilings = bulkDeleteFilings;
        _confirmDelete = confirmDelete;
        _saveFile = saveFile;
        _navigateToManualFiling = navigateToManualFiling;
        _scheduler = scheduler ?? RxApp.MainThreadScheduler;

        _hasReportFilter = this.WhenAnyValue(x => x.ReportIdFilter)
            .Select(id => id.HasValue)
            .ToProperty(this, x => x.HasReportFilter, scheduler: _scheduler);

        _hasSelection = this.WhenAnyValue(x => x.SelectedCount)
            .Select(c => c > 0)
            .ToProperty(this, x => x.HasSelection, scheduler: _scheduler);

        _deleteSelectedLabel = this.WhenAnyValue(x => x.SelectedCount)
            .Select(c => string.Format(Strings.BulkDelete_Button_Template, c))
            .ToProperty(this, x => x.DeleteSelectedLabel,
                initialValue: string.Format(Strings.BulkDelete_Button_Template, 0),
                scheduler: _scheduler);

        LoadPageCommand = ReactiveCommand.CreateFromTask(
            LoadPageAsync, outputScheduler: _scheduler);

        ClearReportFilterCommand = ReactiveCommand.Create(
            () => { ReportIdFilter = null; },
            this.WhenAnyValue(x => x.HasReportFilter),
            outputScheduler: _scheduler);

        PreviousPageCommand= ReactiveCommand.CreateFromTask(
            async (CancellationToken ct) =>
            {
                _currentPage--;
                await LoadPageAsync(ct);
            },
            this.WhenAnyValue(x => x.HasPreviousPage),
            outputScheduler: _scheduler);

        NextPageCommand = ReactiveCommand.CreateFromTask(
            async (CancellationToken ct) =>
            {
                _currentPage++;
                await LoadPageAsync(ct);
            },
            this.WhenAnyValue(x => x.HasNextPage),
            outputScheduler: _scheduler);

        ClearErrorCommand = ReactiveCommand.Create(
            () => { ErrorMessage = null; },
            outputScheduler: _scheduler);

        NewFilingCommand = ReactiveCommand.Create(
            () => _navigateToManualFiling(),
            outputScheduler: _scheduler);

        AdvanceStatusCommand = ReactiveCommand.CreateFromTask<(Guid Id, FilingStatus NewStatus)>(
            async (args, ct) =>
            {
                var result = await _updateStatus.HandleAsync(
                    new UpdateFilingStatusCommand(args.Id, args.NewStatus), ct);
                var errorToPreserve = result.IsSuccess ? null : result.Error.Message;
                await LoadPageAsync(ct);
                if (errorToPreserve is not null)
                    ErrorMessage = errorToPreserve;
            },
            outputScheduler: _scheduler);

        SavePaymentRefCommand = ReactiveCommand.CreateFromTask<(Guid Id, string? Reference)>(
            async (args, ct) =>
            {
                var result = await _updateReference.HandleAsync(
                    new UpdatePaymentReferenceCommand(args.Id, args.Reference), ct);
                var errorToPreserve = result.IsSuccess ? null : result.Error.Message;
                await LoadPageAsync(ct);
                if (errorToPreserve is not null)
                    ErrorMessage = errorToPreserve;
            },
            outputScheduler: _scheduler);

        DeleteCommand = ReactiveCommand.CreateFromTask<Guid>(
            async (id, ct) =>
            {
                var confirmed = await _confirmDelete(Strings.Filings_Delete_Confirmation_Message);
                if (!confirmed) return;

                var result = await _deleteFiling.HandleAsync(new DeleteFilingCommand(id), ct);
                if (!result.IsSuccess)
                {
                    ErrorMessage = result.Error.Message;
                    return;
                }

                // Decrement page when last item on a non-first page is deleted
                if (Rows.Count == 1 && _currentPage > 1)
                    _currentPage--;

                await LoadPageAsync(ct);
            },
            outputScheduler: _scheduler);

        ExportCommand = ReactiveCommand.CreateFromTask<Guid>(
            async (id, ct) =>
            {
                var result = await _exportFiling.HandleAsync(new ExportFilingCommand(id), ct);
                if (!result.IsSuccess)
                {
                    ErrorMessage = result.Error.Message;
                    return;
                }
                await _saveFile(result.Value);
            },
            outputScheduler: _scheduler);

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

                var message = string.Format(Strings.BulkDelete_Filings_Confirmation_Message, selectedIds.Count);
                var confirmed = await _confirmDelete(message);
                if (!confirmed) return;

                var result = await _bulkDeleteFilings.HandleAsync(
                    new BulkDeleteFilingsCommand(selectedIds), ct);

                if (!result.IsSuccess)
                {
                    ErrorMessage = Strings.BulkDelete_Error_Failed;
                    return;
                }

                // Decrement page when all visible items on a non-first page are deleted
                if (selectedIds.Count == Rows.Count && _currentPage > 1)
                    _currentPage--;

                await LoadPageAsync(ct);
            },
            this.WhenAnyValue(x => x.HasSelection),
            outputScheduler: _scheduler);

        // Feature 046: Server-side column sort command with 3-state cycle.
        // Same column, ascending  → descending (keep page).
        // Same column, descending → null/unsorted (keep page).
        // Different column or null → ascending, reset to page 1.
        ApplySortCommand = ReactiveCommand.CreateFromTask<(string ColumnTag, bool? CurrentDirection)>(
            async (args, ct) =>
            {
                if (!Enum.TryParse<FilingSortColumn>(args.ColumnTag, out var newColumn))
                    return;

                if (_sortColumn == newColumn)
                {
                    if (!_sortDescending)
                    {
                        // Ascending → descending (second click on same column); keep page.
                        _sortDescending = true;
                        this.RaisePropertyChanged(nameof(SortDescending));
                    }
                    else
                    {
                        // Descending → null/unsorted (third click on same column); keep page.
                        _sortColumn = null;
                        _sortDescending = false;
                        this.RaisePropertyChanged(nameof(SortColumn));
                        this.RaisePropertyChanged(nameof(SortDescending));
                    }
                }
                else
                {
                    // Different column (or currently unsorted) → ascending, reset to page 1.
                    _sortColumn = newColumn;
                    _sortDescending = false;
                    _currentPage = 1;
                    this.RaisePropertyChanged(nameof(SortColumn));
                    this.RaisePropertyChanged(nameof(SortDescending));
                    this.RaisePropertyChanged(nameof(CurrentPage));
                }

                await LoadPageAsync(ct);
            },
            outputScheduler: _scheduler);

        this.WhenActivated(disposables =>
        {
            LoadPageCommand.Execute().Subscribe().DisposeWith(disposables);

            // M2: InvokeCommand pattern prevents undisposed Subscribe() calls in property setters
            this.WhenAnyValue(x => x.ShowAll)
                .Skip(1)
                .Select(_ => Unit.Default)
                .InvokeCommand(LoadPageCommand)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ReportIdFilter)
                .Skip(1)
                .Select(_ => Unit.Default)
                .InvokeCommand(LoadPageCommand)
                .DisposeWith(disposables);

            // C2: dispose row subscriptions on every deactivation cycle
            Disposable.Create(() => _rowSubscriptions.Clear())
                .DisposeWith(disposables);

            LoadPageCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            AdvanceStatusCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            SavePaymentRefCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            DeleteCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            ExportCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            BulkDeleteCommand.ThrownExceptions
                .Subscribe(ex => ErrorMessage = ex.Message)
                .DisposeWith(disposables);
            ApplySortCommand.ThrownExceptions
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
                .Subscribe(_ =>
                {
                    SelectedCount = Rows.Count(r => r.IsSelected);
                    this.RaisePropertyChanged(nameof(IsAllSelected));
                })
                .DisposeWith(_rowSubscriptions);
        }
        SelectedCount = Rows.Count(r => r.IsSelected);
        this.RaisePropertyChanged(nameof(IsAllSelected));
    }

    private async Task LoadPageAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var filter = _showAll ? FilingFilterMode.All : FilingFilterMode.Unpaid;
            var sortColumn = _sortColumn ?? FilingSortColumn.FilingDeadline;
            var result = await _getFilings.HandleAsync(
                new GetFilingsQuery(filter, _currentPage, 30, _reportIdFilter, sortColumn, _sortDescending), ct);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error.Message;
                return;
            }

            var page = result.Value;
            Rows.Clear();
            foreach (var dto in page.Rows)
                Rows.Add(FilingRowViewModel.From(
                    dto,
                    args => AdvanceStatusCommand.Execute(args).Subscribe(),
                    id => ExportCommand.Execute(id).Subscribe(),
                    id => DeleteCommand.Execute(id).Subscribe()));

            TotalCount = page.TotalCount;
            TotalPages = page.TotalPages;

            // Clamp current page if filter reduced total pages
            var clampedPage = Math.Min(_currentPage, page.TotalPages);
            if (clampedPage != _currentPage)
            {
                _currentPage = clampedPage;
                this.RaisePropertyChanged(nameof(CurrentPage));
            }

            this.RaisePropertyChanged(nameof(IsEmpty));
            this.RaisePropertyChanged(nameof(HasItems));
            this.RaisePropertyChanged(nameof(HasPreviousPage));
            this.RaisePropertyChanged(nameof(HasNextPage));
            this.RaisePropertyChanged(nameof(PageIndicator));

            RebuildRowSubscriptions();
        }
        finally
        {
            IsLoading = false;
        }
    }
}
