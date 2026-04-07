using System.Reactive.Linq;
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.Desktop.Tests;

public class ReportsViewModelTests
{
    private static ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>> MakeHandler(
        SyncResult? successResult = null)
    {
        var handler = Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>();
        handler.HandleAsync(Arg.Any<SyncMailboxCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SyncResult, Error>.Success(
                successResult ?? new SyncResult(0, [])));
        return handler;
    }

    [Fact]
    public async Task SyncCommand_Execute_CallsHandlerHandleAsync()
    {
        var handler = MakeHandler();
        var vm = new ReportsViewModel(handler);

        await vm.SyncCommand.Execute().FirstAsync();

        await handler.Received(1).HandleAsync(
            Arg.Any<SyncMailboxCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncCommand_Success_SetsStatusMessageWithCount()
    {
        var handler = MakeHandler(new SyncResult(5, []));
        var vm = new ReportsViewModel(handler);

        await vm.SyncCommand.Execute().FirstAsync();

        vm.StatusMessage.Should().Contain("5");
    }

    [Fact]
    public async Task Progress_Update_PropagatesProgressToProperty()
    {
        var handler = Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>();
        handler.HandleAsync(Arg.Any<SyncMailboxCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var progress = callInfo.Arg<SyncMailboxCommand>().Progress;
                progress?.Report(new SyncProgress(10, 5, "file.csv", false));
                return Result<SyncResult, Error>.Success(new SyncResult(0, []));
            });

        var vm = new ReportsViewModel(handler);
        await vm.SyncCommand.Execute().FirstAsync();

        // After completion ProgressValue is set to 100
        vm.ProgressValue.Should().Be(100);
    }

    [Fact]
    public async Task SyncCommand_Failure_ErrorMessageVisible()
    {
        var handler = Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>();
        handler.HandleAsync(Arg.Any<SyncMailboxCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SyncResult, Error>.Failure(Error.Infrastructure("IMAP connection failed")));

        var vm = new ReportsViewModel(handler);
        await vm.SyncCommand.Execute().FirstAsync();

        vm.StatusMessage.Should().Be("IMAP connection failed");
    }
}
