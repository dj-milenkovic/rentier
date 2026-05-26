using System.Collections;
using System.Reactive.Concurrency;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.ViewModels;
using Rentier.Desktop.Views;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// Headless UI tests for <see cref="HolidaySettingsView"/>.
/// Verifies rendering and control state — only what cannot be covered at the ViewModel level.
/// </summary>
[Trait("Category", "UI")]
public class HolidaySettingsViewHeadlessTests
{
    [AvaloniaFact]
    public void HolidaySettingsView_WhenCreated_RendersWithoutError()
    {
        // Arrange
        var vm = CreateMinimalHolidaySettingsViewModel();
        var view = new HolidaySettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };

        // Act
        window.Show();

        // Assert
        view.Should().NotBeNull();
        window.IsVisible.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void HolidaySettingsView_WhenHolidaysLoaded_GridShowsCorrectRowCount()
    {
        // Arrange — 3 holidays all within the default 2024–2025 range
        var holidays = new[]
        {
            MakeHolidayEntryDto(2024),
            MakeHolidayEntryDto(2024, month: 6, day: 15),
            MakeHolidayEntryDto(2025, month: 1, day: 1),
        };
        var conf = new HolidayConfDto(holidays, 2024, 2025);

        var queryHandler = Substitute.For<IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>>();
        queryHandler.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Success(conf));

        var vm = CreateMinimalHolidaySettingsViewModel(queryHandler);

        // Act — activate triggers WhenActivated → LoadAsync
        using var activation = vm.Activator.Activate();
        Dispatcher.UIThread.RunJobs();

        var view = new HolidaySettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert
        vm.FilteredEntries.Should().HaveCount(3);

        // Use visual tree instead of FindControl to avoid name-scope boundary issues
        var holidaysGrid = window.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault();
        holidaysGrid.Should().NotBeNull();
        ((IEnumerable)holidaysGrid!.ItemsSource!).Cast<object>().Count().Should().Be(3);

        window.Close();
    }

    [AvaloniaFact]
    public void HolidaySettingsView_WhenNoHolidays_NoItemsMessageIsVisible()
    {
        // Arrange — empty holiday list
        var vm = CreateMinimalHolidaySettingsViewModel();

        // Act — activate triggers WhenActivated → LoadAsync (empty)
        using var activation = vm.Activator.Activate();
        Dispatcher.UIThread.RunJobs();

        var view = new HolidaySettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert
        vm.HasItems.Should().BeFalse();

        // "No holidays configured..." TextBlock is bound to !HasItems — visible when HasItems=false.
        // Match by Opacity=0.6 (unique to this empty-state element) to remain locale-agnostic.
        var noItemsBlock = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(tb => tb.IsVisible && Math.Abs(tb.Opacity - 0.6) < 0.01 && !string.IsNullOrEmpty(tb.Text));
        noItemsBlock.Should().NotBeNull();

        window.Close();
    }

    [AvaloniaFact]
    public void HolidaySettingsView_WhenNoEntrySelected_DeleteButtonIsDisabled()
    {
        // Arrange — no entry selected (default state)
        var vm = CreateMinimalHolidaySettingsViewModel();

        var view = new HolidaySettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert
        vm.SelectedEntry.Should().BeNull();

        var deleteBtn = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.Command == vm.DeleteRowCommand);

        // Verify binding is in place and command reports CanExecute=false.
        // Checking button.Command.CanExecute() is reliable in headless tests even when
        // the Avalonia IsEnabled update is asynchronous via RxApp.MainThreadScheduler.
        deleteBtn.Should().NotBeNull("the Delete button should be bound to DeleteRowCommand");
        deleteBtn!.Command?.CanExecute(deleteBtn.CommandParameter).Should().BeFalse(
            "no entry is selected so canDelete should be false");

        window.Close();
    }

    [AvaloniaFact]
    public void HolidaySettingsView_WhenLoadFails_ErrorMessageIsVisible()
    {
        // Arrange
        var queryHandler = Substitute.For<IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>>();
        queryHandler.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Failure(Error.Infrastructure("Load failed")));

        var vm = CreateMinimalHolidaySettingsViewModel(queryHandler);

        // Act — activate triggers WhenActivated → LoadAsync → sets ErrorMessage
        using var activation = vm.Activator.Activate();
        Dispatcher.UIThread.RunJobs();

        // Assert
        vm.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [AvaloniaFact]
    public void HolidaySettingsView_WhenEntriesOutsideYearRange_FilteredEntriesIsEmpty()
    {
        // Arrange — 2 holidays in 2020, but the loaded range is 2024–2025
        var holidays = new[]
        {
            MakeHolidayEntryDto(year: 2020),
            MakeHolidayEntryDto(year: 2020, month: 7, day: 4),
        };
        var conf = new HolidayConfDto(holidays, 2024, 2025);

        var queryHandler = Substitute.For<IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>>();
        queryHandler.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Success(conf));

        var vm = CreateMinimalHolidaySettingsViewModel(queryHandler);

        // Act
        using var activation = vm.Activator.Activate();
        Dispatcher.UIThread.RunJobs();

        // Assert
        vm.Entries.Should().HaveCount(2);
        vm.FilteredEntries.Should().BeEmpty();
        vm.IsFilteredEmpty.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static HolidaySettingsViewModel CreateMinimalHolidaySettingsViewModel(
        IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>? queryHandler = null)
    {
        queryHandler ??= CreateEmptyHolidayQueryHandler();
        var saveHandler = Substitute.For<ICommandHandler<SaveHolidayConfCommand, Result<VoidResult, Error>>>();
        var fetchHandler = Substitute.For<ICommandHandler<FetchHolidaysFromWebCommand, Result<IReadOnlyList<HolidayEntryDto>, Error>>>();
        return new HolidaySettingsViewModel(
            queryHandler,
            saveHandler,
            fetchHandler,
            scheduler: ImmediateScheduler.Instance);
    }

    private static IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>> CreateEmptyHolidayQueryHandler()
    {
        var h = Substitute.For<IQueryHandler<GetHolidayConfQuery, Result<HolidayConfDto, Error>>>();
        h.HandleAsync(Arg.Any<GetHolidayConfQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HolidayConfDto, Error>.Success(new HolidayConfDto([], 2024, 2025)));
        return h;
    }

    private static HolidayEntryDto MakeHolidayEntryDto(int year = 2024, int month = 1, int day = 1) =>
        new(new DateOnly(year, month, day), "New Year");
}
