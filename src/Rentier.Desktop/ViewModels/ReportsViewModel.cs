using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Models;
using Rentier.Desktop.Resources;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.ViewModels;

public sealed class ReportsViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    private readonly IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>> _getReports;
    private readonly ICommandHandler<ImportReportCommand, Result<Guid, Error>> _importReport;
    private readonly ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>> _deleteReport;
    private readonly ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>> _bulkDeleteReports;
    private readonly Func<string, string, Task<bool>> _confirmDelete;
    private readonly Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>> _showImportDialog;
    private readonly Action<Guid> _navigateToFilings;
    private readonly IScheduler _scheduler;

    private bool _isLoading;
    private string? _errorMessage;
    private int _selectedCount;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private int _totalCount;
    private bool _sortDescending = true;
    private bool _isUpdatingSelection;

    private readonly ObservableAsPropertyHelper<bool> _hasSelection;
    private readonly ObservableAsPropertyHelper<string> _deleteSelectedLabel;
    private readonly ObservableAsPropertyHelper<bool> _hasActiveFilters;

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

    public int SelectedCount
    {
        get => _selectedCount;
        private set => this.RaiseAndSetIfChanged(ref _selectedCount, value);
    }

    public bool HasSelection => _hasSelection.Value;
    public string DeleteSelectedLabel => _deleteSelectedLabel.Value;
    public bool HasActiveFilters => _hasActiveFilters.Value;

    // Filter flyout ViewModels
    public TextFilterFlyoutViewModel NameFilter { get; }
    public TextFilterFlyoutViewModel ImporterFilter { get; }
    public TextFilterFlyoutViewModel ImportDateFilter { get; }
    public TextFilterFlyoutViewModel EmailDateFilter { get; }
    public TextFilterFlyoutViewModel FilingCountFilter { get; }
    public EnumFilterFlyoutViewModel<ReportStatus> StatusFilter { get; }

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
            }
            finally
            {
                _isUpdatingSelection = false;
                this.RaisePropertyChanged(nameof(IsAllSelected));
            }
        }
    }

    public bool IsEmpty => Rows.Count == 0 && !IsLoading;
    public bool HasItems => Rows.Count > 0;

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentPage, value);
            this.RaisePropertyChanged(nameof(HasPreviousPage));
            this.RaisePropertyChanged(nameof(HasNextPage));
            this.RaisePropertyChanged(nameof(PageIndicator));
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            this.RaiseAndSetIfChanged(ref _totalPages, value);
            this.RaisePropertyChanged(nameof(HasNextPage));
            this.RaisePropertyChanged(nameof(PageIndicator));
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => this.RaiseAndSetIfChanged(ref _totalCount, value);
    }

    public bool HasPreviousPage => _currentPage > 1 && !IsLoading;
    public bool HasNextPage => _currentPage < _totalPages && !IsLoading;
    public string PageIndicator => string.Format(Strings.Reports_Page_Indicator, _currentPage, _totalPages);

    /// <summary>When true, reports are sorted newest-first. Resetting this property resets the page to 1.</summary>
    public bool SortDescending
    {
        get => _sortDescending;
        set
        {
            if (value == _sortDescending)
                return;

            this.RaiseAndSetIfChanged(ref _sortDescending, value);
            CurrentPage = 1;
        }
    }

    public ObservableCollection<ReportRowViewModel> Rows { get; } = new();

    public ReactiveCommand<Unit, Unit> LoadPageCommand { get; }
    public ReactiveCommand<Unit, Unit> PreviousPageCommand { get; }
    public ReactiveCommand<Unit, Unit> NextPageCommand { get; }
    public ReactiveCommand<Unit, Unit> ImportCommand { get; }
    public ReactiveCommand<Guid, Unit> DeleteCommand { get; }
    public ReactiveCommand<Guid, Unit> ViewFilingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearErrorCommand { get; }
    public ReactiveCommand<Unit, Unit> SelectAllCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> BulkDeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }

    // Keep LoadReportsCommand as an alias for backward compat with existing AXAML / tests
    public ReactiveCommand<Unit, Unit> LoadReportsCommand => LoadPageCommand;

    private readonly CompositeDisposable _rowSubscriptions = new();

    public ReportsViewModel(
        IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>> getReports,
        ICommandHandler<ImportReportCommand, Result<Guid, Error>> importReport,
        ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>> deleteReport,
        ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>> bulkDeleteReports,
        Func<string, string, Task<bool>> confirmDelete,
        Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>> showImportDialog,
        Action<Guid> navigateToFilings,
        IScheduler? scheduler = null)
    {
        _getReports = getReports;
        _importReport = importReport;
        _deleteReport = deleteReport;
        _bulkDeleteReports = bulkDeleteReports;
        _confirmDelete = confirmDelete;
        _showImportDialog = showImportDialog;
        _navigateToFilings = navigateToFilings;
        _scheduler = scheduler ?? RxSchedulers.MainThreadScheduler;

        _hasSelection = this.WhenAnyValue(x => x.SelectedCount)
            .Select(c => c > 0)
            .ToProperty(this, x => x.HasSelection, scheduler: _scheduler);

        _deleteSelectedLabel = this.WhenAnyValue(x => x.SelectedCount)
            .Select(c => string.Format(Strings.BulkDelete_Button_Template, c))
            .ToProperty(this, x => x.DeleteSelectedLabel,
                initialValue: string.Format(Strings.BulkDelete_Button_Template, 0),
                scheduler: _scheduler);

        NameFilter = new TextFilterFlyoutViewModel();
        ImporterFilter = new TextFilterFlyoutViewModel();
        ImportDateFilter = new TextFilterFlyoutViewModel();
        EmailDateFilter = new TextFilterFlyoutViewModel();
        FilingCountFilter = new TextFilterFlyoutViewModel();
        StatusFilter = new EnumFilterFlyoutViewModel<ReportStatus>(new[]
        {
            new CheckableItem<ReportStatus>(Strings.Filter_StatusInit ?? "Init", ReportStatus.Init),
            new CheckableItem<ReportStatus>("Processed", ReportStatus.Processed),
            new CheckableItem<ReportStatus>("Error", ReportStatus.Error),
            new CheckableItem<ReportStatus>("Partial Error", ReportStatus.PartialError),
        });

        _hasActiveFilters = Observable.CombineLatest(
            this.WhenAnyValue(x => x.NameFilter.IsActive),
            this.WhenAnyValue(x => x.ImporterFilter.IsActive),
            this.WhenAnyValue(x => x.ImportDateFilter.IsActive),
            this.WhenAnyValue(x => x.EmailDateFilter.IsActive),
            this.WhenAnyValue(x => x.FilingCountFilter.IsActive),
            this.WhenAnyValue(x => x.StatusFilter.IsActive),
            (a, b, c, d, e, f) => a || b || c || d || e || f)
            .ToProperty(this, x => x.HasActiveFilters, scheduler: _scheduler);

        var canGoToPreviousPage = this.WhenAnyValue(
            x => x.CurrentPage,
            x => x.IsLoading,
            (currentPage, isLoading) => currentPage > 1 && !isLoading);

        var canGoToNextPage = this.WhenAnyValue(
            x => x.CurrentPage,
            x => x.TotalPages,
            x => x.IsLoading,
            (currentPage, totalPages, isLoading) => currentPage < totalPages && !isLoading);

        LoadPageCommand = ReactiveCommand.CreateFromTask(
            LoadPageAsync, outputScheduler: _scheduler);

        PreviousPageCommand = ReactiveCommand.CreateFromTask(
            async (CancellationToken ct) =>
            {
                CurrentPage--;
                await LoadPageAsync(ct);
            },
            canGoToPreviousPage,
            outputScheduler: _scheduler);

        NextPageCommand = ReactiveCommand.CreateFromTask(
            async (CancellationToken ct) =>
            {
                CurrentPage++;
                await LoadPageAsync(ct);
            },
            canGoToNextPage,
            outputScheduler: _scheduler);

        ImportCommand = ReactiveCommand.CreateFromTask(
            ImportAsync, outputScheduler: _scheduler);

        DeleteCommand = ReactiveCommand.CreateFromTask<Guid>(
            DeleteAsync, outputScheduler: _scheduler);

        ViewFilingsCommand = ReactiveCommand.Create<Guid>(
            id => _navigateToFilings(id), outputScheduler: _scheduler);

        ClearErrorCommand = ReactiveCommand.Create(
            () => { ErrorMessage = null; }, outputScheduler: _scheduler);

        ClearFiltersCommand = ReactiveCommand.CreateFromTask(
            async (CancellationToken ct) =>
            {
                NameFilter.Clear();
                ImporterFilter.Clear();
                ImportDateFilter.Clear();
                EmailDateFilter.Clear();
                FilingCountFilter.Clear();
                StatusFilter.Clear();
                CurrentPage = 1;
                await LoadPageAsync(ct);
            },
            this.WhenAnyValue(x => x.HasActiveFilters),
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
            OnBulkDeleteAsync,
            this.WhenAnyValue(x => x.HasSelection),
            outputScheduler: _scheduler);

        this.WhenActivated(disposables =>
        {
            LoadPageCommand.Execute().Subscribe().DisposeWith(disposables);

            // Sort descending change triggers reload
            this.WhenAnyValue(x => x.SortDescending)
                .Skip(1)
                .Select(_ => Unit.Default)
                .InvokeCommand(LoadPageCommand)
                .DisposeWith(disposables);

            // All flyouts: applied → reset page + reload
            Observable.Merge(
                NameFilter.Applied,
                ImporterFilter.Applied,
                ImportDateFilter.Applied,
                EmailDateFilter.Applied,
                FilingCountFilter.Applied,
                StatusFilter.Applied)
                .Do(_ => CurrentPage = 1)
                .InvokeCommand(LoadPageCommand)
                .DisposeWith(disposables);

            // Dispose row subscriptions on deactivation
            Disposable.Create(() => _rowSubscriptions.Clear())
                .DisposeWith(disposables);

            LoadPageCommand.ThrownExceptions
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
                .Skip(1)
                .Subscribe(isNowSelected =>
                {
                    SelectedCount += isNowSelected ? 1 : -1;
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
            var filter = BuildFilter();
            var result = await _getReports.HandleAsync(
                new GetReportsQuery(_currentPage, 30, _sortDescending, filter), ct);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error.Message;
                return;
            }

            var page = result.Value;
            Rows.Clear();
            foreach (var dto in page.Rows)
                Rows.Add(ReportRowViewModel.From(dto));

            TotalCount = page.TotalCount;
            TotalPages = page.TotalPages;

            // Clamp current page if data reduced total pages
            var clampedPage = Math.Min(_currentPage, page.TotalPages);
            if (clampedPage != _currentPage)
                CurrentPage = clampedPage;

            this.RaisePropertyChanged(nameof(IsEmpty));
            this.RaisePropertyChanged(nameof(HasItems));

            RebuildRowSubscriptions();
        }
        finally
        {
            IsLoading = false;
            this.RaisePropertyChanged(nameof(HasPreviousPage));
            this.RaisePropertyChanged(nameof(HasNextPage));
        }
    }

    private ReportColumnFilter? BuildFilter()
    {
        var nameContains = NameFilter.GetCommittedValue();
        var importerContains = ImporterFilter.GetCommittedValue();
        var importDateContains = ImportDateFilter.GetCommittedValue();
        var emailDateContains = EmailDateFilter.GetCommittedValue();
        var filingCountText = FilingCountFilter.GetCommittedValue();
        var statusFilters = StatusFilter.GetCommittedValues();

        int? filingCountValue = null;
        if (int.TryParse(filingCountText, out var parsed))
            filingCountValue = parsed;

        if (nameContains is null && importerContains is null && importDateContains is null &&
            emailDateContains is null && filingCountValue is null && statusFilters is null)
            return null;

        return new ReportColumnFilter(
            NameContains: nameContains,
            ImporterContains: importerContains,
            ImportDateContains: importDateContains,
            EmailDateContains: emailDateContains,
            FilingCountValue: filingCountValue,
            StatusFilters: statusFilters);
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
            await LoadPageAsync(ct);
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
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error.Message;
                return;
            }

            if (Rows.Count == 1 && _currentPage > 1)
                CurrentPage--;

            await LoadPageAsync(ct);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task OnBulkDeleteAsync(CancellationToken ct)
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

        if (selectedIds.Count == Rows.Count && _currentPage > 1)
            CurrentPage--;

        await LoadPageAsync(ct);
    }

}
