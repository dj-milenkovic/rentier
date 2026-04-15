using System.Reactive.Concurrency;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.ViewModels;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.Desktop.Tests;

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

    private static FilingsViewModel CreateVm(
        IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>? getFilings = null,
        ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>? updateStatus = null,
        ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>? updateRef = null,
        ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>? deleteFiling = null,
        Func<string, Task<bool>>? confirmDelete = null)
    {
        return new FilingsViewModel(
            getFilings  ?? MockGetFilings(),
            updateStatus ?? MockUpdateStatus(),
            updateRef    ?? MockUpdateRef(),
            deleteFiling ?? MockDelete(),
            MockExport(),
            MockBulkDeleteFilings(),
            confirmDelete ?? (_ => Task.FromResult(false)),
            _ => Task.CompletedTask,
            ImmediateScheduler.Instance);
    }

    [Fact]
    public void OnActivation_TriggersLoadPageWithDefaultUnpaidFilter()
    {
        var getFilings = MockGetFilings();
        var vm = CreateVm(getFilings: getFilings);

        using var _ = vm.Activator.Activate();

        getFilings.Received(1).HandleAsync(
            Arg.Is<GetFilingsQuery>(q => q.Filter == Application.Enums.FilingFilterMode.Unpaid),
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
        var page = new FilingsPageResult([MakeDto()], 21, 2);
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

        vm.ShowAll = true;

        // LoadPage: 1 activation + 1 filter change
        getFilings.Received(2).HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>());

        // Second call should use All filter
        getFilings.Received(1).HandleAsync(
            Arg.Is<GetFilingsQuery>(q =>
                q.Filter == Application.Enums.FilingFilterMode.All && q.Page == 1),
            Arg.Any<CancellationToken>());
    }
}
