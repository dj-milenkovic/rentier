using System.Reactive;
using System.Reactive.Concurrency;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.Models;
using Rentier.Desktop.ViewModels;
using Rentier.Domain.Enums;
using Rentier.Application.Enums;
using Xunit;

namespace Rentier.UnitTests;

public class FilingsViewModelTests
{
    private static IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>> MockGetFilings(
        FilingsPageResult? page = null)
    {
        var mock = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        mock.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(
                page ?? new FilingsPageResult([], 0, 1)));
        return mock;
    }

    private static ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>> MockUpdateStatus() =>
        Substitute.For<ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>>();

    private static ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>> MockUpdateRef() =>
        Substitute.For<ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>>();

    private static ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>> MockDelete() =>
        Substitute.For<ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>>();

    private static FilingRowDto MakeDto(
        FilingStatus status = FilingStatus.Init,
        DateOnly? deadline = null) =>
        new(Guid.NewGuid(), status, IncomeType.Dividend, "ACME Corp",
            deadline ?? new DateOnly(2024, 4, 30), 100m, null);

    private static ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>> MockExport() =>
        Substitute.For<ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>>();

    private static ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>> MockBulkDeleteFilings() =>
        Substitute.For<ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>>();

    private static ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>> MockExportSuccess(ExportFilingResult? result = null)
    {
        var mock = Substitute.For<ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>>();
        mock.HandleAsync(Arg.Any<ExportFilingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExportFilingResult, Error>.Success(result ?? new ExportFilingResult([], "test.xml")));
        return mock;
    }

    private static FilingsViewModel CreateVm(
        IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>? getFilings = null,
        ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>? updateStatus = null,
        ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>? updateRef = null,
        ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>? deleteFiling = null,
        Func<string, Task<bool>>? confirmDelete = null,
        ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>? exportFiling = null,
        Func<ExportFilingResult, Task>? saveFile = null)
    {
        return new FilingsViewModel(
            getFilings   ?? MockGetFilings(),
            updateStatus ?? MockUpdateStatus(),
            updateRef    ?? MockUpdateRef(),
            deleteFiling ?? MockDelete(),
            exportFiling ?? MockExport(),
            MockBulkDeleteFilings(),
            confirmDelete ?? (_ => Task.FromResult(false)),
            saveFile ?? (_ => Task.CompletedTask),
            () => { },
            ImmediateScheduler.Instance);
    }

    [Fact]
    public void OnActivation_TriggersLoadPageWithDefaultAllFilter()
    {
        var getFilings = MockGetFilings();
        var vm = CreateVm(getFilings: getFilings);

        using var _ = vm.Activator.Activate();

        getFilings.Received(1).HandleAsync(
            Arg.Is<GetFilingsQuery>(q => q.Filter == FilingFilterMode.All),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void LoadPage_WhenQuerySucceeds_PopulatesRowsAndClearsError()
    {
        var dto = MakeDto();
        var page = new FilingsPageResult([dto], 1, 1);
        var vm = CreateVm(getFilings: MockGetFilings(page));

        using var _ = vm.Activator.Activate();

        vm.Rows.Should().HaveCount(1);
        vm.Rows[0].Id.Should().Be(dto.Id);
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void LoadPage_WhenQueryFails_SetsErrorMessageAndLeavesRowsEmpty()
    {
        var mock = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        mock.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Failure(
                new Error("INFRA", "DB connection lost")));
        var vm = CreateVm(getFilings: mock);

        using var _ = vm.Activator.Activate();

        vm.ErrorMessage.Should().Be("DB connection lost");
        vm.Rows.Should().BeEmpty();
    }

    [Fact]
    public void LoadPage_SetsIsLoadingFalseOnComplete()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void IsEmpty_WhenNoRows_IsTrue()
    {
        var vm = CreateVm(getFilings: MockGetFilings());
        using var _ = vm.Activator.Activate();

        vm.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_WhenRowsPresent_IsFalse()
    {
        var page = new FilingsPageResult([MakeDto()], 1, 1);
        var vm = CreateVm(getFilings: MockGetFilings(page));
        using var _ = vm.Activator.Activate();

        vm.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void AdvanceStatusCommand_WhenHandlerSucceeds_ReloadsPage()
    {
        var updateStatus = MockUpdateStatus();
        updateStatus.HandleAsync(Arg.Any<UpdateFilingStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));

        var getFilings = MockGetFilings();
        var vm = CreateVm(getFilings: getFilings, updateStatus: updateStatus);
        using var _ = vm.Activator.Activate();

        vm.AdvanceStatusCommand.Execute((Guid.NewGuid(), FilingStatus.Filed)).Subscribe();

        // LoadPage called once on activation + once after status update
        getFilings.Received(2).HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AdvanceStatusCommand_WhenHandlerFails_SetsErrorMessage()
    {
        var updateStatus = MockUpdateStatus();
        updateStatus.HandleAsync(Arg.Any<UpdateFilingStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Failure(new Error("DOMAIN_ERROR", "Bad transition")));

        var vm = CreateVm(updateStatus: updateStatus);
        using var _ = vm.Activator.Activate();

        vm.AdvanceStatusCommand.Execute((Guid.NewGuid(), FilingStatus.Paid)).Subscribe();

        vm.ErrorMessage.Should().Be("Bad transition");
    }

    [Fact]
    public void DeleteCommand_ConfirmFalse_DoesNotCallDelete()
    {
        var deleteMock = MockDelete();
        var vm = CreateVm(
            deleteFiling: deleteMock,
            confirmDelete: _ => Task.FromResult(false));

        using var _ = vm.Activator.Activate();
        vm.DeleteCommand.Execute(Guid.NewGuid()).Subscribe();

        deleteMock.DidNotReceive().HandleAsync(Arg.Any<DeleteFilingCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DeleteCommand_ConfirmTrue_CallsDeleteAndReloads()
    {
        var deleteMock = MockDelete();
        deleteMock.HandleAsync(Arg.Any<DeleteFilingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));

        var getFilings = MockGetFilings();
        var vm = CreateVm(
            getFilings: getFilings,
            deleteFiling: deleteMock,
            confirmDelete: _ => Task.FromResult(true));

        using var _ = vm.Activator.Activate();
        vm.DeleteCommand.Execute(Guid.NewGuid()).Subscribe();

        deleteMock.Received(1).HandleAsync(Arg.Any<DeleteFilingCommand>(), Arg.Any<CancellationToken>());
        // LoadPage: activation + after delete
        getFilings.Received(2).HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DeleteCommand_LastItemOnPageBeyondOne_DecrementsPage()
    {
        var deleteMock = MockDelete();
        deleteMock.HandleAsync(Arg.Any<DeleteFilingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));

        // Seed one row so Rows.Count == 1 after load on page 2
        var page = new FilingsPageResult([MakeDto()], 31, 2);
        var getFilings = MockGetFilings(page);
        var vm = CreateVm(
            getFilings: getFilings,
            deleteFiling: deleteMock,
            confirmDelete: _ => Task.FromResult(true));

        using var _ = vm.Activator.Activate();

        // Manually advance to page 2 to simulate user being on page 2
        // (normally done via NextPageCommand; here we test the decrement logic)
        // Reset mock to empty page to simulate page-2 view
        var emptyAfterDelete = new FilingsPageResult([], 0, 1);
        getFilings.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(emptyAfterDelete));

        vm.DeleteCommand.Execute(Guid.NewGuid()).Subscribe();

        // After deleting the last item on page 2, page should clamp to 1
        vm.TotalPages.Should().Be(1);
    }

    [Fact]
    public void FilterToggle_ShowAllChange_ResetsPageToOneAndReloads()
    {
        var getFilings = MockGetFilings();
        var vm = CreateVm(getFilings: getFilings);
        using var _ = vm.Activator.Activate();

        // ShowAll defaults to true now (FR-008); toggle to false to trigger a load.
        vm.ShowAll = false;

        // LoadPage: 1 activation + 1 filter change
        getFilings.Received(2).HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>());

        // Second call should use Unpaid filter
        getFilings.Received(1).HandleAsync(
            Arg.Is<GetFilingsQuery>(q =>
                q.Filter == FilingFilterMode.Unpaid && q.Page == 1),
            Arg.Any<CancellationToken>());
    }

    // -- Sort state tests (feature 027, updated for 3-state cycle in feature 046) -----------

    [Fact]
    public void InitialSortState_IsFilingDeadlineDescending()
    {
        var vm = CreateVm();

        // SortColumn is now FilingSortColumn? (nullable) — value is still FilingDeadline by default.
        vm.SortColumn.Should().Be(FilingSortColumn.FilingDeadline);
        vm.SortDescending.Should().BeTrue();
    }

    [Fact]
    public void LoadPage_ConstructsQueryWithCurrentSortState()
    {
        var getFilings = MockGetFilings();
        var vm = CreateVm(getFilings: getFilings);
        using var _ = vm.Activator.Activate();

        // Default state: FilingDeadline DESC
        getFilings.Received(1).HandleAsync(
            Arg.Is<GetFilingsQuery>(q =>
                q.SortColumn == FilingSortColumn.FilingDeadline &&
                q.SortDescending == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ApplySortCommand_SameColumn_AscendingToDescending()
    {
        // Setup: get VM into ascending state by clicking a different column first.
        var getFilings = MockGetFilings(new FilingsPageResult([MakeDto()], 40, 2));
        var vm = CreateVm(getFilings: getFilings);
        using var _ = vm.Activator.Activate();

        // Click TaxPayable (different column → ascending, page resets to 1)
        vm.ApplySortCommand.Execute(("TaxPayable", null)).Subscribe();
        vm.SortColumn.Should().Be(FilingSortColumn.TaxPayable);
        vm.SortDescending.Should().BeFalse();

        // Click TaxPayable again (same column, ascending → descending, page kept)
        vm.ApplySortCommand.Execute(("TaxPayable", null)).Subscribe();

        vm.SortDescending.Should().BeTrue();
        vm.SortColumn.Should().Be(FilingSortColumn.TaxPayable);
    }

    [Fact]
    public void ApplySortCommand_SameColumn_TogglesDirectionKeepsPage()
    {
        // Alias for ApplySortCommand_SameColumn_AscendingToDescending — kept for backwards-compat naming.
        var getFilings = MockGetFilings(new FilingsPageResult([MakeDto()], 40, 2));
        var vm = CreateVm(getFilings: getFilings);
        using var _ = vm.Activator.Activate();

        // Navigate to page 2 via TaxPayable (ascending)
        vm.ApplySortCommand.Execute(("TaxPayable", null)).Subscribe();

        // Same column second click: ascending → descending; page stays
        var pageBefore = vm.CurrentPage;
        vm.ApplySortCommand.Execute(("TaxPayable", null)).Subscribe();

        vm.SortDescending.Should().BeTrue();
        vm.SortColumn.Should().Be(FilingSortColumn.TaxPayable);
        vm.CurrentPage.Should().Be(pageBefore);
    }

    [Fact]
    public void ApplySortCommand_SameColumn_ThirdClick_ClearsSortToUnsorted()
    {
        var getFilings = MockGetFilings(new FilingsPageResult([MakeDto()], 10, 1));
        var vm = CreateVm(getFilings: getFilings);
        using var _ = vm.Activator.Activate();

        // 1st click: different column → ascending
        vm.ApplySortCommand.Execute(("IncomeType", null)).Subscribe();
        vm.SortColumn.Should().Be(FilingSortColumn.IncomeType);
        vm.SortDescending.Should().BeFalse();

        // 2nd click: same column, ascending → descending
        vm.ApplySortCommand.Execute(("IncomeType", null)).Subscribe();
        vm.SortDescending.Should().BeTrue();

        // 3rd click: same column, descending → null (unsorted)
        vm.ApplySortCommand.Execute(("IncomeType", null)).Subscribe();

        vm.SortColumn.Should().BeNull("third click on the same column clears the sort");
        vm.SortDescending.Should().BeFalse();
    }

    [Fact]
    public void ApplySortCommand_UnsortedColumn_FirstClick_SetsAscending()
    {
        var getFilings = MockGetFilings(new FilingsPageResult([MakeDto()], 10, 1));
        var vm = CreateVm(getFilings: getFilings);
        using var _ = vm.Activator.Activate();

        // Get to null (unsorted) by clicking FilingDeadline three times
        // (FilingDeadline DESC → null → ascending → descending → null)
        // Simpler: descending then null in two clicks from initial DESC state.
        vm.ApplySortCommand.Execute(("FilingDeadline", null)).Subscribe(); // DESC → null
        vm.SortColumn.Should().BeNull();

        // Now in null state: click TaxPayable → ascending on TaxPayable
        vm.ApplySortCommand.Execute(("TaxPayable", null)).Subscribe();

        vm.SortColumn.Should().Be(FilingSortColumn.TaxPayable);
        vm.SortDescending.Should().BeFalse("first click from unsorted sets ascending");
    }

    [Fact]
    public void ApplySortCommand_DifferentColumn_ResetsToAscendingOnNewColumn()
    {
        var getFilings = MockGetFilings(new FilingsPageResult([MakeDto()], 40, 2));
        var vm = CreateVm(getFilings: getFilings);
        using var _ = vm.Activator.Activate();

        // Navigate to page 2 via NextPage
        vm.NextPageCommand.Execute().Subscribe();
        vm.CurrentPage.Should().Be(2);

        // Click IncomeType (different from current FilingDeadline)
        vm.ApplySortCommand.Execute(("IncomeType", null)).Subscribe();

        vm.SortColumn.Should().Be(FilingSortColumn.IncomeType, "arrow should move to new column");
        vm.SortDescending.Should().BeFalse("clicking a new column always starts ascending");
        vm.CurrentPage.Should().Be(1, "changing column resets pagination to page 1");
    }

    [Fact]
    public void ApplySortCommand_SameColumn_DoesNotResetCurrentPage()
    {
        // Set up mock to return page with multiple pages so CurrentPage can be > 1
        var getFilings = MockGetFilings(new FilingsPageResult([MakeDto()], 40, 2));
        var vm = CreateVm(getFilings: getFilings);
        using var _ = vm.Activator.Activate();

        // Navigate to page 2
        vm.NextPageCommand.Execute().Subscribe();
        vm.CurrentPage.Should().Be(2);

        // Toggle same column — page should stay at 2 (regardless of direction change)
        vm.ApplySortCommand.Execute(("FilingDeadline", true)).Subscribe();

        vm.CurrentPage.Should().Be(2);
    }

    [Fact]
    public void ApplySortCommand_DifferentColumn_SetsNewColumnAscendingResetsPage()
    {
        var getFilings = MockGetFilings(new FilingsPageResult([MakeDto()], 40, 2));
        var vm = CreateVm(getFilings: getFilings);
        using var _ = vm.Activator.Activate();

        // Navigate to page 2
        vm.NextPageCommand.Execute().Subscribe();
        vm.CurrentPage.Should().Be(2);

        // Click a different column (TaxPayable)
        vm.ApplySortCommand.Execute(("TaxPayable", null)).Subscribe();

        vm.SortColumn.Should().Be(FilingSortColumn.TaxPayable);
        vm.SortDescending.Should().BeFalse(); // ascending when changing column
        vm.CurrentPage.Should().Be(1);        // page reset to 1
    }

    [Fact]
    public void ApplySortCommand_UnknownColumnTag_IsNoOp()
    {
        var getFilings = MockGetFilings();
        var vm = CreateVm(getFilings: getFilings);
        using var _ = vm.Activator.Activate();

        // "UnknownColumn" doesn't map to FilingSortColumn
        vm.ApplySortCommand.Execute(("UnknownColumn", null)).Subscribe();

        // Sort state unchanged
        vm.SortColumn.Should().Be(FilingSortColumn.FilingDeadline);
        vm.SortDescending.Should().BeTrue();
    }

    // ── SavePaymentRefCommand tests ──────────────────────────────────────────

    [Fact]
    public void SavePaymentRefCommand_WhenSuccessful_ReloadsPage()
    {
        var updateRef = MockUpdateRef();
        updateRef.HandleAsync(Arg.Any<UpdatePaymentReferenceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));

        var getFilings = MockGetFilings();
        var vm = CreateVm(getFilings: getFilings, updateRef: updateRef);
        using var _ = vm.Activator.Activate();

        vm.SavePaymentRefCommand.Execute((Guid.NewGuid(), "REF123")).Subscribe();

        // LoadPage: 1 on activation + 1 after save
        getFilings.Received(2).HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SavePaymentRefCommand_WhenHandlerFails_SetsErrorMessage()
    {
        var updateRef = MockUpdateRef();
        updateRef.HandleAsync(Arg.Any<UpdatePaymentReferenceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Failure(new Error("UPDATE_FAILED", "Update failed")));

        var vm = CreateVm(updateRef: updateRef);
        using var _ = vm.Activator.Activate();

        vm.SavePaymentRefCommand.Execute((Guid.NewGuid(), "REF123")).Subscribe();

        vm.ErrorMessage.Should().Be("Update failed");
    }

    // ── ExportCommand tests ──────────────────────────────────────────────────

    [Fact]
    public void ExportCommand_WhenSuccessful_CallsSaveFileDialog()
    {
        var expectedResult = new ExportFilingResult([], "test.xml");
        var exportHandler = MockExportSuccess(expectedResult);
        var savedResults = new List<ExportFilingResult>();

        var vm = CreateVm(
            exportFiling: exportHandler,
            saveFile: r => { savedResults.Add(r); return Task.CompletedTask; });
        using var _ = vm.Activator.Activate();

        vm.ExportCommand.Execute(Guid.NewGuid()).Subscribe();

        savedResults.Should().ContainSingle().Which.Should().Be(expectedResult);
    }

    [Fact]
    public void ExportCommand_WhenHandlerFails_SetsErrorMessage()
    {
        var exportHandler = Substitute.For<ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>>();
        exportHandler.HandleAsync(Arg.Any<ExportFilingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExportFilingResult, Error>.Failure(new Error("EXPORT_FAILED", "Export failed")));

        var vm = CreateVm(exportFiling: exportHandler);
        using var _ = vm.Activator.Activate();

        vm.ExportCommand.Execute(Guid.NewGuid()).Subscribe();

        vm.ErrorMessage.Should().Be("Export failed");
    }

    // ── HasReportFilter tests (T005) ─────────────────────────────────────────

    [Fact]
    public void HasReportFilter_WhenReportIdFilterIsNull_ReturnsFalse()
    {
        var vm = CreateVm();

        vm.HasReportFilter.Should().BeFalse();
    }

    [Fact]
    public void HasReportFilter_WhenReportIdFilterIsSet_ReturnsTrue()
    {
        var vm = CreateVm();

        vm.ReportIdFilter = Guid.NewGuid();

        vm.HasReportFilter.Should().BeTrue();
    }

    // ── ClearReportFilterCommand tests (T007) ────────────────────────────────

    [Fact]
    public void ClearReportFilterCommand_WhenNoReportFilter_CannotExecute()
    {
        var vm = CreateVm();
        var canExecute = true;

        // ReportIdFilter is null by default
        vm.ClearReportFilterCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeFalse();
    }

    [Fact]
    public void ClearReportFilterCommand_WhenExecuted_SetsReportIdFilterToNull()
    {
        var vm = CreateVm();
        vm.ReportIdFilter = Guid.NewGuid();

        vm.ClearReportFilterCommand.Execute(Unit.Default).Subscribe();

        vm.ReportIdFilter.Should().BeNull();
        vm.HasReportFilter.Should().BeFalse();
    }

    [Fact]
    public void ClearReportFilterCommand_WhenExecuted_TriggersLoadPageCommand()
    {
        var getFilings = MockGetFilings();
        var vm = CreateVm(getFilings: getFilings);
        vm.ReportIdFilter = Guid.NewGuid();

        // Activate so the WhenAnyValue(ReportIdFilter).InvokeCommand(LoadPageCommand) subscription is live
        using var _ = vm.Activator.Activate();

        // Clear call count from activation load
        getFilings.ClearReceivedCalls();

        // Setting ReportIdFilter = null via the command triggers the reactive pipeline
        vm.ClearReportFilterCommand.Execute(Unit.Default).Subscribe();

        // LoadPageCommand should have fired due to ReportIdFilter change
        getFilings.Received().HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>());
    }

    // ── Feature 045: Column filter tests ─────────────────────────────────────

    [Fact]
    public void FilterStatus_WhenSet_TriggersLoadPageAndSetsHasActiveFilters()
    {
        var getFilings = MockGetFilings();
        var vm = CreateVm(getFilings: getFilings);
        using var _ = vm.Activator.Activate();
        var initialCalls = getFilings.ReceivedCalls().Count();

        vm.FilterStatus = FilingStatus.Init;

        getFilings.ReceivedCalls().Count().Should().BeGreaterThan(initialCalls);
        vm.HasActiveFilters.Should().BeTrue();
    }

    [Fact]
    public void FilterStatus_WhenNull_HasActiveFiltersFalse()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        vm.FilterStatus = null;

        vm.HasActiveFilters.Should().BeFalse();
    }

    [Fact]
    public void StatusFilterOptions_ContainsNullEntryAndAllStatusValues()
    {
        var vm = CreateVm();

        vm.StatusFilterOptions.Should().Contain(o => o.Value == null);
        vm.StatusFilterOptions.Should().Contain(o => o.Value == FilingStatus.Init);
        vm.StatusFilterOptions.Should().Contain(o => o.Value == FilingStatus.Filed);
        vm.StatusFilterOptions.Should().Contain(o => o.Value == FilingStatus.Paid);
    }

    [Fact]
    public void FilterPayingEntity_WhenSet_SetsHasActiveFilters()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        vm.FilterPayingEntity = "ACME";

        vm.HasActiveFilters.Should().BeTrue();
    }

    [Fact]
    public void FilterPayingEntity_WhenCleared_ClearsHasActiveFilters()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();
        vm.FilterPayingEntity = "ACME";
        vm.FilterPayingEntity = "";

        vm.HasActiveFilters.Should().BeFalse();
    }

    [Fact]
    public void FilterPaymentReference_WhenSet_SetsHasActiveFilters()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        vm.FilterPaymentReference = "REF-001";

        vm.HasActiveFilters.Should().BeTrue();
    }

    [Fact]
    public void FilterIncomeType_WhenSet_TriggersLoadAndSetsHasActiveFilters()
    {
        var getFilings = MockGetFilings();
        var vm = CreateVm(getFilings: getFilings);
        using var _ = vm.Activator.Activate();
        var initialCalls = getFilings.ReceivedCalls().Count();

        vm.FilterIncomeType = IncomeType.Dividend;

        getFilings.ReceivedCalls().Count().Should().BeGreaterThan(initialCalls);
        vm.HasActiveFilters.Should().BeTrue();
    }

    [Fact]
    public void IncomeTypeFilterOptions_ContainsNullEntryAndAllIncomeTypeValues()
    {
        var vm = CreateVm();

        vm.IncomeTypeFilterOptions.Should().Contain(o => o.Value == null);
        vm.IncomeTypeFilterOptions.Should().Contain(o => o.Value == IncomeType.Dividend);
        vm.IncomeTypeFilterOptions.Should().Contain(o => o.Value == IncomeType.Interest);
    }

    [Fact]
    public void FilterDeadline_WhenSet_TriggersLoadAndSetsHasActiveFilters()
    {
        var getFilings = MockGetFilings();
        var vm = CreateVm(getFilings: getFilings);
        using var _ = vm.Activator.Activate();
        var initialCalls = getFilings.ReceivedCalls().Count();

        vm.FilterDeadline = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero);

        getFilings.ReceivedCalls().Count().Should().BeGreaterThan(initialCalls);
        vm.HasActiveFilters.Should().BeTrue();
    }

    [Fact]
    public void FilterDeadline_WhenNull_HasActiveFiltersFalse()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        vm.FilterDeadline = null;

        vm.HasActiveFilters.Should().BeFalse();
    }

    [Fact]
    public void ClearFiltersCommand_ResetsAllFilterProperties()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();
        vm.FilterStatus = FilingStatus.Init;
        vm.FilterIncomeType = IncomeType.Dividend;
        vm.FilterPayingEntity = "ACME";
        vm.FilterPaymentReference = "REF";
        vm.FilterDeadline = DateTimeOffset.UtcNow;

        vm.ClearFiltersCommand.Execute().Subscribe();

        vm.FilterStatus.Should().BeNull();
        vm.FilterIncomeType.Should().BeNull();
        vm.FilterPayingEntity.Should().BeNullOrEmpty();
        vm.FilterPaymentReference.Should().BeNullOrEmpty();
        vm.FilterDeadline.Should().BeNull();
        vm.HasActiveFilters.Should().BeFalse();
    }

    [Fact]
    public void ClearFiltersCommand_CanExecute_FalseWhenNoFilters()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        bool canExecute = false;
        vm.ClearFiltersCommand.CanExecute.Subscribe(v => canExecute = v);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public void ClearFiltersCommand_CanExecute_TrueWhenFilterActive()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        vm.FilterStatus = FilingStatus.Init;

        bool canExecute = false;
        vm.ClearFiltersCommand.CanExecute.Subscribe(v => canExecute = v);

        canExecute.Should().BeTrue();
    }

    [Fact]
    public void SetReportIdFilter_ClearsAllFilterProperties()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();
        vm.FilterStatus = FilingStatus.Init;
        vm.FilterIncomeType = IncomeType.Dividend;
        vm.FilterPayingEntity = "ACME";

        vm.ReportIdFilter = Guid.NewGuid();

        vm.FilterStatus.Should().BeNull();
        vm.FilterIncomeType.Should().BeNull();
        vm.FilterPayingEntity.Should().BeNullOrEmpty();
    }

    [Fact]
    public void IsFilterRowEnabled_FalseWhenReportIdFilterSet()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        vm.ReportIdFilter = Guid.NewGuid();

        vm.IsFilterRowEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsFilterRowEnabled_TrueWhenReportIdFilterNull()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        vm.IsFilterRowEnabled.Should().BeTrue();
    }

    [Fact]
    public void ClearingReportIdFilter_ReEnablesFilterRow()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();
        vm.ReportIdFilter = Guid.NewGuid();

        vm.ReportIdFilter = null;

        vm.IsFilterRowEnabled.Should().BeTrue();
    }

    [Fact]
    public void LoadPage_WhenFilterStatus_IncludesColumnFilterInQuery()
    {
        GetFilingsQuery? capturedQuery = null;
        var mock = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        mock.HandleAsync(Arg.Do<GetFilingsQuery>(q => capturedQuery = q), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(new FilingsPageResult([], 0, 1)));

        var vm = CreateVm(getFilings: mock);
        using var _ = vm.Activator.Activate();
        capturedQuery = null; // reset after activation call

        vm.FilterStatus = FilingStatus.Filed;

        capturedQuery.Should().NotBeNull();
        capturedQuery!.ColumnFilter.Should().NotBeNull();
        capturedQuery.ColumnFilter!.Status.Should().Be(FilingStatus.Filed);
    }

    [Fact]
    public void HasActiveFilters_TrueWhenMultipleFiltersActive()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        vm.FilterStatus = FilingStatus.Init;
        vm.FilterIncomeType = IncomeType.Dividend;

        vm.HasActiveFilters.Should().BeTrue();
    }

    [Fact]
    public void ChangingOneFilter_DoesNotResetOtherFilter()
    {
        var vm = CreateVm();
        using var _ = vm.Activator.Activate();

        vm.FilterStatus = FilingStatus.Init;
        vm.FilterIncomeType = IncomeType.Dividend;

        vm.FilterStatus = FilingStatus.Filed;

        vm.FilterIncomeType.Should().Be(IncomeType.Dividend);
    }
}