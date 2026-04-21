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
using Rentier.Desktop.Resources;
using Rentier.Desktop.ViewModels;
using Rentier.Desktop.Views;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
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

    [AvaloniaFact]
    public void FilingsView_WhenFilingsLoaded_DataGridHasCorrectRowCount()
    {
        // Arrange
        var rows = new[] { MakeFilingRowDto(), MakeFilingRowDto(), MakeFilingRowDto() };
        var getFilings = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        getFilings.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(new FilingsPageResult(rows, 3, 1)));

        var vm = CreateFilingsViewModelWith(getFilings);
        var view = new FilingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act — activate triggers WhenActivated → LoadPageCommand
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert
        var dataGrid = window.GetVisualDescendants().OfType<DataGrid>().First();
        ((IEnumerable)dataGrid.ItemsSource!).Cast<object>().Count().Should().Be(3);
        vm.HasItems.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void FilingsView_WhenIsLoading_ProgressBarIsVisible()
    {
        // Arrange — stuck TCS keeps the load pending so IsLoading stays true
        var tcs = new TaskCompletionSource<Result<FilingsPageResult, Error>>();
        var getFilings = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        getFilings.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => tcs.Task);

        var vm = CreateFilingsViewModelWith(getFilings);
        var view = new FilingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act — trigger load directly without activating
        vm.LoadPageCommand.Execute().Subscribe();
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
    public void FilingsView_WhenErrorOccurs_ErrorBannerIsVisible()
    {
        // Arrange
        var getFilings = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        getFilings.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Failure(Error.Infrastructure("Test error")));

        var vm = CreateFilingsViewModelWith(getFilings);
        var view = new FilingsView { DataContext = vm };
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
    public void FilingsView_WhenHasItems_DataGridIsVisible()
    {
        // Arrange
        var rows = new[] { MakeFilingRowDto(), MakeFilingRowDto(), MakeFilingRowDto() };
        var getFilings = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        getFilings.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(new FilingsPageResult(rows, 3, 1)));

        var vm = CreateFilingsViewModelWith(getFilings);
        var view = new FilingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act — activate triggers load
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert — VM reports items loaded
        vm.HasItems.Should().BeTrue();

        // Assert — DataGrid is visible when items are present (IsVisible bound to HasItems)
        var dataGrid = window.GetVisualDescendants()
            .OfType<DataGrid>()
            .FirstOrDefault(g => g.IsVisible);
        dataGrid.Should().NotBeNull();

        window.Close();
    }

    [AvaloniaFact]
    public void FilingsView_ActionAndHeaderColumns_RenderQaFixes()
    {
        // Arrange
        var rows = new[] { MakeFilingRowDto() };
        var getFilings = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        getFilings.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(new FilingsPageResult(rows, 1, 1)));

        var vm = CreateFilingsViewModelWith(getFilings);
        var view = new FilingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 1000, Height = 600 };
        window.Show();

        // Act
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert
        var dataGrid = window.GetVisualDescendants().OfType<DataGrid>().First();
        dataGrid.Columns[1].Header.Should().Be(Strings.Filings_Col_Status);

        var selectAllCheckbox = window.GetVisualDescendants()
            .OfType<CheckBox>()
            .FirstOrDefault(checkBox => checkBox.IsThreeState);
        selectAllCheckbox.Should().NotBeNull();
        selectAllCheckbox!.IsVisible.Should().BeTrue();

        var exportButton = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => Avalonia.Controls.ToolTip.GetTip(button) as string == Strings.Filings_Tooltip_Export);
        exportButton.Should().NotBeNull();
        var exportIcons = exportButton!.GetVisualDescendants().OfType<PathIcon>().ToList();
        exportIcons.Should().ContainSingle();
        exportIcons[0].Data.Should().NotBeNull();

        var advanceButton = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => Avalonia.Controls.ToolTip.GetTip(button) as string == vm.Rows[0].AdvanceStatusTooltip);
        advanceButton.Should().NotBeNull();

        window.Close();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a minimal FilingsViewModel with all dependencies mocked.
    /// Returns empty filings page by default.
    /// </summary>
    private static FilingsViewModel CreateMinimalFilingsViewModel()
    {
        var getFilings = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
        getFilings.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FilingsPageResult, Error>.Success(new FilingsPageResult([], 0, 1)));

        return CreateFilingsViewModelWith(getFilings);
    }

    /// <summary>
    /// Creates a FilingsViewModel with a given getFilings handler and all other dependencies mocked.
    /// </summary>
    private static FilingsViewModel CreateFilingsViewModelWith(
        IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>> getFilings)
    {
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
            navigateToManualFiling: () => { },
            scheduler: ImmediateScheduler.Instance);
    }

    /// <summary>Creates a sample <see cref="FilingRowDto"/> for test data.</summary>
    private static FilingRowDto MakeFilingRowDto(Guid? id = null) => new(
        id ?? Guid.NewGuid(),
        FilingStatus.Init,
        IncomeType.Dividend,
        "ACME Corp",
        new DateOnly(2025, 4, 30),
        100.00m,
        null);
}
