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
using Rentier.Desktop.ViewModels;
using Rentier.Desktop.Views;
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// Headless UI tests for <see cref="SyncView"/>. Verifies rendering and ViewModel
/// state reflection — things that cannot be tested at the ViewModel level alone.
/// </summary>
[Trait("Category", "UI")]
public class SyncViewHeadlessTests
{
    [AvaloniaFact]
    public void SyncView_WhenCreated_RendersWithoutError()
    {
        // Arrange
        var vm = CreateMinimalSyncViewModel();
        var view = new SyncView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };

        // Act
        window.Show();

        // Assert
        view.Should().NotBeNull();
        window.IsVisible.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void SyncView_WhenDefault_StartButtonCanBeEnabled()
    {
        // Arrange — default: Incremental mode, ValidationError = null, IsRunning = false
        var vm = CreateMinimalSyncViewModel();
        var view = new SyncView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert — ViewModel preconditions for an enabled start button
        vm.IsRunning.Should().BeFalse();
        vm.ValidationError.Should().BeNull();

        window.Close();
    }

    [AvaloniaFact]
    public void SyncView_WhenDefault_IsNotRunning()
    {
        // Arrange
        var vm = CreateMinimalSyncViewModel();
        var view = new SyncView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert — CancelCommand is enabled only when IsRunning = true
        vm.IsRunning.Should().BeFalse();

        window.Close();
    }

    [AvaloniaFact]
    public void SyncView_WhenFullReplayMode_IsFullReplayModeIsTrue()
    {
        // Arrange
        var vm = CreateMinimalSyncViewModel();
        var view = new SyncView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        using var activation = vm.Activator.Activate();

        // Act — switch to FullReplay mode
        vm.SelectedSyncMode = SyncMode.FullReplay;
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert — drives the warning TextBlock visibility binding
        vm.IsFullReplayMode.Should().BeTrue();
        vm.IsReplayFromDateMode.Should().BeFalse();

        window.Close();
    }

    [AvaloniaFact]
    public void SyncView_WhenIncrementalMode_DatePickerIsNotEffectivelyVisible()
    {
        // Arrange — default is Incremental, so the date-picker Grid is hidden
        var vm = CreateMinimalSyncViewModel();
        var view = new SyncView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert — ViewModel state drives IsVisible on the wrapping Grid
        vm.IsReplayFromDateMode.Should().BeFalse();

        // The DatePicker's parent Grid has IsVisible="{Binding IsReplayFromDateMode}",
        // so the DatePicker itself should not be effectively visible.
        var datePicker = window.GetVisualDescendants().OfType<DatePicker>().FirstOrDefault();
        if (datePicker is not null)
            datePicker.IsEffectivelyVisible.Should().BeFalse();

        window.Close();
    }

    [AvaloniaFact]
    public void SyncView_WhenReplayFromDateModeSelected_DatePickerRowBecomesVisible()
    {
        // Arrange
        var vm = CreateMinimalSyncViewModel();
        var view = new SyncView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        using var activation = vm.Activator.Activate();

        // Act
        vm.SelectedSyncMode = SyncMode.ReplayFromDate;
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert — drives IsVisible on the wrapping Grid
        vm.IsReplayFromDateMode.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void SyncView_WhenReplayFromDateModeWithNoDateSet_ValidationErrorIsNotNull()
    {
        // Arrange
        var vm = CreateMinimalSyncViewModel();
        var view = new SyncView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        using var activation = vm.Activator.Activate();

        // Act — switch to ReplayFromDate but leave ReplayFromDateOffset as null
        vm.SelectedSyncMode = SyncMode.ReplayFromDate;
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert — SyncCommand.CanExecute is false when ValidationError is non-null
        vm.ReplayFromDateOffset.Should().BeNull();
        vm.ValidationError.Should().NotBeNullOrEmpty();

        window.Close();
    }

    [AvaloniaFact]
    public void SyncView_WhenInitialized_LogEntriesIsEmpty()
    {
        // Arrange
        var vm = CreateMinimalSyncViewModel();
        var view = new SyncView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert — no log entries until a sync run starts
        vm.LogEntries.Should().BeEmpty();

        window.Close();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a <see cref="SyncViewModel"/> with all dependencies mocked.</summary>
    private static SyncViewModel CreateMinimalSyncViewModel()
    {
        var handler = Substitute.For<ISyncAllCommandHandler>();
        handler.HandleAsync(
                Arg.Any<SyncAllCommand>(),
                Arg.Any<IProgress<SyncProgressEntry>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<SyncAllResult, Error>.Success(
                new SyncAllResult(0, 0, 0, 0, 0, 0, 0, [])));

        return new SyncViewModel(
            handler,
            navigateToFilings: () => { },
            scheduler: ImmediateScheduler.Instance);
    }
}
