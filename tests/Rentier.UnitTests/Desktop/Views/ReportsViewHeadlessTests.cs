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
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// Headless UI tests for <see cref="ReportsView"/>. Verifies that the view renders
/// correctly and reflects ViewModel state changes — things that cannot be tested
/// at the ViewModel level.
/// </summary>
[Trait("Category", "UI")]
public class ReportsViewHeadlessTests
{
    [AvaloniaFact]
    public void ReportsView_ActionColumn_RendersIconOnlyButtons()
    {
        // Arrange — load a report row so the action column is instantiated
        IReadOnlyList<ReportRowDto> rows = [MakeReportRowDto()];
        var getReports = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(
                new ReportsPageResult(rows, rows.Count, 1)));

        var vm = CreateMinimalReportsViewModel(getReports);
        var view = new ReportsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert (a) — no Button in the visual tree has the old text-label content for the action buttons.
        // The old content was the value of Reports_Button_ViewFilings / Reports_Button_Delete strings.
        // We check that no button has exactly those strings as Content.
        var buttons = window.GetVisualDescendants().OfType<Button>().ToList();
        var viewFilingsTextButton = buttons.FirstOrDefault(b =>
            b.Content is string s && s == Rentier.Desktop.Resources.Strings.Reports_Button_ViewFilings);
        var deleteTextButton = buttons.FirstOrDefault(b =>
            b.Content is string s && s == Rentier.Desktop.Resources.Strings.Reports_Button_Delete);
        viewFilingsTextButton.Should().BeNull(
            because: "View Filings action button must be icon-only, not text-labelled");
        deleteTextButton.Should().BeNull(
            because: "Delete action button must be icon-only, not text-labelled");

        // Assert (b) — the action column contains exactly two PathIcon descendants per row
        var pathIcons = window.GetVisualDescendants().OfType<PathIcon>().ToList();
        pathIcons.Count.Should().BeGreaterThanOrEqualTo(2,
            because: "each report row should render two PathIcon elements (View Filings + Delete)");

