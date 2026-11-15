using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using Rentier.Desktop.Extensions;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Queries;
using Rentier.Desktop.Models;
using Rentier.Desktop.Resources;
using Rentier.Desktop.Services;
using Rentier.Domain.Enums;

namespace Rentier.Desktop.ViewModels;

public sealed class ReportsViewModel : PagedSelectionViewModelBase<ReportRowViewModel>
{
    private readonly ReportsHandlers _handlers;
    private readonly IConfirmationService _confirmation;
    private readonly IReportImportDialogService _importDialog;

    private bool _sortDescending = true;

    // Filter flyout ViewModels
    public TextFilterFlyoutViewModel NameFilter { get; }
    public TextFilterFlyoutViewModel ImporterFilter { get; }
    public TextFilterFlyoutViewModel ImportDateFilter { get; }
    public TextFilterFlyoutViewModel EmailDateFilter { get; }
    public TextFilterFlyoutViewModel FilingCountFilter { get; }
    public EnumFilterFlyoutViewModel<ReportStatus> StatusFilter { get; }

    protected override string PageIndicatorFormat => Strings.Reports_Page_Indicator;

    /// <summary>When true, reports are sorted newest-first. Changing this property resets the page to 1.</summary>
    public bool SortDescending
    {
        get => _sortDescending;
        set
        {
            if (value == _sortDescending)
                return;

            this.RaiseAndSetIfChanged(ref _sortDescending, value);
            Paging.ResetToFirstPage();
        }
    }

    public ReactiveCommand<RxVoid, RxVoid> ImportCommand { get; }
    public ReactiveCommand<Guid, RxVoid> DeleteCommand { get; }
    public ReactiveCommand<Guid, RxVoid> ViewFilingsCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> BulkDeleteCommand { get; }

    // Keep LoadReportsCommand as an alias for backward compat with existing AXAML / tests
    public ReactiveCommand<RxVoid, RxVoid> LoadReportsCommand => LoadPageCommand;

    public ReportsViewModel(
        ReportsHandlers handlers,
        IConfirmationService confirmation,
        IReportImportDialogService importDialog,
        INavigationService navigation,
        ISequencer? scheduler = null) : base(scheduler ?? RxSchedulers.MainThreadScheduler)
    {
        _handlers = handlers;
        _confirmation = confirmation;
        _importDialog = importDialog;

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
        RegisterFilters(
            NameFilter, ImporterFilter, ImportDateFilter, EmailDateFilter, FilingCountFilter, StatusFilter);

        ImportCommand = ReactiveCommand.CreateFromTask(
            ImportAsync, outputScheduler: _scheduler);

        DeleteCommand = ReactiveCommand.CreateFromTask<Guid>(
            DeleteAsync, outputScheduler: _scheduler);

        ViewFilingsCommand = ReactiveCommand.Create<Guid>(
            id => navigation.NavigateToFilings(id), outputScheduler: _scheduler);

        BulkDeleteCommand = ReactiveCommand.CreateFromTask(
            OnBulkDeleteAsync,
            this.WhenAnyValue(x => x.HasSelection),
            outputScheduler: _scheduler);
    }

    protected override void OnActivated(MultipleDisposable disposables)
    {
        // Sort descending change triggers reload
        this.WhenAnyValue(x => x.SortDescending)
            .Skip(1)
            .Select(_ => RxVoid.Default)
            .InvokeCommand(LoadPageCommand)
            .DisposeWith(disposables);

        SurfaceErrors(disposables,
            ImportCommand.ThrownExceptions,
            DeleteCommand.ThrownExceptions,
            BulkDeleteCommand.ThrownExceptions);
    }

    protected override async Task<Result<PageOf<ReportRowViewModel>, Error>> QueryPageAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var result = await _handlers.GetReports.HandleAsync(
            new GetReportsQuery(page, pageSize, _sortDescending, BuildFilter()), ct);

        if (!result.IsSuccess)
            return Result<PageOf<ReportRowViewModel>, Error>.Failure(result.Error);

        var rows = result.Value.Rows.Select(ReportRowViewModel.From).ToList();
        return Result<PageOf<ReportRowViewModel>, Error>.Success(
            new PageOf<ReportRowViewModel>(rows, result.Value.TotalCount, result.Value.TotalPages));
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

    private Task<bool> ConfirmDeleteAsync(string title, string message) =>
        _confirmation.ConfirmAsync(new ConfirmationRequest(
            title,
            message,
            Strings.Reports_Delete_Confirm_Button,
            Strings.Reports_Delete_Cancel_Button));

    private async Task ImportAsync(CancellationToken ct = default)
    {
        var selection = await _importDialog.PickReportAsync();
        if (selection is null)
            return;

        IsLoading = true;
        try
        {
            var result = await _handlers.ImportReport.HandleAsync(
                new ImportReportCommand(selection.ImporterId, selection.FileName, selection.Content), ct);
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
        var confirmed = await ConfirmDeleteAsync(
            Strings.Reports_Delete_Confirmation_Title,
            Strings.Reports_Delete_Confirmation_Message);
        if (!confirmed)
            return;

        IsLoading = true;
        try
        {
            var result = await _handlers.DeleteReport.HandleAsync(new DeleteReportCommand(reportId), ct);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error.Message;
                return;
            }

            await ReloadAfterRemovalAsync(removedCount: 1, ct);
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
        if (!await ConfirmDeleteAsync(Strings.BulkDelete_Reports_Confirmation_Title, message))
            return;

        var result = await _handlers.BulkDeleteReports.HandleAsync(
            new BulkDeleteReportsCommand(selectedIds), ct);

        if (!result.IsSuccess)
        {
            ErrorMessage = Strings.BulkDelete_Error_Failed;
            return;
        }

        await ReloadAfterRemovalAsync(selectedIds.Count, ct);
    }
}
