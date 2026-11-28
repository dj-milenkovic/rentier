using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Resources;
using Rentier.Desktop.Services;
using Rentier.Desktop.ViewModels;
using Rentier.Domain.Enums;
using ReactiveUI;
using Xunit;

namespace Rentier.UnitTests;

public class ReportsViewModelTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>> MakeGetReports(
        IReadOnlyList<ReportRowDto>? rows = null)
    {
        var h = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        var rowList = rows ?? Array.Empty<ReportRowDto>();
        var page = new ReportsPageResult(rowList, rowList.Count, rowList.Count > 0 ? 1 : 1);
        h.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(page));
        return h;
    }

    private static ICommandHandler<ImportReportCommand, Result<Guid, Error>> MakeImportHandler(
        bool success = true)
    {
        var h = Substitute.For<ICommandHandler<ImportReportCommand, Result<Guid, Error>>>();
        h.HandleAsync(Arg.Any<ImportReportCommand>(), Arg.Any<CancellationToken>())
            .Returns(success
                ? Result<Guid, Error>.Success(Guid.NewGuid())
                : Result<Guid, Error>.Failure(new Error("REPORT_IMPORT_FAILED", "Import error")));
        return h;
    }

    private static ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>> MakeDeleteHandler(
        bool success = true)
    {
        var h = Substitute.For<ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>>();
        h.HandleAsync(Arg.Any<DeleteReportCommand>(), Arg.Any<CancellationToken>())
            .Returns(success
                ? Result<VoidResult, Error>.Success(VoidResult.Value)
                : Result<VoidResult, Error>.Failure(new Error("REPORT_DELETE_FAILED", "Delete error")));
        return h;
    }

    private static ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>> MockBulkDeleteReports() =>
        Substitute.For<ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>>();

    private static ReportRowDto MakeDto(string name = "report.csv") =>
        new(Guid.NewGuid(), name, new DateOnly(2024, 3, 1), null, "My Importer",
            ReportStatus.Processed, 3,
            $"My Importer \u2013 2024-03-01", null);

    private static ReportsViewModel CreateVm(
        IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>? getReports = null,
        ICommandHandler<ImportReportCommand, Result<Guid, Error>>? importHandler = null,
        ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>? deleteHandler = null,
        IConfirmationService? confirmation = null,
        IReportImportDialogService? importDialog = null,
        INavigationService? navigation = null,
        ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>? bulkDelete = null,
        ILocalizationService? localization = null)
    {
        return new ReportsViewModel(
            new ReportsHandlers(
                getReports ?? MakeGetReports(),
                importHandler ?? MakeImportHandler(),
                deleteHandler ?? MakeDeleteHandler(),
                bulkDelete ?? MockBulkDeleteReports()),
            confirmation ?? DesktopTestDoubles.Confirmation(),
            importDialog ?? DesktopTestDoubles.ImportDialog(),
            navigation ?? Substitute.For<INavigationService>(),
            ImmediateSequencer.Instance,
            localization);
    }

    private static LocalizationService Serbian()
    {
        var loc = new LocalizationService();
        loc.SetCulture("sr-Latn");
        return loc;
    }

    // ── US1: Browse reports list ─────────────────────────────────────────────

    [Fact]
    public void OnActivation_TriggersLoadReportsCommand()
    {
        var getReports = MakeGetReports();
        var vm = CreateVm(getReports: getReports);

        using var _activation = vm.Activator.Activate();

        getReports.Received(1).HandleAsync(
            Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void LoadReports_WhenQuerySucceeds_PopulatesRowsAndClearsError()
    {
        var dto = MakeDto();
        var vm = CreateVm(getReports: MakeGetReports([dto]));

        using var _activation = vm.Activator.Activate();

        vm.Rows.Should().HaveCount(1);
        vm.Rows[0].ReportName.Should().Be(dto.ReportName);
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void LoadReports_WhenQueryFails_SetsErrorMessageAndLeavesRowsEmpty()
    {
        var failingHandler = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        failingHandler.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Failure(
                new Error("REPORT_QUERY_FAILED", "DB error")));

        var vm = CreateVm(getReports: failingHandler);

        using var _activation = vm.Activator.Activate();

        vm.ErrorMessage.Should().Be("DB error");
        vm.Rows.Should().BeEmpty();
    }

    [Fact]
    public void IsEmpty_WhenRowsEmpty_IsTrue()
    {
        var vm = CreateVm(getReports: MakeGetReports([]));

        using var _activation = vm.Activator.Activate();

        vm.IsEmpty.Should().BeTrue();
    }

    // ── US2: Import ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportCommand_WhenDialogCancelled_DoesNotCallImportHandler()
    {
        var importHandler = MakeImportHandler();
        var vm = CreateVm(
            importHandler: importHandler,
            importDialog: DesktopTestDoubles.ImportDialog());

        await vm.ImportCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await importHandler.DidNotReceive().HandleAsync(
            Arg.Any<ImportReportCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportCommand_WhenHandlerSucceeds_ReloadsList()
    {
        var importerId = Guid.NewGuid();
        var getReports = MakeGetReports([MakeDto()]);
        var vm = CreateVm(
            getReports: getReports,
            importHandler: MakeImportHandler(success: true),
            importDialog: DesktopTestDoubles.ImportDialog(
                new ReportImportSelection(importerId, "test.csv", [1, 2, 3])));

        await vm.ImportCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        // HandleAsync called twice: once on activation (zero) and once after import
        await getReports.Received().HandleAsync(
            Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportCommand_WhenHandlerFails_SetsErrorMessage()
    {
        var importerId = Guid.NewGuid();
        var vm = CreateVm(
            importHandler: MakeImportHandler(success: false),
            importDialog: DesktopTestDoubles.ImportDialog(
                new ReportImportSelection(importerId, "bad.csv", [1, 2, 3])));

        await vm.ImportCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.ErrorMessage.Should().Be("Import error");
    }

    // ── US4: Delete ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCommand_WhenUserCancels_DoesNotCallDeleteHandler()
    {
        var deleteHandler = MakeDeleteHandler();
        var vm = CreateVm(
            deleteHandler: deleteHandler,
            confirmation: DesktopTestDoubles.Confirmation(false));

        await vm.DeleteCommand.Execute(Guid.NewGuid()).FirstAsync(TestContext.Current.CancellationToken);

        await deleteHandler.DidNotReceive().HandleAsync(
            Arg.Any<DeleteReportCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCommand_WhenUserConfirms_CallsDeleteHandlerAndReloads()
    {
        var reportId = Guid.NewGuid();
        var deleteHandler = MakeDeleteHandler(success: true);
        var getReports = MakeGetReports();
        var vm = CreateVm(
            deleteHandler: deleteHandler,
            getReports: getReports,
            confirmation: DesktopTestDoubles.Confirmation(true));

        await vm.DeleteCommand.Execute(reportId).FirstAsync(TestContext.Current.CancellationToken);

        await deleteHandler.Received(1).HandleAsync(
            Arg.Is<DeleteReportCommand>(c => c!.ReportId == reportId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCommand_WhenHandlerFails_SetsErrorMessage()
    {
        var vm = CreateVm(
            deleteHandler: MakeDeleteHandler(success: false),
            confirmation: DesktopTestDoubles.Confirmation(true));

        await vm.DeleteCommand.Execute(Guid.NewGuid()).FirstAsync(TestContext.Current.CancellationToken);

        vm.ErrorMessage.Should().Be("Delete error");
    }

    // ── US3: View Filings navigation ─────────────────────────────────────────

    [Fact]
    public async Task ViewFilingsCommand_Executed_NavigatesToFilingsOfThatReport()
    {
        var reportId = Guid.NewGuid();
        var navigation = Substitute.For<INavigationService>();
        var vm = CreateVm(navigation: navigation);

        await vm.ViewFilingsCommand.Execute(reportId).FirstAsync(TestContext.Current.CancellationToken);

        navigation.Received(1).NavigateToFilings(reportId);
    }

    [Fact]
    public async Task ImportCommand_WithSelection_SendsSelectedImporterFileAndContent()
    {
        var importerId = Guid.NewGuid();
        byte[] content = [1, 2, 3];
        var importHandler = MakeImportHandler(success: true);
        var vm = CreateVm(
            importHandler: importHandler,
            importDialog: DesktopTestDoubles.ImportDialog(
                new ReportImportSelection(importerId, "test.csv", content)));

        await vm.ImportCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        await importHandler.Received(1).HandleAsync(
            Arg.Is<ImportReportCommand>(c =>
                c!.ImporterId == importerId && c.FileName == "test.csv" && c.CsvContent == content),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCommand_Executed_AsksConfirmationWithLocalizedReportDeleteTexts()
    {
        var confirmation = DesktopTestDoubles.Confirmation();
        var vm = CreateVm(confirmation: confirmation);

        await vm.DeleteCommand.Execute(Guid.NewGuid()).FirstAsync(TestContext.Current.CancellationToken);

        await confirmation.Received(1).ConfirmAsync(new ConfirmationRequest(
            Strings.Reports_Delete_Confirmation_Title,
            Strings.Reports_Delete_Confirmation_Message,
            Strings.Reports_Delete_Confirm_Button,
            Strings.Reports_Delete_Cancel_Button));
    }

    // ── Sync removed from Reports page ───────────────────────────────────────

    [Fact]
    public void ReportsViewModel_Constructor_DoesNotRequireSyncHandler()
    {
        var vm = CreateVm();

        vm.IsLoading.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
    }

    // ── T014/T016: Pagination (feature 029) ─────────────────────────────────

    private static IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>> MakePagedGetReports(
        int totalCount, int totalPages, IReadOnlyList<ReportRowDto>? rows = null)
    {
        var h = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        var rowList = rows ?? Array.Empty<ReportRowDto>();
        var page = new ReportsPageResult(rowList, totalCount, totalPages);
        h.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(page));
        return h;
    }

    [Fact]
    public void Pagination_InitialState_CurrentPage1HasPreviousPageFalse()
    {
        // 3 pages of data
        var vm = CreateVm(getReports: MakePagedGetReports(75, 3, [MakeDto()]));
        using var _activation = vm.Activator.Activate();

        vm.CurrentPage.Should().Be(1);
        vm.TotalPages.Should().Be(3);
        vm.HasPreviousPage.Should().BeFalse();
        vm.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void Pagination_PageIndicator_FormatsCorrectly()
    {
        var vm = CreateVm(getReports: MakePagedGetReports(75, 3, [MakeDto()]));
        using var _activation = vm.Activator.Activate();

        vm.PageIndicator.Should().Be("Page 1 of 3");
    }

    [Fact]
    public void Pagination_PageIndicatorInSerbian_UsesTranslatedFormat()
    {
        var vm = CreateVm(getReports: MakePagedGetReports(75, 3, [MakeDto()]), localization: Serbian());
        using var _activation = vm.Activator.Activate();

        vm.PageIndicator.Should().Be("Strana 1 od 3");
    }

    [Fact]
    public void StatusFilterLabels_InSerbian_AreTranslated()
    {
        var vm = CreateVm(localization: Serbian());

        vm.StatusFilter.WorkingItems.Select(i => i.Label)
            .Should().Contain(SrLatnStrings.All["ReportStatus_PartialError"]);
    }

    [Fact]
    public async Task Pagination_NextPageCommand_IncrementsPageAndReloads()
    {
        var getReports = MakePagedGetReports(75, 3, [MakeDto()]);
        var vm = CreateVm(getReports: getReports);
        using var _activation = vm.Activator.Activate();

        await vm.NextPageCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.CurrentPage.Should().Be(2);
        // LoadPage: 1 activation + 1 next
        var __ = getReports.Received(2).HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Pagination_WhenAdditionalPagesExist_NextPageCommandCanExecute()
    {
        var vm = CreateVm(getReports: MakePagedGetReports(75, 3, [MakeDto()]));
        using var _activation = vm.Activator.Activate();

        bool? canExecute = null;
        using var subscription = vm.NextPageCommand.CanExecute.Subscribe(value => canExecute = value);

        canExecute.Should().BeTrue();
    }

    [Fact]
    public async Task Pagination_PreviousPageCommand_DecrementsPageAndReloads()
    {
        // Simulate being on page 2 by injecting a handler that always returns page 2 state
        var getReports = MakePagedGetReports(75, 3, [MakeDto()]);
        var vm = CreateVm(getReports: getReports);
        using var _activation = vm.Activator.Activate();

        // Navigate to page 2 first
        await vm.NextPageCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);
        vm.CurrentPage.Should().Be(2);

        // Now go back
        await vm.PreviousPageCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm.CurrentPage.Should().Be(1);
    }

    [Fact]
    public async Task Pagination_WhenOnSecondPage_PreviousPageCommandCanExecute()
    {
        var vm = CreateVm(getReports: MakePagedGetReports(75, 3, [MakeDto()]));
        using var _activation = vm.Activator.Activate();

        bool? canExecute = null;
        using var subscription = vm.PreviousPageCommand.CanExecute.Subscribe(value => canExecute = value);

        await vm.NextPageCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        canExecute.Should().BeTrue();
    }

    [Fact]
    public void Pagination_OnLastPage_HasNextPageIsFalse()
    {
        // Single page of data
        var vm = CreateVm(getReports: MakePagedGetReports(5, 1, [MakeDto()]));
        using var _activation = vm.Activator.Activate();

        vm.HasNextPage.Should().BeFalse();
        vm.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void Pagination_OnFirstPage_PreviousPageCommandDisabled()
    {
        var vm = CreateVm(getReports: MakePagedGetReports(75, 3, [MakeDto()]));
        using var _activation = vm.Activator.Activate();

        // PreviousPage should be disabled when on page 1
        vm.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task Pagination_DeleteLastItemOnNonFirstPage_DecrementsPage()
    {
        var deleteHandler = MakeDeleteHandler(success: true);

        // Page 2 contains exactly 1 item
        var pageFor2 = new ReportsPageResult([MakeDto()], 31, 2);
        var getReports = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        // First call (activation): returns 1-item page 2 result so we're "on page 2"
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(pageFor2));

        var vm = CreateVm(
            getReports: getReports,
            deleteHandler: deleteHandler,
            confirmation: DesktopTestDoubles.Confirmation(true));
        using var _activation = vm.Activator.Activate();

        // Manually put ViewModel on page 2
        await vm.NextPageCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);
        vm.CurrentPage.Should().Be(2);

        // Now the handler returns an empty page 1 result after delete
        var emptyPage = new ReportsPageResult([], 0, 1);
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(emptyPage));

        await vm.DeleteCommand.Execute(Guid.NewGuid()).FirstAsync(TestContext.Current.CancellationToken);

        vm.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task Pagination_BulkDeleteAllItemsOnNonFirstPage_DecrementsPage()
    {
        var dto = MakeDto();
        var pageFor2 = new ReportsPageResult([dto], 31, 2);
        var getReports = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(pageFor2));

        var bulkDelete = Substitute.For<ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>>();
        bulkDelete.HandleAsync(Arg.Any<BulkDeleteReportsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));

        var vm2 = CreateVm(
            getReports: getReports,
            confirmation: DesktopTestDoubles.Confirmation(true),
            bulkDelete: bulkDelete);

        using var activation = vm2.Activator.Activate();

        // Navigate to page 2
        await vm2.NextPageCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);
        vm2.CurrentPage.Should().Be(2);

        // Select the row and bulk-delete
        vm2.Rows.First().IsSelected = true;

        var emptyPage = new ReportsPageResult([], 0, 1);
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(emptyPage));

        await vm2.BulkDeleteCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);

        vm2.TotalPages.Should().Be(1);
    }

    // ── T016: Page reset on sort change ──────────────────────────────────────

    [Fact]
    public async Task SortDescending_WhenChanged_ResetsPageTo1AndReloads()
    {
        var getReports = MakePagedGetReports(75, 3, [MakeDto()]);
        var vm = CreateVm(getReports: getReports);
        using var _activation = vm.Activator.Activate();

        // Navigate to page 2
        await vm.NextPageCommand.Execute().FirstAsync(TestContext.Current.CancellationToken);
        vm.CurrentPage.Should().Be(2);

        // Change sort — should reset to page 1
        vm.SortDescending = false;

        vm.CurrentPage.Should().Be(1);
        // activation + next + sort change
        var __ = getReports.Received(3).HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>());
    }

    // ── State re-published when loading ends ─────────────────────────────────

    [Fact]
    public void IsEmpty_LoadReturnsNoRows_NotifiesTrueWhenLoadingEnds()
    {
        var vm = CreateVm(getReports: MakeGetReports());
        var notified = new List<bool>();
        using var subscription = vm.WhenAnyValue(x => x.IsEmpty).Subscribe(notified.Add);

        using var _ = vm.Activator.Activate();

        notified.Should().EndWith(true);
    }
}
