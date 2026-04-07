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

public class ReportsViewModelTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>> MakeSyncHandler(
        SyncResult? result = null)
    {
        var h = Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>();
        h.HandleAsync(Arg.Any<SyncMailboxCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SyncResult, Error>.Success(result ?? new SyncResult(0, [])));
        return h;
    }

    private static IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>> MakeGetReports(
        IReadOnlyList<ReportRowDto>? rows = null)
    {
        var h = Substitute.For<IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>>>();
        h.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ReportRowDto>, Error>.Success(
                rows ?? Array.Empty<ReportRowDto>()));
        return h;
    }

    private static ICommandHandler<ImportReportCommand, Result<Guid, Error>> MakeImportHandler(
        bool success = true)
    {
        var h = Substitute.For<ICommandHandler<ImportReportCommand, Result<Guid, Error>>>();
        h.HandleAsync(Arg.Any<ImportReportCommand>(), Arg.Any<CancellationToken>())
            .Returns(success
                ? Result<Guid, Error>.Success(Guid.NewGuid())
                : Result<Guid, Error>.Failure(new Error("IMPORT_FAILED", "Import error")));
        return h;
    }

    private static ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>> MakeDeleteHandler(
        bool success = true)
    {
        var h = Substitute.For<ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>>();
        h.HandleAsync(Arg.Any<DeleteReportCommand>(), Arg.Any<CancellationToken>())
            .Returns(success
                ? Result<VoidResult, Error>.Success(VoidResult.Value)
                : Result<VoidResult, Error>.Failure(new Error("DELETE_REPORT_FAILED", "Delete error")));
        return h;
    }

    private static ReportRowDto MakeDto(string name = "report.csv") =>
        new(Guid.NewGuid(), name, new DateOnly(2024, 3, 1), "My Importer",
            ReportStatus.Processed, 3);

    private static ReportsViewModel CreateVm(
        ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>? syncHandler = null,
        IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>>? getReports = null,
        ICommandHandler<ImportReportCommand, Result<Guid, Error>>? importHandler = null,
        ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>? deleteHandler = null,
        Func<string, string, Task<bool>>? confirmDelete = null,
        Func<Task<(Guid ImporterId, string FileName, byte[] Content)?>>? showImportDialog = null,
        Action<Guid>? navigateToFilings = null)
    {
        return new ReportsViewModel(
            syncHandler      ?? MakeSyncHandler(),
            getReports       ?? MakeGetReports(),
            importHandler    ?? MakeImportHandler(),
            deleteHandler    ?? MakeDeleteHandler(),
            confirmDelete    ?? ((_, _) => Task.FromResult(false)),
            showImportDialog ?? (() => Task.FromResult<(Guid, string, byte[])?>(null)),
            navigateToFilings ?? (_ => { }),
            ImmediateScheduler.Instance);
    }

    // ── US1: Browse reports list ─────────────────────────────────────────────

    [Fact]
    public void OnActivation_TriggersLoadReportsCommand()
    {
        var getReports = MakeGetReports();
        var vm = CreateVm(getReports: getReports);

        using var _ = vm.Activator.Activate();

        getReports.Received(1).HandleAsync(
            Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void LoadReports_WhenQuerySucceeds_PopulatesRowsAndClearsError()
    {
        var dto = MakeDto();
        var vm = CreateVm(getReports: MakeGetReports([dto]));

        using var _ = vm.Activator.Activate();

        vm.Rows.Should().HaveCount(1);
        vm.Rows[0].ReportName.Should().Be(dto.ReportName);
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void LoadReports_WhenQueryFails_SetsErrorMessageAndLeavesRowsEmpty()
    {
        var failingHandler = Substitute.For<IQueryHandler<GetReportsQuery, Result<IReadOnlyList<ReportRowDto>, Error>>>();
        failingHandler.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ReportRowDto>, Error>.Failure(
                new Error("GET_REPORTS_FAILED", "DB error")));

        var vm = CreateVm(getReports: failingHandler);

        using var _ = vm.Activator.Activate();

        vm.ErrorMessage.Should().Be("DB error");
        vm.Rows.Should().BeEmpty();
    }

    [Fact]
    public void IsEmpty_WhenRowsEmpty_IsTrue()
    {
        var vm = CreateVm(getReports: MakeGetReports([]));

        using var _ = vm.Activator.Activate();

        vm.IsEmpty.Should().BeTrue();
    }

    // ── US2: Import ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportCommand_WhenDialogCancelled_DoesNotCallImportHandler()
    {
        var importHandler = MakeImportHandler();
        var vm = CreateVm(
            importHandler: importHandler,
            showImportDialog: () => Task.FromResult<(Guid, string, byte[])?>(null));

        await vm.ImportCommand.Execute().FirstAsync();

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
            showImportDialog: () => Task.FromResult<(Guid, string, byte[])?>(
                (importerId, "test.csv", [1, 2, 3])));

        await vm.ImportCommand.Execute().FirstAsync();

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
            showImportDialog: () => Task.FromResult<(Guid, string, byte[])?>(
                (importerId, "bad.csv", [1, 2, 3])));

        await vm.ImportCommand.Execute().FirstAsync();

        vm.ErrorMessage.Should().Be("Import error");
    }

    // ── US4: Delete ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCommand_WhenUserCancels_DoesNotCallDeleteHandler()
    {
        var deleteHandler = MakeDeleteHandler();
        var vm = CreateVm(
            deleteHandler: deleteHandler,
            confirmDelete: (_, _) => Task.FromResult(false));

        await vm.DeleteCommand.Execute(Guid.NewGuid()).FirstAsync();

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
            confirmDelete: (_, _) => Task.FromResult(true));

        await vm.DeleteCommand.Execute(reportId).FirstAsync();

        await deleteHandler.Received(1).HandleAsync(
            Arg.Is<DeleteReportCommand>(c => c.ReportId == reportId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCommand_WhenHandlerFails_SetsErrorMessage()
    {
        var vm = CreateVm(
            deleteHandler: MakeDeleteHandler(success: false),
            confirmDelete: (_, _) => Task.FromResult(true));

        await vm.DeleteCommand.Execute(Guid.NewGuid()).FirstAsync();

        vm.ErrorMessage.Should().Be("Delete error");
    }

    // ── US3: View Filings navigation ─────────────────────────────────────────

    [Fact]
    public async Task ViewFilingsCommand_InvokesNavigateToFilingsDelegate()
    {
        var reportId = Guid.NewGuid();
        Guid? navigatedId = null;
        var vm = CreateVm(navigateToFilings: id => navigatedId = id);

        await vm.ViewFilingsCommand.Execute(reportId).FirstAsync();

        navigatedId.Should().Be(reportId);
    }

    // ── Sync (preserved behaviour) ───────────────────────────────────────────

    [Fact]
    public async Task SyncCommand_WhenHandlerSucceeds_SetsSyncStatusMessage()
    {
        var vm = CreateVm(syncHandler: MakeSyncHandler(new SyncResult(3, [])));

        await vm.SyncCommand.Execute().FirstAsync();

        vm.SyncStatusMessage.Should().Contain("3");
    }

    [Fact]
    public async Task SyncCommand_WhenHandlerFails_SetsSyncStatusMessage()
    {
        var failingSync = Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>();
        failingSync.HandleAsync(Arg.Any<SyncMailboxCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SyncResult, Error>.Failure(Error.Infrastructure("IMAP timeout")));

        var vm = CreateVm(syncHandler: failingSync);

        await vm.SyncCommand.Execute().FirstAsync();

        vm.SyncStatusMessage.Should().Be("IMAP timeout");
    }
}

