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

public class FilingsViewModelBulkDeleteTests
{
    private static FilingRowDto MakeDto(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), FilingStatus.Init, IncomeType.Dividend, "ACME Corp",
            new DateOnly(2024, 4, 30), 100m, null);

    private static IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>> MockGetFilings(
        params FilingRowDto[] rows)
    {
        var mock = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        mock.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(
                new FilingsPageResult(rows, rows.Length, 1)));
        return mock;
    }

    private static ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>> MockBulkDelete(
        bool success = true)
    {
        var mock = Substitute.For<ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>>();
        mock.HandleAsync(Arg.Any<BulkDeleteFilingsCommand>(), Arg.Any<CancellationToken>())
            .Returns(success
                ? Result<VoidResult, Error>.Success(VoidResult.Value)
                : Result<VoidResult, Error>.Failure(new Error("FILING_BULK_DELETE_FAILED", "error")));
        return mock;
    }

    private static FilingsViewModel CreateVm(
        IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>? getFilings = null,
        ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>? bulkDelete = null,
        Func<string, Task<bool>>? confirmDelete = null)
    {
        return new FilingsViewModel(
            new FilingsHandlers(
                getFilings ?? MockGetFilings(),
                Substitute.For<ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>>(),
                Substitute.For<ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>>(),
                Substitute.For<ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>>(),
                Substitute.For<ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>>(),
                bulkDelete ?? MockBulkDelete()),
            confirmDelete ?? (_ => Task.FromResult(false)),
            _ => Task.CompletedTask,
            () => { },
            ImmediateScheduler.Instance);
    }

    [Fact]
    public void SelectedCount_WhenNoRowsSelected_IsZero()
    {
        var dto = MakeDto();
        var vm = CreateVm(getFilings: MockGetFilings(dto));
        using var _ = vm.Activator.Activate();

        vm.SelectedCount.Should().Be(0);
    }

    [Fact]
    public void SelectedCount_WhenRowIsSelected_UpdatesReactively()
    {
        var dto = MakeDto();
        var vm = CreateVm(getFilings: MockGetFilings(dto));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;

        vm.SelectedCount.Should().Be(1);
    }

    [Fact]
    public void HasSelection_IsFalseWhenNoRowsSelected()
    {
        var dto = MakeDto();
        var vm = CreateVm(getFilings: MockGetFilings(dto));
        using var _ = vm.Activator.Activate();

        vm.HasSelection.Should().BeFalse();
    }

    [Fact]
    public void HasSelection_IsTrueWhenARowIsSelected()
    {
        var dto = MakeDto();
        var vm = CreateVm(getFilings: MockGetFilings(dto));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;

        vm.HasSelection.Should().BeTrue();
    }

    [Fact]
    public void DeleteSelectedLabel_ReflectsSelectedCount()
    {
        var dto1 = MakeDto();
        var dto2 = MakeDto();
        var vm = CreateVm(getFilings: MockGetFilings(dto1, dto2));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;
        vm.Rows[1].IsSelected = true;

        vm.DeleteSelectedLabel.Should().Contain("2");
    }

    [Fact]
    public void SelectAllCommand_SetsAllRowsSelected()
    {
        var vm = CreateVm(getFilings: MockGetFilings(MakeDto(), MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        vm.SelectAllCommand.Execute().Subscribe();

        vm.Rows.Should().OnlyContain(r => r.IsSelected);
        vm.SelectedCount.Should().Be(3);
    }

    [Fact]
    public void ClearSelectionCommand_ClearsAllSelections()
    {
        var vm = CreateVm(getFilings: MockGetFilings(MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;
        vm.Rows[1].IsSelected = true;
        vm.ClearSelectionCommand.Execute().Subscribe();

        vm.Rows.Should().OnlyContain(r => !r.IsSelected);
        vm.SelectedCount.Should().Be(0);
    }

    [Fact]
    public async Task BulkDeleteCommand_WhenCancelled_DoesNotCallHandler()
    {
        var bulkDelete = MockBulkDelete();
        var vm = CreateVm(
            getFilings: MockGetFilings(MakeDto()),
            bulkDelete: bulkDelete,
            confirmDelete: _ => Task.FromResult(false));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;
        vm.BulkDeleteCommand.Execute().Subscribe();

        await bulkDelete.DidNotReceive()
            .HandleAsync(Arg.Any<BulkDeleteFilingsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkDeleteCommand_WhenConfirmed_DispatchesCommandWithCorrectIds()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var bulkDelete = MockBulkDelete();
        var vm = CreateVm(
            getFilings: MockGetFilings(MakeDto(id1), MakeDto(id2)),
            bulkDelete: bulkDelete,
            confirmDelete: _ => Task.FromResult(true));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;
        vm.Rows[1].IsSelected = true;
        vm.BulkDeleteCommand.Execute().Subscribe();

        await bulkDelete.Received(1).HandleAsync(
            Arg.Is<BulkDeleteFilingsCommand>(c =>
                c!.FilingIds.Count == 2 &&
                c.FilingIds.Contains(id1) &&
                c.FilingIds.Contains(id2)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BulkDeleteCommand_OnSuccess_ReloadsAndClearsSelection()
    {
        var getFilings = MockGetFilings(MakeDto());
        var vm = CreateVm(
            getFilings: getFilings,
            bulkDelete: MockBulkDelete(success: true),
            confirmDelete: _ => Task.FromResult(true));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;
        vm.BulkDeleteCommand.Execute().Subscribe();

        // After reload with fresh rows, selected count should be 0
        vm.SelectedCount.Should().Be(0);
    }

    [Fact]
    public void BulkDeleteCommand_OnFailure_SetsErrorMessage()
    {
        var vm = CreateVm(
            getFilings: MockGetFilings(MakeDto()),
            bulkDelete: MockBulkDelete(success: false),
            confirmDelete: _ => Task.FromResult(true));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;
        vm.BulkDeleteCommand.Execute().Subscribe();

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ── IsAllSelected tests (Feature 028) ──────────────────────────────────

    [Fact]
    public void IsAllSelected_WhenNoRowsSelected_ReturnsFalse()
    {
        var vm = CreateVm(getFilings: MockGetFilings(MakeDto(), MakeDto(), MakeDto(), MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        vm.IsAllSelected.Should().BeFalse();
    }

    [Fact]
    public void IsAllSelected_WhenAllRowsSelected_ReturnsTrue()
    {
        var vm = CreateVm(getFilings: MockGetFilings(MakeDto(), MakeDto(), MakeDto(), MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        foreach (var row in vm.Rows) row.IsSelected = true;

        vm.IsAllSelected.Should().BeTrue();
    }

    [Fact]
    public void IsAllSelected_WhenSomeRowsSelected_ReturnsNull()
    {
        var vm = CreateVm(getFilings: MockGetFilings(MakeDto(), MakeDto(), MakeDto(), MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        vm.Rows[0].IsSelected = true;
        vm.Rows[1].IsSelected = true;

        vm.IsAllSelected.Should().BeNull();
    }

    [Fact]
    public void IsAllSelected_SetTrue_SelectsAllRows()
    {
        var vm = CreateVm(getFilings: MockGetFilings(MakeDto(), MakeDto(), MakeDto(), MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        vm.IsAllSelected = true;

        vm.SelectedCount.Should().Be(5);
        vm.IsAllSelected.Should().BeTrue();
    }

    [Fact]
    public void IsAllSelected_SetTrue_FromIndeterminate_SelectsAllRows()
    {
        var vm = CreateVm(getFilings: MockGetFilings(MakeDto(), MakeDto(), MakeDto(), MakeDto(), MakeDto()));
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
        var vm = CreateVm(getFilings: MockGetFilings(MakeDto(), MakeDto(), MakeDto(), MakeDto(), MakeDto()));
        using var _ = vm.Activator.Activate();

        foreach (var row in vm.Rows) row.IsSelected = true;

        vm.IsAllSelected = false;

        vm.SelectedCount.Should().Be(0);
        vm.IsAllSelected.Should().BeFalse();
    }

    [Fact]
    public void IsAllSelected_UpdatesWhenRowSelectionChanges()
    {
        var vm = CreateVm(getFilings: MockGetFilings(MakeDto(), MakeDto(), MakeDto(), MakeDto(), MakeDto()));
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
        var vm = CreateVm(getFilings: MockGetFilings());
        using var _ = vm.Activator.Activate();

        var ex = Record.Exception(() => vm.IsAllSelected);
        ex.Should().BeNull();
        vm.IsAllSelected.Should().BeFalse();
    }

    [Fact]
    public void IsAllSelected_RecalculatesAfterRowsReloaded()
    {
        var getFilings = MockGetFilings(MakeDto(), MakeDto(), MakeDto());
        var vm = CreateVm(getFilings: getFilings);
        using var _ = vm.Activator.Activate();

        // Select all
        foreach (var row in vm.Rows) row.IsSelected = true;
        vm.IsAllSelected.Should().BeTrue();

        // Simulate reload by reconfiguring the mock with fresh unselected rows
        getFilings.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(
                new FilingsPageResult(
                    new[] { MakeDto(), MakeDto(), MakeDto() },
                    3, 1)));
        vm.LoadPageCommand.Execute().Subscribe();

        vm.SelectedCount.Should().Be(0);
        vm.IsAllSelected.Should().BeFalse();
    }
}

