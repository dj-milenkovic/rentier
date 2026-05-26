using System.Reactive.Concurrency;
using System.Reactive.Linq;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.UnitTests;

public class MailboxSettingsViewModelTests
{
    private static readonly DateOnly TestDate = new(2024, 1, 1);

    private static IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>> MockQuery()
        => Substitute.For<IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>>();

    private static ICommandHandler<AddMailboxCommand, Result<Guid, Error>> MockAdd()
        => Substitute.For<ICommandHandler<AddMailboxCommand, Result<Guid, Error>>>();

    private static ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>> MockUpdate()
        => Substitute.For<ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>>>();

    private static ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>> MockDelete()
        => Substitute.For<ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>>>();

    private static MailboxSettingsViewModel CreateVm(
        IQueryHandler<GetMailboxesQuery, Result<IReadOnlyList<MailboxDto>, Error>>? query = null,
        ICommandHandler<AddMailboxCommand, Result<Guid, Error>>? add = null,
        ICommandHandler<UpdateMailboxCommand, Result<VoidResult, Error>>? update = null,
        ICommandHandler<DeleteMailboxCommand, Result<VoidResult, Error>>? delete = null)
        => new(
            query ?? MockQuery(),
            add ?? MockAdd(),
            update ?? MockUpdate(),
            delete ?? MockDelete(),
            ImmediateScheduler.Instance,
            confirmAction: (_, _, _, _) => Task.FromResult(true));

    private static MailboxDto MakeDto(string host = "imap.example.com", int port = 993, string username = "user@example.com")
        => new(Guid.NewGuid(), host, port, username, null, null);

    [Fact]
    public void WhenActivated_NoMailboxes_MailboxesCollectionIsEmpty()
    {
        var query = MockQuery();
        query.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success(new List<MailboxDto>()));

        var vm = CreateVm(query: query);
        using var _ = vm.Activator.Activate();

        vm.Mailboxes.Count.Should().Be(0);
        vm.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public void WhenActivated_TwoMailboxes_PopulatesCollection()
    {
        var dto1 = MakeDto("imap.host1.com");
        var dto2 = MakeDto("imap.host2.com");

        var query = MockQuery();
        query.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success(
                new List<MailboxDto> { dto1, dto2 }));

        var vm = CreateVm(query: query);
        using var _ = vm.Activator.Activate();