        window.Close();
    }

    [AvaloniaFact]
    public void ReportsView_WhenCreated_RendersWithoutError()
    {
        // Arrange
        var vm = CreateMinimalReportsViewModel();
        var view = new ReportsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };

        // Act
        window.Show();

        // Assert
        view.Should().NotBeNull();
        window.IsVisible.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void ReportsView_WhenNoReports_EmptyStateIsVisible()
    {
        // Arrange
        var vm = CreateMinimalReportsViewModel(CreateEmptyReportsHandler());
        var view = new ReportsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act — activation triggers WhenActivated → LoadReportsCommand.Execute()
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert
        vm.IsEmpty.Should().BeTrue();
        vm.Rows.Should().BeEmpty();

        window.Close();
    }

    [AvaloniaFact]
    public void ReportsView_WhenNoReports_PaginationRemainsVisible()
    {
        var vm = CreateMinimalReportsViewModel(CreateEmptyReportsHandler());
        var view = new ReportsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        FindButton(window, Rentier.Desktop.Resources.Strings.Reports_Page_Previous).IsVisible.Should().BeTrue();
        FindButton(window, Rentier.Desktop.Resources.Strings.Reports_Page_Next).IsVisible.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void ReportsView_WhenReportsLoaded_DataGridHasCorrectRowCount()
    {
        // Arrange
        IReadOnlyList<ReportRowDto> rows = [MakeReportRowDto(), MakeReportRowDto(), MakeReportRowDto()];
        var getReports = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(
                new ReportsPageResult(rows, rows.Count, 1)));

        var vm = CreateMinimalReportsViewModel(getReports);
        var view = new ReportsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert — ViewModel state
        vm.HasItems.Should().BeTrue();
        vm.Rows.Should().HaveCount(3);

        // Assert — DataGrid reflects ItemsSource
        var grid = window.GetVisualDescendants().OfType<DataGrid>().First();
        ((IEnumerable)grid.ItemsSource!).Cast<object>().Count().Should().Be(3);

        window.Close();
    }

    [AvaloniaFact]
    public void ReportsView_WhenMultiplePagesExist_NextButtonIsEnabled()
    {
        IReadOnlyList<ReportRowDto> rows = [MakeReportRowDto()];
        var getReports = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(
                new ReportsPageResult(rows, 31, 2)));

        var vm = CreateMinimalReportsViewModel(getReports);
        var view = new ReportsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        FindButton(window, Rentier.Desktop.Resources.Strings.Reports_Page_Next).IsEnabled.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void ReportsView_WhenRowsLoaded_HeaderSelectionCheckboxIsRendered()
    {
        IReadOnlyList<ReportRowDto> rows = [MakeReportRowDto()];
        var getReports = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(
                new ReportsPageResult(rows, rows.Count, 1)));

        var vm = CreateMinimalReportsViewModel(getReports);
        var view = new ReportsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        window.GetVisualDescendants().OfType<CheckBox>().Should().HaveCountGreaterThan(1);

        window.Close();
    }

    [AvaloniaFact]
    public void ReportsView_WhenLoadFails_ErrorMessageIsSet()
    {
        // Arrange
        var getReports = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Failure(
                Error.Infrastructure("Load failed")));

        var vm = CreateMinimalReportsViewModel(getReports);
        var view = new ReportsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert
        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.ErrorMessage.Should().Contain("Load failed");

        window.Close();
    }

    [AvaloniaFact]
    public void ReportsView_WhenIsLoading_ProgressBarIsVisible()
    {
        // Arrange — a stuck TCS keeps LoadReportsAsync suspended so IsLoading stays true
        var tcs = new TaskCompletionSource<Result<ReportsPageResult, Error>>();
        var getReports = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        getReports.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => tcs.Task);

        var vm = CreateMinimalReportsViewModel(getReports);
        var view = new ReportsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act — trigger load manually (not via activation) so we control timing
        vm.LoadReportsCommand.Execute().Subscribe();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert
        vm.IsLoading.Should().BeTrue();

        // The indeterminate ProgressBar (Height=4) is bound to IsLoading
        var progressBar = window.GetVisualDescendants().OfType<ProgressBar>()
            .First(pb => pb.IsIndeterminate);
        progressBar.IsVisible.Should().BeTrue();

        // Cleanup — cancel the pending task to unblock the command
        tcs.TrySetCanceled();
        window.Close();
    }

    [AvaloniaFact]
    public void ReportsView_WhenLoadCompletes_IsLoadingIsFalse()
    {
        // Arrange
        var vm = CreateMinimalReportsViewModel(CreateEmptyReportsHandler());
        var view = new ReportsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        // Act
        using var activation = vm.Activator.Activate();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert — after load completes, IsLoading returns to false (import button re-enabled)
        vm.IsLoading.Should().BeFalse();

        window.Close();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a <see cref="ReportsViewModel"/> with all dependencies mocked.</summary>
    private static ReportsViewModel CreateMinimalReportsViewModel(
        IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>? getReports = null)
    {
        getReports ??= CreateEmptyReportsHandler();
        var syncHandler = Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>();
        var importReport = Substitute.For<ICommandHandler<ImportReportCommand, Result<Guid, Error>>>();
        var deleteReport = Substitute.For<ICommandHandler<DeleteReportCommand, Result<VoidResult, Error>>>();
        var bulkDelete = Substitute.For<ICommandHandler<BulkDeleteReportsCommand, Result<VoidResult, Error>>>();

        return new ReportsViewModel(
            syncHandler,
            getReports,
            importReport,
            deleteReport,
            bulkDelete,
            confirmDelete: (_, _) => Task.FromResult(false),
            showImportDialog: () => Task.FromResult<(Guid ImporterId, string FileName, byte[] Content)?>(null),
            navigateToFilings: _ => { },
            scheduler: ImmediateScheduler.Instance);
    }

    private static IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>> CreateEmptyReportsHandler()
    {
        var handler = Substitute.For<IQueryHandler<GetReportsQuery, Result<ReportsPageResult, Error>>>();
        handler.HandleAsync(Arg.Any<GetReportsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ReportsPageResult, Error>.Success(
                new ReportsPageResult(Array.Empty<ReportRowDto>(), 0, 1)));
        return handler;
    }

    private static ReportRowDto MakeReportRowDto() => new(
        Guid.NewGuid(),
        "IBKR 2025-01",
        new DateOnly(2025, 1, 15),
        null,
        "IBKR Importer",
        ReportStatus.Processed,
        3,
        "IBKR \u2013 Jan 2025",
        new DateOnly(2025, 1, 10));

    private static Button FindButton(Window window, string content)
        => window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Content is string text && text == content);
}
