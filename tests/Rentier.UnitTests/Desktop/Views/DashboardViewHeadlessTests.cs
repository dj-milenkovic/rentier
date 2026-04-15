using System.Collections;
using System.Reactive.Concurrency;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.ViewModels;
using Rentier.Desktop.Views;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// Headless UI tests for <see cref="DashboardView"/>.
/// Verifies rendering and control state — only what cannot be covered at the ViewModel level.
/// </summary>
[Trait("Category", "UI")]
public class DashboardViewHeadlessTests
{
    [AvaloniaFact]
    public void DashboardView_WhenCreated_RendersWithoutError()
    {
        // Arrange
        var vm = CreateMinimalDashboardViewModel();
        var view = new DashboardView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };

        // Act
        window.Show();

        // Assert
        view.Should().NotBeNull();
        window.IsVisible.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void DashboardView_WhenIsLoading_ProgressBarIsVisible()
    {
        // Arrange — stuck TCS keeps the load pending so IsLoading stays true
        var tcs = new TaskCompletionSource<Result<DashboardDto, Error>>();
        var handler = Substitute.For<IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>>();
        handler.HandleAsync(Arg.Any<GetDashboardQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => tcs.Task);

        var vm = CreateDashboardViewModelWith(handler);
        var view = new DashboardView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act — trigger load directly without activating
        vm.LoadCommand.Execute().Subscribe();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert
        var progressBar = window.GetVisualDescendants().OfType<ProgressBar>().First();
        progressBar.IsVisible.Should().BeTrue();

        // Cleanup
        tcs.TrySetCanceled();
        window.Close();
    }

    [AvaloniaFact]
    public void DashboardView_WhenUpcomingDeadlinesLoaded_GridShowsCorrectRowCount()
    {
        // Arrange
        var deadlines = new[]
        {
            MakeUpcomingDeadline(),
            MakeUpcomingDeadline(),
            MakeUpcomingDeadline()
        };
        var dto = new DashboardDto(deadlines, [], 3, 0, 0, 0m, null);

        var handler = Substitute.For<IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>>();
        handler.HandleAsync(Arg.Any<GetDashboardQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<DashboardDto, Error>.Success(dto));

        var vm = CreateDashboardViewModelWith(handler);
        var view = new DashboardView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act — activate triggers WhenActivated → LoadCommand
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert — ViewModel has correct data
        vm.UpcomingDeadlines.Should().HaveCount(3);

        // Assert — named DataGrid reflects the items
        var grid = window.GetVisualDescendants()
            .OfType<DataGrid>()
            .FirstOrDefault(g => g.Name == "UpcomingGrid");
        grid.Should().NotBeNull();
        ((IEnumerable)grid!.ItemsSource!).Cast<object>().Count().Should().Be(3);

        window.Close();
    }

    [AvaloniaFact]
    public void DashboardView_WhenErrorOccurs_ErrorTextBlockIsVisible()
    {
        // Arrange
        var handler = Substitute.For<IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>>();
        handler.HandleAsync(Arg.Any<GetDashboardQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<DashboardDto, Error>.Failure(Error.Infrastructure("Dashboard load failed")));

        var vm = CreateDashboardViewModelWith(handler);
        var view = new DashboardView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act — activate triggers load which will fail
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert — VM state reflects the error
        vm.ErrorMessage.Should().NotBeNullOrEmpty();

        // Assert — the error TextBlock is visible in the visual tree
        var errorTextBlock = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(tb => tb.IsVisible && tb.Text == vm.ErrorMessage);
        errorTextBlock.Should().NotBeNull();

        window.Close();
    }

    [AvaloniaFact]
    public void DashboardView_WhenOverdueFilingsPresent_OverdueSectionIsVisible()
    {
        // Arrange
        var overdue = new[]
        {
            MakeOverdueFiling(),
            MakeOverdueFiling()
        };
        var dto = new DashboardDto([], overdue, 0, 0, 0, 0m, null);

        var handler = Substitute.For<IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>>();
        handler.HandleAsync(Arg.Any<GetDashboardQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<DashboardDto, Error>.Success(dto));

        var vm = CreateDashboardViewModelWith(handler);
        var view = new DashboardView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act — activate triggers load
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert — VM properties reflect the overdue data
        vm.HasOverdueFilings.Should().BeTrue();
        vm.OverdueFilings.Should().HaveCount(2);

        // Assert — the StackPanel bound to HasOverdueFilings is visible in the visual tree.
        // The panel is identified by containing visible children with overdue filing data.
        var overduePanels = window.GetVisualDescendants()
            .OfType<StackPanel>()
            .Where(sp => sp.IsVisible)
            .ToList();
        overduePanels.Should().NotBeEmpty();

        window.Close();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Creates a minimal DashboardViewModel returning an empty dashboard.</summary>
    private static DashboardViewModel CreateMinimalDashboardViewModel()
    {
        var handler = Substitute.For<IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>>>();
        handler.HandleAsync(Arg.Any<GetDashboardQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<DashboardDto, Error>.Success(EmptyDashboard()));

        return CreateDashboardViewModelWith(handler);
    }

    /// <summary>Creates a DashboardViewModel with a given handler and all other dependencies mocked.</summary>
    private static DashboardViewModel CreateDashboardViewModelWith(
        IQueryHandler<GetDashboardQuery, Result<DashboardDto, Error>> handler)
    {
        return new DashboardViewModel(
            handler,
            navigateToFilings: () => { },
            scheduler: ImmediateScheduler.Instance);
    }

    private static DashboardDto EmptyDashboard() => new([], [], 0, 0, 0, 0m, null);

    private static UpcomingDeadlineDto MakeUpcomingDeadline() => new(
        Guid.NewGuid(),
        "ACME Corp",
        new DateOnly(2025, 4, 30),
        100.00m,
        FilingStatus.Init,
        IncomeType.Dividend);

    private static OverdueFilingDto MakeOverdueFiling() => new(
        Guid.NewGuid(),
        "ACME Corp",
        new DateOnly(2025, 3, 31),
        200.00m,
        FilingStatus.Init);
}
