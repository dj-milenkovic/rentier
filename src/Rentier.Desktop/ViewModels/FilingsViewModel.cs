using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using Rentier.Desktop.Extensions;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Enums;
using Rentier.Application.Queries;
using Rentier.Desktop.Models;
using Rentier.Desktop.Resources;
using Rentier.Desktop.Services;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.ViewModels;

public sealed class FilingsViewModel : PagedSelectionViewModelBase<FilingRowViewModel>
{
    private const string EXPORT_FILE_PATTERN = "*.xml";

    private readonly FilingsHandlers _handlers;
    private readonly IConfirmationService _confirmation;
    private readonly IFileSaveService _fileSave;

    private readonly SortState<FilingSortColumn> _sort = new(FilingSortColumn.FilingDeadline, descending: true);
    private bool _showAll = true;
    private Guid? _reportIdFilter;
    private readonly ObservableAsPropertyHelper<bool> _hasReportFilter;

    public bool ShowAll
    {
        get => _showAll;
        set
        {
            this.RaiseAndSetIfChanged(ref _showAll, value);
            Paging.ResetToFirstPage();
        }
    }

    public Guid? ReportIdFilter
    {
        get => _reportIdFilter;
        set
        {
            this.RaiseAndSetIfChanged(ref _reportIdFilter, value);
            Paging.ResetToFirstPage();
            if (value.HasValue)
                ClearFilters();
            this.RaisePropertyChanged(nameof(IsFilterRowEnabled));
        }
    }

    public FilingSortColumn? SortColumn => _sort.Column;
    public bool SortDescending => _sort.Descending;

    public bool HasReportFilter => _hasReportFilter.Value;

    public string EmptyStateMessage => HasActiveFilters
        ? Strings.Filter_NoResults
        : Strings.Filings_Empty;

    protected override string PageIndicatorFormat => Strings.Filings_Page_Indicator;

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

    public ReactiveCommand<RxVoid, RxVoid> ClearReportFilterCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> NewFilingCommand { get; }
    public ReactiveCommand<(Guid Id, FilingStatus NewStatus), RxVoid> AdvanceStatusCommand { get; }
    public ReactiveCommand<(Guid Id, string? Reference), RxVoid> SavePaymentRefCommand { get; }
    public ReactiveCommand<Guid, RxVoid> DeleteCommand { get; }
    public ReactiveCommand<Guid, RxVoid> ExportCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> BulkDeleteCommand { get; }
    public ReactiveCommand<(string ColumnTag, bool? CurrentDirection), RxVoid> ApplySortCommand { get; }

    public FilingsViewModel(
        FilingsHandlers handlers,
        IConfirmationService confirmation,
        IFileSaveService fileSave,
        INavigationService navigation,
        ISequencer? scheduler = null) : base(scheduler ?? RxSchedulers.MainThreadScheduler)
    {
        _handlers = handlers;
        _confirmation = confirmation;
        _fileSave = fileSave;

        _sort.PropertyChanged += (_, e) => this.RaisePropertyChanged(
            e.PropertyName == nameof(SortState<FilingSortColumn>.Column)
                ? nameof(SortColumn)
                : nameof(SortDescending));

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
        RegisterFilters(
            StatusFilter, IncomeTypeFilter, PayingEntityFilter, PaymentReferenceFilter, DeadlineFilter);

        _hasReportFilter = this.WhenAnyValue(x => x.ReportIdFilter)
            .Select(id => id.HasValue)
            .ToProperty(this, x => x.HasReportFilter, scheduler: _scheduler);

        ClearReportFilterCommand = ReactiveCommand.Create(
            () => { ReportIdFilter = null; },
            this.WhenAnyValue(x => x.HasReportFilter),
            outputScheduler: _scheduler);

        NewFilingCommand = ReactiveCommand.Create(
            navigation.NavigateToManualFiling,
            outputScheduler: _scheduler);

        AdvanceStatusCommand = ReactiveCommand.CreateFromTask<(Guid Id, FilingStatus NewStatus)>(
            (args, ct) => ReloadPreservingErrorAsync(
                _handlers.UpdateStatus.HandleAsync(new UpdateFilingStatusCommand(args.Id, args.NewStatus), ct), ct),
            outputScheduler: _scheduler);

        SavePaymentRefCommand = ReactiveCommand.CreateFromTask<(Guid Id, string? Reference)>(
            (args, ct) => ReloadPreservingErrorAsync(
                _handlers.UpdateReference.HandleAsync(new UpdatePaymentReferenceCommand(args.Id, args.Reference), ct), ct),
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

        // Feature 046: Server-side column sort command with 3-state cycle (see SortState).
        // Same column toggles direction / clears and keeps the page; a different column resets to page 1.
        ApplySortCommand = ReactiveCommand.CreateFromTask<(string ColumnTag, bool? CurrentDirection)>(
            OnApplySortAsync,
            outputScheduler: _scheduler);
    }

    protected override void OnActivated(MultipleDisposable disposables)
    {
        // M2: InvokeCommand pattern prevents undisposed Subscribe() calls in property setters
        this.WhenAnyValue(x => x.ShowAll)
            .Skip(1)
            .Select(_ => RxVoid.Default)
            .InvokeCommand(LoadPageCommand)
            .DisposeWith(disposables);

        this.WhenAnyValue(x => x.ReportIdFilter)
            .Skip(1)
            .Select(_ => RxVoid.Default)
            .InvokeCommand(LoadPageCommand)
            .DisposeWith(disposables);

        SurfaceErrors(disposables,
            AdvanceStatusCommand.ThrownExceptions,
            SavePaymentRefCommand.ThrownExceptions,
            DeleteCommand.ThrownExceptions,
            ExportCommand.ThrownExceptions,
            BulkDeleteCommand.ThrownExceptions,
            ApplySortCommand.ThrownExceptions);
    }

    protected override void OnPageLoaded() =>
        this.RaisePropertyChanged(nameof(EmptyStateMessage));

    protected override async Task<Result<PageOf<FilingRowViewModel>, Error>> QueryPageAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var filter = _showAll ? FilingFilterMode.All : FilingFilterMode.Unpaid;
        var sortColumn = _sort.Column ?? FilingSortColumn.FilingDeadline;
        var columnFilter = _reportIdFilter.HasValue ? null : new FilingColumnFilter(
            PayingEntity: PayingEntityFilter.GetCommittedValue(),
            PaymentReference: PaymentReferenceFilter.GetCommittedValue(),
            Statuses: StatusFilter.GetCommittedValues(),
            IncomeTypes: IncomeTypeFilter.GetCommittedValues(),
            FilingDeadlineText: DeadlineFilter.GetCommittedValue());

        var result = await _handlers.GetFilings.HandleAsync(
            new GetFilingsQuery(filter, page, pageSize, _reportIdFilter, sortColumn, _sort.Descending, columnFilter), ct);

        if (!result.IsSuccess)
            return Result<PageOf<FilingRowViewModel>, Error>.Failure(result.Error);

        var rows = result.Value.Rows
            .Select(dto => FilingRowViewModel.From(
                dto,
                args => AdvanceStatusCommand.Execute(args).Subscribe(),
                id => ExportCommand.Execute(id).Subscribe(),
                id => DeleteCommand.Execute(id).Subscribe()))
            .ToList();

        return Result<PageOf<FilingRowViewModel>, Error>.Success(
            new PageOf<FilingRowViewModel>(rows, result.Value.TotalCount, result.Value.TotalPages));
    }

