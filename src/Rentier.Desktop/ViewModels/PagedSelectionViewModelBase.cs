using System.Collections.ObjectModel;
using System.ComponentModel;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using Rentier.Desktop.Extensions;
using ReactiveUI;
using Rentier.Application.Common;
using Rentier.Desktop.Resources;

namespace Rentier.Desktop.ViewModels;

/// <summary>One page of rows plus the totals the pager needs.</summary>
public sealed record PageOf<TRow>(IReadOnlyList<TRow> Rows, int TotalCount, int TotalPages);

/// <summary>
/// Base for server-side paged lists with row selection and column filters (Filings, Reports).
/// <para>
/// Owns the load / page / filter state machine so derived ViewModels only describe their
/// query (<see cref="QueryPageAsync"/>) and their page-specific commands. The rule
/// "anything that changes the query goes back to page 1" lives in <see cref="PagedQueryState"/>
/// and is applied here for column filters; derived classes call
/// <see cref="PagedQueryState.ResetToFirstPage"/> on <see cref="Paging"/> for their own inputs.
/// </para>
/// </summary>
public abstract class PagedSelectionViewModelBase<TRow> : ReactiveObject, IActivatableViewModel
    where TRow : ReactiveObject, ISelectableRow
{
    public ViewModelActivator Activator { get; } = new();

    protected readonly ISequencer _scheduler;
    private int _selectedCount;
    private bool _isUpdatingSelection;
    private bool _isLoading;
    private string? _errorMessage;
    private bool _hasActiveFilters;
    private IReadOnlyList<IColumnFilter> _filters = [];
    protected readonly MultipleDisposable _rowSubscriptions = new();

    private readonly ObservableAsPropertyHelper<bool> _hasSelection;
    private readonly ObservableAsPropertyHelper<string> _deleteSelectedLabel;

    public ObservableCollection<TRow> Rows { get; } = new();

    /// <summary>Current page and totals; the single owner of paging state.</summary>
    protected PagedQueryState Paging { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        protected set
        {
            if (value == _isLoading) return;
            this.RaiseAndSetIfChanged(ref _isLoading, value);
            this.RaisePropertyChanged(nameof(HasPreviousPage));
            this.RaisePropertyChanged(nameof(HasNextPage));
            RaiseEmptyStateChanged();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        protected set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public int CurrentPage => Paging.CurrentPage;
    public int TotalPages => Paging.TotalPages;
    public int TotalCount => Paging.TotalCount;
    public bool HasPreviousPage => Paging.HasPreviousPage && !IsLoading;
    public bool HasNextPage => Paging.HasNextPage && !IsLoading;
    public string PageIndicator => string.Format(PageIndicatorFormat, CurrentPage, TotalPages);

    /// <summary>Localized "Page {0} of {1}" format for this list.</summary>
    protected abstract string PageIndicatorFormat { get; }

    public int SelectedCount
    {
        get => _selectedCount;
        protected set => this.RaiseAndSetIfChanged(ref _selectedCount, value);
    }

    public bool HasSelection => _hasSelection.Value;
    public string DeleteSelectedLabel => _deleteSelectedLabel.Value;
    public bool HasItems => Rows.Count > 0;
    public bool IsEmpty => Rows.Count == 0 && !IsLoading;
    public bool IsEmptyWithFilters => IsEmpty && HasActiveFilters;

    /// <summary>True when at least one registered column filter is committed.</summary>
    public bool HasActiveFilters
    {
        get => _hasActiveFilters;
        private set
        {
            if (value == _hasActiveFilters) return;
            this.RaiseAndSetIfChanged(ref _hasActiveFilters, value);
            this.RaisePropertyChanged(nameof(IsEmptyWithFilters));
        }
    }

    public ReactiveCommand<RxVoid, RxVoid> SelectAllCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ClearSelectionCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> LoadPageCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> PreviousPageCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> NextPageCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ClearErrorCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ClearFiltersCommand { get; }

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

    protected PagedSelectionViewModelBase(ISequencer scheduler)
    {
        _scheduler = scheduler;
        Paging.PropertyChanged += OnPagingChanged;

        _hasSelection = this.WhenAnyValue(x => x.SelectedCount)
            .Select(c => c > 0)
            .ToProperty(this, x => x.HasSelection, scheduler: _scheduler);

        _deleteSelectedLabel = this.WhenAnyValue(x => x.SelectedCount)
            .Select(c => string.Format(Strings.BulkDelete_Button_Template, c))
            .ToProperty(this, x => x.DeleteSelectedLabel,
                initialValue: string.Format(Strings.BulkDelete_Button_Template, 0),
                scheduler: _scheduler);

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

        LoadPageCommand = ReactiveCommand.CreateFromTask(
            LoadPageAsync, outputScheduler: _scheduler);

        PreviousPageCommand = ReactiveCommand.CreateFromTask(
            async (CancellationToken ct) =>
            {
                Paging.MoveToPreviousPage();
                await LoadPageAsync(ct);
            },
            this.WhenAnyValue(x => x.HasPreviousPage),
            outputScheduler: _scheduler);

        NextPageCommand = ReactiveCommand.CreateFromTask(
            async (CancellationToken ct) =>
            {
                Paging.MoveToNextPage();
                await LoadPageAsync(ct);
            },
            this.WhenAnyValue(x => x.HasNextPage),
            outputScheduler: _scheduler);

        ClearErrorCommand = ReactiveCommand.Create(
            () => { ErrorMessage = null; },
            outputScheduler: _scheduler);

        ClearFiltersCommand = ReactiveCommand.CreateFromTask(
            async (CancellationToken ct) =>
            {
                ClearFilters();
                await ReloadFromFirstPageAsync(ct);
            },
            this.WhenAnyValue(x => x.HasActiveFilters),
            outputScheduler: _scheduler);

        this.WhenActivated((MultipleDisposable disposables) =>
        {
            LoadPageCommand.Execute().Subscribe().DisposeWith(disposables);

            // Any column filter applied → page 1 + reload.
            _filters.Select(f => f.Applied)
                .ToArray()
                .Merge()
                .ObserveOn(_scheduler)
                .Do(_ => Paging.ResetToFirstPage())
                .Select(_ => RxVoid.Default)
                .InvokeCommand(LoadPageCommand)
                .DisposeWith(disposables);

            // Dispose row subscriptions on every deactivation cycle.
            new ActionDisposable(() => _rowSubscriptions.Clear())
                .DisposeWith(disposables);

            SurfaceErrors(disposables,
                LoadPageCommand.ThrownExceptions,
                PreviousPageCommand.ThrownExceptions,
                NextPageCommand.ThrownExceptions,
                ClearFiltersCommand.ThrownExceptions);

            OnActivated(disposables);
        });
    }

    /// <summary>Fetches one page for the current filters and sort order.</summary>
    protected abstract Task<Result<PageOf<TRow>, Error>> QueryPageAsync(
        int page, int pageSize, CancellationToken ct);

    /// <summary>Hook for page-specific activation wiring; runs after the initial load is started.</summary>
    protected virtual void OnActivated(MultipleDisposable disposables)
    {
    }

    /// <summary>Hook invoked after a page was loaded into <see cref="Rows"/>.</summary>
    protected virtual void OnPageLoaded()
    {
    }

    /// <summary>
    /// Registers the column filter flyouts of this list. Applying any of them resets to page 1
    /// and reloads; <see cref="HasActiveFilters"/> and <see cref="ClearFiltersCommand"/> follow them.
    /// Call once from the derived constructor.
    /// </summary>
    protected void RegisterFilters(params IColumnFilter[] filters)
    {
        _filters = filters;
        foreach (var filter in filters)
            filter.PropertyChanged += OnFilterChanged;
    }

    /// <summary>Clears every registered column filter without reloading.</summary>
    protected void ClearFilters()
    {
        foreach (var filter in _filters)
            filter.Clear();
    }

    /// <summary>Routes unhandled command exceptions into <see cref="ErrorMessage"/>.</summary>
    protected void SurfaceErrors(MultipleDisposable disposables, params IObservable<Exception>[] errors)
    {
        foreach (var source in errors)
            source.Subscribe(ex => ErrorMessage = ex.Message).DisposeWith(disposables);
    }

    /// <summary>The query changed shape: go back to page 1 and reload.</summary>
    protected Task ReloadFromFirstPageAsync(CancellationToken ct)
    {
        Paging.ResetToFirstPage();
        return LoadPageAsync(ct);
    }

    /// <summary>Reloads after <paramref name="removedCount"/> visible rows were deleted.</summary>
    protected Task ReloadAfterRemovalAsync(int removedCount, CancellationToken ct)
    {
        Paging.StepBackIfPageEmptied(removedCount, Rows.Count);
        return LoadPageAsync(ct);
    }

    protected async Task LoadPageAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await QueryPageAsync(Paging.CurrentPage, Paging.PageSize, ct);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error.Message;
                return;
            }

            var page = result.Value;
            Rows.Clear();
            foreach (var row in page.Rows)
                Rows.Add(row);

            Paging.ApplyResult(page.TotalCount, page.TotalPages);

            this.RaisePropertyChanged(nameof(HasItems));
            RaiseEmptyStateChanged();
            OnPageLoaded();

            RebuildRowSubscriptions();
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected void RebuildRowSubscriptions()
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

    private void RaiseEmptyStateChanged()
    {
        this.RaisePropertyChanged(nameof(IsEmpty));
        this.RaisePropertyChanged(nameof(IsEmptyWithFilters));
    }

    private void OnPagingChanged(object? sender, PropertyChangedEventArgs e)
    {
        // CurrentPage / TotalPages / TotalCount / HasPreviousPage / HasNextPage are re-exposed
        // under the same names, so the notification is forwarded as-is.
        this.RaisePropertyChanged(e.PropertyName);
        if (e.PropertyName is nameof(PagedQueryState.CurrentPage) or nameof(PagedQueryState.TotalPages))
            this.RaisePropertyChanged(nameof(PageIndicator));
    }

    private void OnFilterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IColumnFilter.IsActive))
            HasActiveFilters = _filters.Any(f => f.IsActive);
    }
}
