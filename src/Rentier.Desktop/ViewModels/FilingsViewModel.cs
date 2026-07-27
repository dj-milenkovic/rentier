using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Reactive;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Enums;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Models;
using Rentier.Desktop.Resources;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.ViewModels;

public sealed class FilingsViewModel : PagedSelectionViewModelBase<FilingRowViewModel>
{
    private readonly IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>> _getFilings;
    private readonly ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>> _updateStatus;
    private readonly ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>> _updateReference;
    private readonly ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>> _deleteFiling;
    private readonly ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>> _exportFiling;
    private readonly ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>> _bulkDeleteFilings;
    private readonly Func<string, Task<bool>> _confirmDelete;
    private readonly Func<ExportFilingResult, Task> _saveFile;
    private readonly Action _navigateToManualFiling;

    private bool _isLoading;
    private string? _errorMessage;
    private bool _showAll = true;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private int _totalCount;
    private Guid? _reportIdFilter;
    private FilingSortColumn? _sortColumn = FilingSortColumn.FilingDeadline;
    private bool _sortDescending = true;
    private readonly ObservableAsPropertyHelper<bool> _hasReportFilter;

    private bool _hasActiveFilters;

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
            if (value.HasValue)
            {
                StatusFilter.Clear();
                IncomeTypeFilter.Clear();
                PayingEntityFilter.Clear();
                PaymentReferenceFilter.Clear();
                DeadlineFilter.Clear();
            }
            this.RaisePropertyChanged(nameof(IsFilterRowEnabled));
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

    // REMOVED: SortIndicatorDisplay — replaced by per-column PathIcon arrows (feature 046, FR-009).

    public bool IsEmpty => Rows.Count == 0 && !IsLoading;
    public bool IsEmptyWithFilters => IsEmpty && HasActiveFilters;

    public string EmptyStateMessage => HasActiveFilters
        ? Strings.Filter_NoResults
        : Strings.Filings_Empty;

    public bool HasPreviousPage => _currentPage > 1 && !IsLoading;
    public bool HasNextPage => _currentPage < _totalPages && !IsLoading;
    public string PageIndicator => string.Format(Strings.Filings_Page_Indicator, _currentPage, _totalPages);

    public bool HasActiveFilters
    {
        get => _hasActiveFilters;
        private set => this.RaiseAndSetIfChanged(ref _hasActiveFilters, value);
    }

    /// <summary>False when a ReportIdFilter is active (flyout filters are disabled while browsing by report).</summary>
    public bool IsFilterRowEnabled => _reportIdFilter == null;

    // ── Feature 050: Column filter flyout ViewModels ──────────────────────────

    /// <summary>Filter flyout for the Status column (multi-select enum).</summary>
    public EnumFilterFlyoutViewModel<FilingStatus> StatusFilter { get; }

    /// <summary>Filter flyout for the Income Type column (multi-select enum).</summary>
    public EnumFilterFlyoutViewModel<IncomeType> IncomeTypeFilter { get; }

    /// <summary>Filter flyout for the Paying Entity column (text search).</summary>
    public TextFilterFlyoutViewModel PayingEntityFilter { get; }

    /// <summary>Filter flyout for the Payment Reference column (text search).</summary>
    public TextFilterFlyoutViewModel PaymentReferenceFilter { get; }

