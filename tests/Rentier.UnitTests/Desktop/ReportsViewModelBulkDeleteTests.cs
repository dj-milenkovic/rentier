using System.Reactive.Concurrency;
using System.Reactive.Linq;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.ViewModels;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests;

public class ReportsViewModelBulkDeleteTests
{
    private static ReportRowDto MakeDto(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "report.csv", new DateOnly(2024, 3, 1), null, "Importer",
            ReportStatus.Processed, 2, "Importer – 2024-03-01", null);

    private static IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>> MockGetReports(
        params ReportRowDto[] rows)
    {
        var mock = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        var page = new ReportsPageResult((IReadOnlyList<ReportRowDto>)rows, rows.Length, rows.Length > 0 ? 1 : 1);
        mock.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(page));
        return mock;
    }

    private static ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>> MockBulkDelete(
        bool success = true)
    {
        var mock = Substitute.For<ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>>();
        mock.HandleAsync(Arg.Any<BulkDeleteReportsCommand>(), Arg.Any<CancellationToken>())
            .Returns(success
                ? Result<VoidResult, Error>.Success(VoidResult.Value)
                : Result<VoidResult, Error>.Failure(new Error("BULK_DELETE_REPORTS_FAILED", "error")));
        return mock;
    }

    private static ReportsViewModel CreateVm(
        IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>? getReports = null,
        ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>? bulkDelete = null,
        Func<string, string, Task<bool>>? confirmDelete = null)
    {
        return new ReportsViewModel(
            Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>(),
            getReports ?? MockGetReports(),
            Substitute.For<ICommandHandler<ImportReportCommand, Result<Guid, Error>>>(),
            Substitute.For<ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>>(),
            bulkDelete ?? MockBulkDelete(),
            confirmDelete ?? ((_, _) => Task.FromResult(false)),
            () => Task.FromResult<(Guid, string, byte[])?>(null),
            _ => { },
            ImmediateScheduler.Instance);
    }

    [Fact]
    public void SelectedCount_WhenNoRowsSelected_IsZero()
    {
        var vm = CreateVm(getReports: MockGetReports(MakeDto()));
        using var _ = vm.Activator.Activate();
        vm.SelectedCount.Should().Be(0);
    }

    [Fact]
    public void SelectedCount_WhenRowSelected_UpdatesReactively()
    {
        var vm = CreateVm(getReports: MockGetReports(MakeDto()));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;

        vm.SelectedCount.Should().Be(1);
    }

    [Fact]
    public void HasSelection_IsTrueWhenRowIsSelected()
    {
        var vm = CreateVm(getReports: MockGetReports(MakeDto()));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;

        vm.HasSelection.Should().BeTrue();
    }

    [Fact]
    public void SelectAllCommand_SetsAllRowsSelected()
    {
        var vm = CreateVm(getReports: MockGetReports(MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        vm.SelectAllCommand.Execute().Subscribe();

        vm.Rows.Should().OnlyContain(r => r.IsSelected);
    }

    [Fact]
    public void ClearSelectionCommand_ClearsAll()
    {
        var vm = CreateVm(getReports: MockGetReports(MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;
        vm.Rows[1].IsSelected = true;
        vm.ClearSelectionCommand.Execute().Subscribe();

        vm.Rows.Should().OnlyContain(r => !r.IsSelected);
    }

    [Fact]
    public async Task BulkDeleteCommand_ConfirmMessageContainsCascadeWarning()
    {
        string? capturedMessage = null;
        var vm = CreateVm(
            getReports: MockGetReports(MakeDto()),
            confirmDelete: (_, msg) =>
            {
                capturedMessage = msg;
                return Task.FromResult(false);
            });
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;
        vm.BulkDeleteCommand.Execute().Subscribe();

        capturedMessage.Should().Contain("linked filings");
    }

    [Fact]
    public async Task BulkDeleteCommand_WhenCancelled_DoesNotCallHandler()
    {
        var bulkDelete = MockBulkDelete();
        var vm = CreateVm(
            getReports: MockGetReports(MakeDto()),
            bulkDelete: bulkDelete,
            confirmDelete: (_, _) => Task.FromResult(false));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;
        vm.BulkDeleteCommand.Execute().Subscribe();

        await bulkDelete.DidNotReceive()
            .HandleAsync(Arg.Any<BulkDeleteReportsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkDeleteCommand_WhenConfirmed_DispatchesCommandWithCorrectIds()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var bulkDelete = MockBulkDelete();
        var vm = CreateVm(
            getReports: MockGetReports(MakeDto(id1), MakeDto(id2)),
            bulkDelete: bulkDelete,
            confirmDelete: (_, _) => Task.FromResult(true));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;
        vm.Rows[1].IsSelected = true;
        vm.BulkDeleteCommand.Execute().Subscribe();

        await bulkDelete.Received(1).HandleAsync(
            Arg.Is<BulkDeleteReportsCommand>(c =>
                c.ReportIds.Count == 2 &&
                c.ReportIds.Contains(id1) &&
                c.ReportIds.Contains(id2)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BulkDeleteCommand_OnFailure_SetsErrorMessage()
    {
        var vm = CreateVm(
            getReports: MockGetReports(MakeDto()),
            bulkDelete: MockBulkDelete(success: false),
            confirmDelete: (_, _) => Task.FromResult(true));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;
        vm.BulkDeleteCommand.Execute().Subscribe();

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ── IsAllSelected tests (Feature 028) ──────────────────────────────────

    [Fact]
    public void IsAllSelected_WhenNoRowsSelected_ReturnsFalse()
    {
        var vm = CreateVm(getReports: MockGetReports(MakeDto(), MakeDto(), MakeDto(), MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        vm.IsAllSelected.Should().BeFalse();
    }

    [Fact]
    public void IsAllSelected_WhenAllRowsSelected_ReturnsTrue()
    {
        var vm = CreateVm(getReports: MockGetReports(MakeDto(), MakeDto(), MakeDto(), MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        foreach (var row in vm.Rows) row.IsSelected = true;

        vm.IsAllSelected.Should().BeTrue();
    }

    [Fact]
    public void IsAllSelected_WhenSomeRowsSelected_ReturnsNull()
    {
        var vm = CreateVm(getReports: MockGetReports(MakeDto(), MakeDto(), MakeDto(), MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;
        vm.Rows[1].IsSelected = true;

        vm.IsAllSelected.Should().BeNull();
    }

    [Fact]
    public void IsAllSelected_SetTrue_SelectsAllRows()
    {
        var vm = CreateVm(getReports: MockGetReports(MakeDto(), MakeDto(), MakeDto(), MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        vm.IsAllSelected = true;

        vm.SelectedCount.Should().Be(5);
        vm.IsAllSelected.Should().BeTrue();
    }

    [Fact]
    public void IsAllSelected_SetTrue_FromIndeterminate_SelectsAllRows()
    {
        var vm = CreateVm(getReports: MockGetReports(MakeDto(), MakeDto(), MakeDto(), MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;
        vm.Rows[1].IsSelected = true;

        vm.IsAllSelected = true;

        vm.SelectedCount.Should().Be(5);
        vm.IsAllSelected.Should().BeTrue();
    }

    [Fact]
    public void IsAllSelected_SetFalse_DeselectsAllRows()
    {
        var vm = CreateVm(getReports: MockGetReports(MakeDto(), MakeDto(), MakeDto(), MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        foreach (var row in vm.Rows) row.IsSelected = true;

        vm.IsAllSelected = false;

        vm.SelectedCount.Should().Be(0);
        vm.IsAllSelected.Should().BeFalse();
    }

    [Fact]
    public void IsAllSelected_UpdatesWhenRowSelectionChanges()
    {
        var vm = CreateVm(getReports: MockGetReports(MakeDto(), MakeDto(), MakeDto(), MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        // 0/5 selected → false
        vm.IsAllSelected.Should().BeFalse();

        // 2/5 selected → null (indeterminate)
        vm.Rows[0].IsSelected = true;
        vm.Rows[1].IsSelected = true;
        vm.IsAllSelected.Should().BeNull();

        // 5/5 selected → true
        vm.Rows[2].IsSelected = true;
        vm.Rows[3].IsSelected = true;
        vm.Rows[4].IsSelected = true;
        vm.IsAllSelected.Should().BeTrue();

        // back to 0/5 → false
        foreach (var row in vm.Rows) row.IsSelected = false;
        vm.IsAllSelected.Should().BeFalse();
    }

    [Fact]
    public void IsAllSelected_WhenNoRows_ReturnsFalse()
    {
        var vm = CreateVm(getReports: MockGetReports());
        using var _ = vm.Activator.Activate();

        var ex = Record.Exception(() => vm.IsAllSelected);
        ex.Should().BeNull();
        vm.IsAllSelected.Should().BeFalse();
    }

    [Fact]
    public void IsAllSelected_RecalculatesAfterRowsReloaded()
    {
        var getReports = MockGetReports(MakeDto(), MakeDto(), MakeDto());
        var vm = CreateVm(getReports: getReports);
        using var _ = vm.Activator.Activate();

        // Select all
        foreach (var row in vm.Rows) row.IsSelected = true;
        vm.IsAllSelected.Should().BeTrue();

        // Simulate reload with fresh unselected rows
        var freshPage = new ReportsPageResult(
            new[] { MakeDto(), MakeDto(), MakeDto() }, 3, 1);
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(freshPage));
        vm.LoadPageCommand.Execute().Subscribe();

        vm.SelectedCount.Should().Be(0);
        vm.IsAllSelected.Should().BeFalse();
    }
}

