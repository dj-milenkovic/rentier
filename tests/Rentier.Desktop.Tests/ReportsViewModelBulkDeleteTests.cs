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

namespace Rentier.Desktop.Tests;

public class ReportsViewModelBulkDeleteTests
{
    private static ReportRowDto MakeDto(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "report.csv", new DateOnly(2024, 3, 1), null, "Importer",
            ReportStatus.Processed, 2, "Importer – 2024-03-01", null);

    private static IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>> MockGetReports(
        params ReportRowDto[] rows)
    {
        var mock = Substitute.For<IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>>>();
        mock.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ReportRowDto>, Error>.Success(
                (IReadOnlyList<ReportRowDto>)rows));
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
        IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>>? getReports = null,
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
}