    /// <summary>Filter flyout for the Deadline column (text search on formatted date string).</summary>
    public TextFilterFlyoutViewModel DeadlineFilter { get; }

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
    public ReactiveCommand<Unit, Unit> BulkDeleteCommand { get; }
    public ReactiveCommand<(string ColumnTag, bool? CurrentDirection), Unit> ApplySortCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }

    public FilingsViewModel(
        FilingsHandlers handlers,
        Func<string, Task<bool>> confirmDelete,
        Func<ExportFilingResult, Task> saveFile,
        Action navigateToManualFiling,
        IScheduler? scheduler = null) : base(scheduler ?? RxSchedulers.MainThreadScheduler)
    {
        _getFilings = handlers.GetFilings;
        _updateStatus = handlers.UpdateStatus;
        _updateReference = handlers.UpdateReference;
        _deleteFiling = handlers.DeleteFiling;
        _exportFiling = handlers.ExportFiling;
        _bulkDeleteFilings = handlers.BulkDeleteFilings;
        _confirmDelete = confirmDelete;
        _saveFile = saveFile;
        _navigateToManualFiling = navigateToManualFiling;

        // Feature 050: initialise flyout filter ViewModels
        StatusFilter = new EnumFilterFlyoutViewModel<FilingStatus>(new[]
        {
            new CheckableItem<FilingStatus>(Strings.Filter_StatusInit,  FilingStatus.Init),
            new CheckableItem<FilingStatus>(Strings.Filter_StatusFiled, FilingStatus.Filed),
            new CheckableItem<FilingStatus>(Strings.Filter_StatusPaid,  FilingStatus.Paid),
        });
        IncomeTypeFilter = new EnumFilterFlyoutViewModel<IncomeType>(new[]
        {
            new CheckableItem<IncomeType>(Strings.Filter_IncomeDividend, IncomeType.Dividend),
            new CheckableItem<IncomeType>(Strings.Filter_IncomeInterest, IncomeType.Interest),
        });
        PayingEntityFilter = new TextFilterFlyoutViewModel();
        PaymentReferenceFilter = new TextFilterFlyoutViewModel();
        DeadlineFilter = new TextFilterFlyoutViewModel();

        _hasReportFilter = this.WhenAnyValue(x => x.ReportIdFilter)
            .Select(id => id.HasValue)
            .ToProperty(this, x => x.HasReportFilter, scheduler: _scheduler);

        LoadPageCommand = ReactiveCommand.CreateFromTask(
            LoadPageAsync, outputScheduler: _scheduler);

        ClearReportFilterCommand = ReactiveCommand.Create(
            () => { ReportIdFilter = null; },
            this.WhenAnyValue(x => x.HasReportFilter),
            outputScheduler: _scheduler);

        PreviousPageCommand = ReactiveCommand.CreateFromTask(
            OnPreviousPageAsync,
            this.WhenAnyValue(x => x.HasPreviousPage),
            outputScheduler: _scheduler);

        NextPageCommand = ReactiveCommand.CreateFromTask(
            OnNextPageAsync,
            this.WhenAnyValue(x => x.HasNextPage),
            outputScheduler: _scheduler);

        ClearErrorCommand = ReactiveCommand.Create(
            () => { ErrorMessage = null; },
            outputScheduler: _scheduler);

        NewFilingCommand = ReactiveCommand.Create(
            () => _navigateToManualFiling(),
            outputScheduler: _scheduler);

        AdvanceStatusCommand = ReactiveCommand.CreateFromTask<(Guid Id, FilingStatus NewStatus)>(
            OnAdvanceStatusAsync,
            outputScheduler: _scheduler);

        SavePaymentRefCommand = ReactiveCommand.CreateFromTask<(Guid Id, string? Reference)>(
            OnSavePaymentRefAsync,
            outputScheduler: _scheduler);

        DeleteCommand = ReactiveCommand.CreateFromTask<Guid>(
            OnDeleteAsync,
            outputScheduler: _scheduler);

        ExportCommand = ReactiveCommand.CreateFromTask<Guid>(
            OnExportAsync,
            outputScheduler: _scheduler);

        BulkDeleteCommand = ReactiveCommand.CreateFromTask(
            OnBulkDeleteAsync,
            this.WhenAnyValue(x => x.HasSelection),
            outputScheduler: _scheduler);

        // Feature 046: Server-side column sort command with 3-state cycle.
        // Same column, ascending  → descending (keep page).
        // Same column, descending → null/unsorted (keep page).
        // Different column or null → ascending, reset to page 1.
        ApplySortCommand = ReactiveCommand.CreateFromTask<(string ColumnTag, bool? CurrentDirection)>(
            OnApplySortAsync,
            outputScheduler: _scheduler);

        ClearFiltersCommand = ReactiveCommand.CreateFromTask(
            OnClearFiltersAsync,
            this.WhenAnyValue(x => x.HasActiveFilters),
            outputScheduler: _scheduler);

        this.WhenActivated(RegisterActivation);
    }

    private async Task OnPreviousPageAsync(CancellationToken ct)
    {
        _currentPage--;
        await LoadPageAsync(ct);
    }

    private async Task OnNextPageAsync(CancellationToken ct)
    {
        _currentPage++;
        await LoadPageAsync(ct);
    }

    private async Task OnAdvanceStatusAsync((Guid Id, FilingStatus NewStatus) args, CancellationToken ct)
    {
        var result = await _updateStatus.HandleAsync(
            new UpdateFilingStatusCommand(args.Id, args.NewStatus), ct);
        var errorToPreserve = result.IsSuccess ? null : result.Error.Message;
        await LoadPageAsync(ct);
        if (errorToPreserve is not null)
            ErrorMessage = errorToPreserve;
    }

    private async Task OnSavePaymentRefAsync((Guid Id, string? Reference) args, CancellationToken ct)
    {
        var result = await _updateReference.HandleAsync(
            new UpdatePaymentReferenceCommand(args.Id, args.Reference), ct);
        var errorToPreserve = result.IsSuccess ? null : result.Error.Message;
        await LoadPageAsync(ct);
        if (errorToPreserve is not null)
            ErrorMessage = errorToPreserve;
    }

    private async Task OnDeleteAsync(Guid id, CancellationToken ct)
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
    }

    private async Task OnExportAsync(Guid id, CancellationToken ct)
    {
        var result = await _exportFiling.HandleAsync(new ExportFilingCommand(id), ct);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error.Message;
            return;
        }
        await _saveFile(result.Value);
    }

    private async Task OnBulkDeleteAsync(CancellationToken ct)
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
    }

    private async Task OnApplySortAsync((string ColumnTag, bool? CurrentDirection) args, CancellationToken ct)
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
    }

    private async Task OnClearFiltersAsync(CancellationToken ct)
    {
        StatusFilter.Clear();
        IncomeTypeFilter.Clear();
        PayingEntityFilter.Clear();
        PaymentReferenceFilter.Clear();
        DeadlineFilter.Clear();
        _currentPage = 1;
        this.RaisePropertyChanged(nameof(CurrentPage));
        await LoadPageAsync(ct);
    }

    private void RegisterActivation(MultipleDisposable disposables)
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

        // Feature 050: when any flyout applies a filter → reset page + reload
        Observable.Merge(
                StatusFilter.Applied,
                IncomeTypeFilter.Applied,
                PayingEntityFilter.Applied,
                PaymentReferenceFilter.Applied,
                DeadlineFilter.Applied)
            .ObserveOn(_scheduler)
            .Do(_ => { _currentPage = 1; this.RaisePropertyChanged(nameof(CurrentPage)); })
            .Select(_ => Unit.Default)
            .InvokeCommand(LoadPageCommand)
            .DisposeWith(disposables);

        // Feature 050: update HasActiveFilters whenever any flyout's IsActive changes
        Observable.CombineLatest(
                this.WhenAnyValue(x => x.StatusFilter.IsActive),
                this.WhenAnyValue(x => x.IncomeTypeFilter.IsActive),
                this.WhenAnyValue(x => x.PayingEntityFilter.IsActive),
                this.WhenAnyValue(x => x.PaymentReferenceFilter.IsActive),
                this.WhenAnyValue(x => x.DeadlineFilter.IsActive),
                (s, i, p, r, d) => s || i || p || r || d)
            .Subscribe(active => HasActiveFilters = active)
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
        ClearFiltersCommand.ThrownExceptions
            .Subscribe(ex => ErrorMessage = ex.Message)
            .DisposeWith(disposables);
    }

    private async Task LoadPageAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var filter = _showAll ? FilingFilterMode.All : FilingFilterMode.Unpaid;
            var sortColumn = _sortColumn ?? FilingSortColumn.FilingDeadline;
            var columnFilter = _reportIdFilter.HasValue ? null : new FilingColumnFilter(
                PayingEntity: PayingEntityFilter.GetCommittedValue(),
                PaymentReference: PaymentReferenceFilter.GetCommittedValue(),
                Statuses: StatusFilter.GetCommittedValues(),
                IncomeTypes: IncomeTypeFilter.GetCommittedValues(),
                FilingDeadlineText: DeadlineFilter.GetCommittedValue());

            var result = await _getFilings.HandleAsync(
                new GetFilingsQuery(filter, _currentPage, 30, _reportIdFilter, sortColumn, _sortDescending, columnFilter), ct);

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
            this.RaisePropertyChanged(nameof(IsEmptyWithFilters));
            this.RaisePropertyChanged(nameof(EmptyStateMessage));
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