        vm.Mailboxes.Count.Should().Be(2);
        vm.Mailboxes[0].DisplayName.Should().Contain("imap.host1.com");
        vm.Mailboxes[1].DisplayName.Should().Contain("imap.host2.com");
    }

    [Fact]
    public void SelectMailbox_PopulatesFormFields()
    {
        var dto = MakeDto("imap.test.com", 143, "test@example.com");

        var query = MockQuery();
        query.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success(
                new List<MailboxDto> { dto }));

        var vm = CreateVm(query: query);
        using var _ = vm.Activator.Activate();

        vm.SelectedMailbox = vm.Mailboxes[0];

        vm.Host.Should().Be("imap.test.com");
        vm.Port.Should().Be(143);
        vm.Username.Should().Be("test@example.com");
        vm.Password.Should().BeEmpty();
        vm.IsEditMode.Should().BeTrue();
    }

    [Fact]
    public void AddNewCommand_Execute_ResetsFormAndClearsSelection()
    {
        var dto = MakeDto();
        var query = MockQuery();
        query.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success(
                new List<MailboxDto> { dto }));

        var vm = CreateVm(query: query);
        using var _ = vm.Activator.Activate();
        vm.SelectedMailbox = vm.Mailboxes[0];

        vm.AddNewCommand.Execute().Subscribe();

        vm.SelectedMailbox.Should().BeNull();
        vm.Host.Should().Be("imap.gmail.com");
        vm.Port.Should().Be(993);
        vm.IsEditMode.Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_NewMode_CallsAddHandlerAndRefreshesList()
    {
        var newId = Guid.NewGuid();
        var addedDto = new MailboxDto(newId, "imap.new.com", 993, "new@example.com", null, null);

        var query = MockQuery();
        query.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Result<IReadOnlyList<MailboxDto>, Error>.Success(new List<MailboxDto>()),
                Result<IReadOnlyList<MailboxDto>, Error>.Success(new List<MailboxDto> { addedDto }));

        var add = MockAdd();
        add.HandleAsync(Arg.Any<AddMailboxCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Guid, Error>.Success(newId));

        var vm = CreateVm(query: query, add: add);
        using var _ = vm.Activator.Activate();

        vm.Host = "imap.new.com";
        vm.Username = "new@example.com";

        await vm.SaveCommand.Execute().FirstAsync();

        await add.Received(1).HandleAsync(Arg.Any<AddMailboxCommand>(), Arg.Any<CancellationToken>());
        await query.Received(2).HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCommand_EditMode_CallsUpdateHandler()
    {
        var dto = MakeDto();
        var query = MockQuery();
        query.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success(new List<MailboxDto> { dto }));

        var update = MockUpdate();
        update.HandleAsync(Arg.Any<UpdateMailboxCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));

        var add = MockAdd();

        var vm = CreateVm(query: query, add: add, update: update);
        using var _ = vm.Activator.Activate();
        vm.SelectedMailbox = vm.Mailboxes[0];
        vm.IsEditMode = true;

        await vm.SaveCommand.Execute().FirstAsync();

        await update.Received(1).HandleAsync(Arg.Any<UpdateMailboxCommand>(), Arg.Any<CancellationToken>());
        await add.DidNotReceive().HandleAsync(Arg.Any<AddMailboxCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DeleteCommand_CanExecute_OnlyWhenMailboxSelected()
    {
        var query = MockQuery();
        query.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success(new List<MailboxDto> { MakeDto() }));

        var vm = CreateVm(query: query);
        using var _ = vm.Activator.Activate();

        bool canExecuteWhenNull = false;
        bool canExecuteWhenSet = false;

        vm.DeleteCommand.CanExecute.FirstAsync().Subscribe(v => canExecuteWhenNull = v);
        canExecuteWhenNull.Should().BeFalse();

        vm.SelectedMailbox = vm.Mailboxes[0];
        vm.DeleteCommand.CanExecute.FirstAsync().Subscribe(v => canExecuteWhenSet = v);
        canExecuteWhenSet.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteCommand_Executes_RemovesFromCollection()
    {
        var dto = MakeDto();
        var query = MockQuery();
        query.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success(new List<MailboxDto> { dto }));

        var delete = MockDelete();
        delete.HandleAsync(Arg.Any<DeleteMailboxCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Success(VoidResult.Value));

        var vm = CreateVm(query: query, delete: delete);
        using var _ = vm.Activator.Activate();
        vm.SelectedMailbox = vm.Mailboxes[0];

        await vm.DeleteCommand.Execute().FirstAsync();

        vm.Mailboxes.Count.Should().Be(0);
    }

    [Fact]
    public async Task SaveCommand_WhenHandlerFailsInEditMode_SetsErrorMessage()
    {
        var dto = MakeDto();
        var query = MockQuery();
        query.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success(new List<MailboxDto> { dto }));

        var update = MockUpdate();
        update.HandleAsync(Arg.Any<UpdateMailboxCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Failure(new Error("ERR_UPDATE", "Save failed")));

        var vm = CreateVm(query: query, update: update);
        using var _ = vm.Activator.Activate();
        vm.SelectedMailbox = vm.Mailboxes[0];

        await vm.SaveCommand.Execute().FirstAsync();

        vm.ErrorMessage.Should().Be("Save failed");
    }

    [Fact]
    public async Task SaveCommand_WhenHandlerFailsInAddMode_SetsErrorMessage()
    {
        var add = MockAdd();
        add.HandleAsync(Arg.Any<AddMailboxCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Guid, Error>.Failure(new Error("ERR_ADD", "Add failed")));

        // LoadAsync fires on activation — give it an empty success so it doesn't throw
        var query = MockQuery();
        query.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success(new List<MailboxDto>()));

        // No mailbox selected after activation → IsEditMode stays false → add path is taken
        var vm = CreateVm(query: query, add: add);
        using var _ = vm.Activator.Activate();

        await vm.SaveCommand.Execute().FirstAsync();

        vm.ErrorMessage.Should().Be("Add failed");
    }

    [Fact]
    public async Task DeleteCommand_WhenHandlerFails_SetsErrorMessage()
    {
        var dto = MakeDto();
        var query = MockQuery();
        query.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Success(new List<MailboxDto> { dto }));

        var delete = MockDelete();
        delete.HandleAsync(Arg.Any<DeleteMailboxCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VoidResult, Error>.Failure(new Error("ERR_DELETE", "Delete failed")));

        var vm = CreateVm(query: query, delete: delete);
        using var _ = vm.Activator.Activate();
        vm.SelectedMailbox = vm.Mailboxes[0];

        await vm.DeleteCommand.Execute().FirstAsync();

        vm.ErrorMessage.Should().Be("Delete failed");
    }

    [Fact]
    public void LoadAsync_WhenHandlerFails_SetsErrorMessage()
    {
        var query = MockQuery();
        query.HandleAsync(Arg.Any<GetMailboxesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MailboxDto>, Error>.Failure(new Error("ERR_LOAD", "Load failed")));

        var vm = CreateVm(query: query);
        using var _ = vm.Activator.Activate();

        vm.ErrorMessage.Should().Be("Load failed");
    }
}
