using System.Reactive.Concurrency;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
/// Headless UI tests that verify views can be instantiated and rendered
/// without crashing. Keeps tests narrow — only test what cannot be tested
/// at the ViewModel level.
/// </summary>
[Trait("Category", "UI")]
public class FilingsViewHeadlessTests
{
    [AvaloniaFact]
    public void Window_WhenShown_DoesNotThrow()
    {
        // Arrange + Act
        var window = new Window
        {
            Title = "Test Window",
            Width = 400,
            Height = 300
        };
        window.Show();

        // Assert
        window.IsVisible.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void FilingsView_WhenCreated_DoesNotThrow()
    {
        // Arrange + Act
        var view = new FilingsView();

        // Assert
        view.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void FilingsView_WithMockedViewModel_RendersWithoutErrors()
    {
        // Arrange
        var vm = CreateMinimalFilingsViewModel();
        var view = new FilingsView
        {
            DataContext = vm
        };

        var window = new Window
        {
            Content = view,
            Width = 800,
            Height = 600
        };

        // Act
        window.Show();

        // Assert
        view.DataContext.Should().Be(vm);
        window.IsVisible.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void FilingsView_WhenNoFilingsLoaded_ShowsEmptyState()
    {
        // Arrange
        var vm = CreateMinimalFilingsViewModel();
        var view = new FilingsView
        {
            DataContext = vm
        };

        var window = new Window
        {
            Content = view,
            Width = 800,
            Height = 600
        };
        window.Show();

        // Act - activate the VM to trigger initial load
        using var activation = vm.Activator.Activate();

        // Assert - ViewModel should report IsEmpty when no filings loaded
        vm.IsEmpty.Should().BeTrue();
        vm.Rows.Should().BeEmpty();

        window.Close();
    }

    /// <summary>
    /// Creates a minimal FilingsViewModel with all dependencies mocked.
    /// Returns empty filings page by default.
    /// </summary>
    private static FilingsViewModel CreateMinimalFilingsViewModel()
    {
        var getFilings = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        getFilings.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(new FilingsPageResult([], 0, 1)));

        var updateStatus = Substitute.For<ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>>();
        var updateRef = Substitute.For<ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>>>();
        var deleteFiling = Substitute.For<ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>>>();
        var exportFiling = Substitute.For<ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>>();
        var bulkDelete = Substitute.For<ICommandHandler<BulkDeleteFilingsCommand, Result<VoidResult, Error>>>();

        return new FilingsViewModel(
            getFilings,
            updateStatus,
            updateRef,
            deleteFiling,
            exportFiling,
            bulkDelete,
            confirmDelete: _ => Task.FromResult(false),
            saveFile: _ => Task.CompletedTask,
            scheduler: ImmediateScheduler.Instance);
    }
}