    /// <summary>Runs a row update, reloads the page, and keeps the update's error visible after the reload.</summary>
    private async Task ReloadPreservingErrorAsync(Task<Result<VoidResult, Error>> update, CancellationToken ct)
    {
        var result = await update;
        await LoadPageAsync(ct);
        if (!result.IsSuccess)
            ErrorMessage = result.Error.Message;
    }

    private Task<bool> ConfirmDeleteAsync(string message) =>
        _confirmation.ConfirmAsync(new ConfirmationRequest(
            Strings.Filings_Delete_Confirmation_Title,
            message,
            Strings.Filings_Delete_Confirm_Button,
            Strings.Filings_Delete_Cancel_Button));

    private async Task OnDeleteAsync(Guid id, CancellationToken ct)
    {
        if (!await ConfirmDeleteAsync(Strings.Filings_Delete_Confirmation_Message))
            return;

        var result = await _handlers.DeleteFiling.HandleAsync(new DeleteFilingCommand(id), ct);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error.Message;
            return;
        }

        await ReloadAfterRemovalAsync(removedCount: 1, ct);
    }

    private async Task OnExportAsync(Guid id, CancellationToken ct)
    {
        var result = await _handlers.ExportFiling.HandleAsync(new ExportFilingCommand(id), ct);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error.Message;
            return;
        }

        await _fileSave.SaveAsync(new FileSaveRequest(
            Strings.Filings_Export_SaveDialog_Title,
            result.Value.SuggestedFileName,
            result.Value.Bytes,
            Strings.Filings_Export_Filter_Xml,
            [EXPORT_FILE_PATTERN]));
    }

    private async Task OnBulkDeleteAsync(CancellationToken ct)
    {
        var selectedIds = Rows
            .Where(r => r.IsSelected)
            .Select(r => r.Id)
            .ToList();

        if (selectedIds.Count == 0) return;

        var message = string.Format(Strings.BulkDelete_Filings_Confirmation_Message, selectedIds.Count);
        if (!await ConfirmDeleteAsync(message))
            return;

        var result = await _handlers.BulkDeleteFilings.HandleAsync(
            new BulkDeleteFilingsCommand(selectedIds), ct);

        if (!result.IsSuccess)
        {
            ErrorMessage = Strings.BulkDelete_Error_Failed;
            return;
        }

        await ReloadAfterRemovalAsync(selectedIds.Count, ct);
    }

    private async Task OnApplySortAsync((string ColumnTag, bool? CurrentDirection) args, CancellationToken ct)
    {
        if (!Enum.TryParse<FilingSortColumn>(args.ColumnTag, out var newColumn))
            return;

        if (_sort.Cycle(newColumn))
            Paging.ResetToFirstPage();

        await LoadPageAsync(ct);
    }
}
