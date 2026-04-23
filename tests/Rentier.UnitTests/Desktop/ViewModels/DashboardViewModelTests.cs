using System.Reactive.Concurrency;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.ViewModels;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests;

public class DashboardViewModelTests
{
    private static IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>> MakeHandler(
        DashboardDto? dto = null)
    {
        var h = Substitute.For<IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>>();
        h.HandleAsync(Arg.Any<GetDashboardQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<DashboardDto, Error>.Success(dto ?? EmptyDashboard()));
        return h;
    }

    private static IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>> MakeFailingHandler(
        string errorMessage = "Load failed")
    {
        var h = Substitute.For<IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>>();
        h.HandleAsync(Arg.Any<GetDashboardQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<DashboardDto, Error>.Failure(new Error("DASHBOARD_ERROR", errorMessage)));
        return h;
    }

    private static DashboardDto EmptyDashboard() =>
        new([], [], 0, 0, 0, 0m, null);

    private static UpcomingDeadlineDto MakeUpcoming(string entity = "Corp A") =>
        new(Guid.NewGuid(), entity, new DateOnly(2024, 6, 30),
            100m, FilingStatus.Init, IncomeType.Dividend);

    private static OverdueFilingDto MakeOverdue(string entity = "Corp B") =>
        new(Guid.NewGuid(), entity, new DateOnly(2024, 1, 15), 200m, FilingStatus.Init);

    private static DashboardViewModel CreateVm(
        IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>? handler = null,
        Action? navigateToFilings = null)
    {
        return new DashboardViewModel(
            handler ?? MakeHandler(),
            navigateToFilings ?? (() => { }),
            ImmediateScheduler.Instance);
    }

    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        var vm = CreateVm();

        vm.IsLoading.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
        vm.HasData.Should().BeFalse();
        vm.UpcomingDeadlines.Should().BeEmpty();
        vm.OverdueFilings.Should().BeEmpty();
        vm.InitCount.Should().Be(0);
        vm.FiledCount.Should().Be(0);
        vm.PaidCount.Should().Be(0);
    }

    [Fact]
    public void LoadCommand_OnSuccess_PopulatesCollections()
    {
        var dto = new DashboardDto(
            [MakeUpcoming("Corp A"), MakeUpcoming("Corp B")],
            [MakeOverdue("Overdue X")],
            0, 0, 0, 0m, null);
        var vm = CreateVm(handler: MakeHandler(dto));

        using var _ = vm.Activator.Activate();

        vm.UpcomingDeadlines.Should().HaveCount(2);
        vm.OverdueFilings.Should().HaveCount(1);
        vm.OverdueFilings[0].PayingEntity.Should().Be("Overdue X");
    }

    [Fact]
    public void LoadCommand_OnSuccess_SetsStatCounts()
    {
        var dto = new DashboardDto([], [], 3, 2, 7, 0m, null);
        var vm = CreateVm(handler: MakeHandler(dto));

        using var _ = vm.Activator.Activate();

        vm.InitCount.Should().Be(3);
        vm.FiledCount.Should().Be(2);
        vm.PaidCount.Should().Be(7);
        vm.HasData.Should().BeTrue();
    }

    [Fact]
    public void LoadCommand_OnSuccess_FormatsTotalUnpaid()
    {
        var dto = new DashboardDto([], [], 0, 0, 0, 1234.56m, null);
        var vm = CreateVm(handler: MakeHandler(dto));

        using var _ = vm.Activator.Activate();

        vm.TotalUnpaidDisplay.Should().Be("1,234.56 RSD");
    }

    [Fact]
    public void LoadCommand_OnSuccess_SetsLastSyncDisplay()
    {
        var dto = new DashboardDto([], [], 0, 0, 0, 0m, new DateOnly(2024, 7, 15));
        var vm = CreateVm(handler: MakeHandler(dto));

        using var _ = vm.Activator.Activate();

        vm.LastSyncDisplay.Should().Be("2024-07-15");
    }

    [Fact]
    public void LoadCommand_OnSuccess_LastSyncNull_ShowsNever()
    {
        var dto = EmptyDashboard();
        var vm = CreateVm(handler: MakeHandler(dto));

        using var _ = vm.Activator.Activate();

        vm.LastSyncDisplay.Should().Be("Never");
    }

    [Fact]
    public void LoadCommand_OnFailure_SetsErrorMessage()
    {
        var vm = CreateVm(handler: MakeFailingHandler("Something went wrong"));

        using var _ = vm.Activator.Activate();

        vm.ErrorMessage.Should().Be("Something went wrong");
        vm.HasData.Should().BeFalse();
    }

    [Fact]
    public void NavigateToFilingsCommand_CallsDelegate()
    {
        var called = false;
        var vm = CreateVm(navigateToFilings: () => called = true);

        vm.NavigateToFilingsCommand.Execute().Subscribe();

        called.Should().BeTrue();
    }
}
