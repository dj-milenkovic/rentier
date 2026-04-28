using System.Collections;
using System.Reactive;
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
            .FirstOrDefault(button => Avalonia.Controls.ToolTip.GetTip(button) as string == GetLocalizedText("Filings_Tooltip_Export"));
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

    // ── Filter chip tests (T006 + T008) ─────────────────────────────────────

    [AvaloniaFact]
    public void FilterChip_WhenReportFilterActive_IsVisible()
    {
        // Arrange
        var vm = CreateMinimalFilingsViewModel();
        vm.ReportIdFilter = Guid.NewGuid();

        var view = new FilingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Act — find the chip Border by checking for a visible Border with a StackPanel child
        var chipBorder = FindChipBorder(window);

        // Assert
        chipBorder.Should().NotBeNull("chip Border should be present in visual tree when ReportIdFilter is set");
        chipBorder!.IsVisible.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void FilterChip_WhenNoReportFilter_IsNotVisible()
    {
        // Arrange — no ReportIdFilter set
        var vm = CreateMinimalFilingsViewModel();

        var view = new FilingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Act
        var chipBorder = FindChipBorder(window);

        // Assert — chip Border either absent or not visible
        var isChipVisible = chipBorder?.IsVisible ?? false;
        isChipVisible.Should().BeFalse("chip Border should not be visible when no ReportIdFilter is active");

        window.Close();
    }

    [AvaloniaFact]
    public void FilterChip_DismissButton_WhenClicked_ChipDisappears()
    {
        // Arrange
        var vm = CreateMinimalFilingsViewModel();
        vm.ReportIdFilter = Guid.NewGuid();

        var view = new FilingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Verify chip is initially visible
        var chipBorder = FindChipBorder(window);
        chipBorder.Should().NotBeNull();
        chipBorder!.IsVisible.Should().BeTrue();

        // Act — execute the command directly (simulates ✕ button click)
        vm.ClearReportFilterCommand.Execute(Unit.Default).Subscribe();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert — chip Border is now hidden
        chipBorder.IsVisible.Should().BeFalse("chip should collapse after ClearReportFilterCommand executes");

        window.Close();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Finds the report filter chip <see cref="Border"/> in the visual tree.
    /// The chip is the Border whose direct child is a StackPanel containing a TextBlock and a Button (✕).
    /// </summary>
    private static Border? FindChipBorder(Window window)
    {
        return window.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(b =>
                b.Child is StackPanel sp &&
                sp.Children.OfType<TextBlock>().Any() &&
                sp.Children.OfType<Button>().Any(btn =>
                    btn.Content is TextBlock tb && tb.Text == "✕"));
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

    private static string GetLocalizedText(string key)
    {
        if (Avalonia.Application.Current?.TryGetResource("Localizer",
            Avalonia.Styling.ThemeVariant.Default, out var localizer) == true
            && localizer is Rentier.Desktop.Services.ILocalizationService loc)
            return loc[key];
        return typeof(Rentier.Desktop.Resources.Strings)
            .GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?.GetValue(null) as string ?? $"[{key}]";
    }
}
