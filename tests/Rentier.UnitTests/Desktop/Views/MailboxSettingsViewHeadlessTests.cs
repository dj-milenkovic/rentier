using ReactiveUI.Primitives.Concurrency;
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
/// Headless UI tests for <see cref="MailboxSettingsView"/>.
/// Verifies rendering and control state — only what cannot be covered at the ViewModel level.
/// </summary>
[Trait("Category", "UI")]
public class MailboxSettingsViewHeadlessTests
{
    [AvaloniaFact]
    public void MailboxSettingsView_WhenCreated_RendersWithoutError()
    {
        // Arrange
        var vm = CreateMinimalMailboxSettingsViewModel();
        var view = new MailboxSettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };

        // Act
        window.Show();

        // Assert
        view.Should().NotBeNull();
        window.IsVisible.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void MailboxSettingsView_WhenMailboxesLoaded_ListBoxShowsCorrectCount()
    {
        // Arrange
        var dto1 = new MailboxDto(Guid.NewGuid(), "imap.gmail.com", 993, "user1@example.com", null, null);
        var dto2 = new MailboxDto(Guid.NewGuid(), "imap.outlook.com", 993, "user2@example.com", null, null);

        var queryHandler = Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>();
        queryHandler.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success([dto1, dto2]));

        var vm = CreateMinimalMailboxSettingsViewModel(queryHandler);

        // Act — activate triggers WhenActivated → LoadAsync
        using var activation = vm.Activator.Activate();
        Dispatcher.UIThread.RunJobs();

        var view = new MailboxSettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert
        vm.Mailboxes.Should().HaveCount(2);
        var listBox = window.GetVisualDescendants().OfType<ListBox>().First();
        listBox.ItemCount.Should().Be(2);

        window.Close();
    }

    [AvaloniaFact]
    public void MailboxSettingsView_WhenNoMailboxSelected_DeleteButtonIsDisabled()
    {
        // Arrange: load one mailbox but do not select any item.
        var dto = new MailboxDto(Guid.NewGuid(), "imap.gmail.com", 993, "user@example.com", null, null);
        var queryHandler = Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>();
        queryHandler.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success([dto]));

        var vm = CreateMinimalMailboxSettingsViewModel(queryHandler);
        using var activation = vm.Activator.Activate();
        Dispatcher.UIThread.RunJobs();

        // SelectedMailbox should be null after load (no auto-select)
        vm.SelectedMailbox = null;

        var view = new MailboxSettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert
        vm.SelectedMailbox.Should().BeNull();
        var deleteBtn = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.Command == vm.DeleteCommand);

        // Verify binding is in place and command reports CanExecute=false.
        deleteBtn.Should().NotBeNull("the Delete button should be bound to DeleteCommand");
        deleteBtn!.Command?.CanExecute(deleteBtn.CommandParameter).Should().BeFalse(
            "no mailbox is selected so canDelete should be false");

        window.Close();
    }

    [AvaloniaFact]
    public void MailboxSettingsView_WhenMailboxSelected_DeleteButtonIsEnabled()
    {
        // Arrange
        var dto = new MailboxDto(Guid.NewGuid(), "imap.gmail.com", 993, "user@example.com", null, null);

        var queryHandler = Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>();
        queryHandler.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success([dto]));

        var vm = CreateMinimalMailboxSettingsViewModel(queryHandler);

        // Act — load mailboxes then select the first one
        using var activation = vm.Activator.Activate();
        Dispatcher.UIThread.RunJobs();

        vm.SelectedMailbox = vm.Mailboxes.First();

        var view = new MailboxSettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert
        var deleteBtn = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.Command == vm.DeleteCommand);

        deleteBtn.Should().NotBeNull();
        deleteBtn!.IsEnabled.Should().BeTrue();

        window.Close();
    }

    [AvaloniaFact]
    public void MailboxSettingsView_WhenLoadFails_ErrorMessageIsVisible()
    {
        // Arrange
        var queryHandler = Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>();
        queryHandler.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Failure(Error.Infrastructure("Connection failed")));

        var vm = CreateMinimalMailboxSettingsViewModel(queryHandler);

        // Act — activate triggers WhenActivated → LoadAsync → sets ErrorMessage
        using var activation = vm.Activator.Activate();
        Dispatcher.UIThread.RunJobs();

        var view = new MailboxSettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        // Assert
        vm.ErrorMessage.Should().NotBeNullOrEmpty();

        var errBlock = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(tb => tb.IsVisible && tb.Text?.Length > 0);

        errBlock.Should().NotBeNull();

        window.Close();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static MailboxSettingsViewModel CreateMinimalMailboxSettingsViewModel(
        IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>? queryHandler = null)
    {
        queryHandler ??= CreateEmptyMailboxQueryHandler();
        var addHandler = Substitute.For<ICommandHandler<AddMailboxCommand, Result<Guid, Error>>>();
        var updateHandler = Substitute.For<ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>>>();
        var deleteHandler = Substitute.For<ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>>>();
        return new MailboxSettingsViewModel(
            queryHandler,
            addHandler,
            updateHandler,
            deleteHandler,
            scheduler: ImmediateSequencer.Instance);
    }

    private static IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>> CreateEmptyMailboxQueryHandler()
    {
        var h = Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>();
        h.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success([]));
        return h;
    }
}
