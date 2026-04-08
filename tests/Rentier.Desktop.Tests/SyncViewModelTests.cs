using System.Reactive.Concurrency;
using System.Reactive.Linq;
using FluentAssertions;
using NSubstitute;
using ReactiveUI;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.Desktop.Tests;

public class SyncViewModelTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ISyncAllCommandHandler MakeHandler(
        Result<SyncAllResult, Error>? result = null)
    {
        var h = Substitute.For<ISyncAllCommandHandler>();
        h.HandleAsync(
                Arg.Any<SyncAllCommand>(),
                Arg.Any<IProgress<SyncProgressEntry>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result
                ?? Result<SyncAllResult, Error>.Success(new SyncAllResult(1, 0, 0, 0, []))));
        return h;
    }

    private static SyncViewModel CreateVm(
        ISyncAllCommandHandler? handler = null,
        Action? navigateToFilings = null)
        => new(
            handler ?? MakeHandler(),
            navigateToFilings ?? (() => { }),
            ImmediateScheduler.Instance);

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithEmptyLog()
    {
        var vm = CreateVm();

        vm.LogEntries.Should().BeEmpty();
        vm.ErrorMessage.Should().BeNull();
        vm.SummaryMessage.Should().BeEmpty();
        vm.HasErrors.Should().BeFalse();
        vm.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task SyncCommand_Execute_SetsIsRunning()
    {
        var tcs = new TaskCompletionSource<Result<SyncAllResult, Error>>();
        var handler = Substitute.For<ISyncAllCommandHandler>();
        handler.HandleAsync(
                Arg.Any<SyncAllCommand>(),
                Arg.Any<IProgress<SyncProgressEntry>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => tcs.Task);

        var vm = new SyncViewModel(handler, () => { });

        using var _ = vm.Activator.Activate();

        var runningValues = new List<bool>();
        vm.WhenAnyValue(x => x.IsRunning).Subscribe(v => runningValues.Add(v));

        var executeTask = vm.SyncCommand.Execute().FirstAsync().GetAwaiter();

        // Allow command to start
        await Task.Delay(50);
        tcs.SetResult(Result<SyncAllResult, Error>.Success(new SyncAllResult(1, 0, 0, 0, [])));

        await Task.Delay(50);

        runningValues.Should().Contain(true);
    }

    [Fact]
    public async Task SyncCommand_OnSuccess_PopulatesLogEntries()
    {
        var progress = Array.Empty<SyncProgressEntry>();
        var handler = Substitute.For<ISyncAllCommandHandler>();
        handler.HandleAsync(
                Arg.Any<SyncAllCommand>(),
                Arg.Any<IProgress<SyncProgressEntry>>(),
                Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var p = ci.ArgAt<IProgress<SyncProgressEntry>>(1);
                p.Report(new SyncProgressEntry(DateTimeOffset.Now, "Starting...", SyncProgressSeverity.Info));
                await Task.Yield();
                return Result<SyncAllResult, Error>.Success(new SyncAllResult(1, 0, 0, 0, []));
            });

        var vm = new SyncViewModel(handler, () => { }, ImmediateScheduler.Instance);

        await vm.SyncCommand.Execute().FirstAsync();

        vm.LogEntries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SyncCommand_OnSuccess_SetsSummaryMessage()
    {
        var handler = MakeHandler(
            Result<SyncAllResult, Error>.Success(new SyncAllResult(1, 2, 3, 4, [])));

        var vm = CreateVm(handler);

        await vm.SyncCommand.Execute().FirstAsync();

        vm.SummaryMessage.Should().Contain("4 filing(s) created");
        vm.SummaryMessage.Should().Contain("0 error(s)");
    }

    [Fact]
    public async Task SyncCommand_OnSuccessNoErrors_NavigatesToFilings()
    {
        bool navigated = false;
        var handler = MakeHandler(
            Result<SyncAllResult, Error>.Success(new SyncAllResult(1, 0, 0, 1, [])));

        var vm = CreateVm(handler, () => navigated = true);

        await vm.SyncCommand.Execute().FirstAsync();

        navigated.Should().BeTrue();
    }

    [Fact]
    public async Task SyncCommand_OnSuccessWithErrors_DoesNotNavigate()
    {
        bool navigated = false;
        var handler = MakeHandler(
            Result<SyncAllResult, Error>.Success(new SyncAllResult(1, 0, 0, 0, ["some error"])));

        var vm = CreateVm(handler, () => navigated = true);

        await vm.SyncCommand.Execute().FirstAsync();

        navigated.Should().BeFalse();
        vm.HasErrors.Should().BeTrue();
    }

    [Fact]
    public async Task SyncCommand_OnFailure_SetsErrorMessage()
    {
        var handler = MakeHandler(
            Result<SyncAllResult, Error>.Failure(new Error("SYNC_FAILED", "Connection refused")));

        var vm = CreateVm(handler);

        await vm.SyncCommand.Execute().FirstAsync();

        vm.ErrorMessage.Should().Be("Connection refused");
    }

    [Fact]
    public async Task SyncCommand_OnFailure_SetsHasErrors()
    {
        var handler = MakeHandler(
            Result<SyncAllResult, Error>.Failure(new Error("SYNC_FAILED", "Connection refused")));

        var vm = CreateVm(handler);

        await vm.SyncCommand.Execute().FirstAsync();

        vm.HasErrors.Should().BeTrue();
    }

    [Fact]
    public async Task SyncCommand_OnFailure_DoesNotNavigate()
    {
        bool navigated = false;
        var handler = MakeHandler(
            Result<SyncAllResult, Error>.Failure(new Error("SYNC_FAILED", "Oops")));

        var vm = CreateVm(handler, () => navigated = true);

        await vm.SyncCommand.Execute().FirstAsync();

        navigated.Should().BeFalse();
    }

    [Fact]
    public async Task CancelCommand_WhenRunning_CancelsToken()
    {
        CancellationToken capturedToken = default;
        var tcs = new TaskCompletionSource<Result<SyncAllResult, Error>>();

        var handler = Substitute.For<ISyncAllCommandHandler>();
        handler.HandleAsync(
                Arg.Any<SyncAllCommand>(),
                Arg.Any<IProgress<SyncProgressEntry>>(),
                Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                capturedToken = ci.ArgAt<CancellationToken>(2);
                return await tcs.Task;
            });

        var vm = new SyncViewModel(handler, () => { });
        using var _ = vm.Activator.Activate();

        var executeTask = vm.SyncCommand.Execute().FirstAsync().GetAwaiter();

        // Wait until the handler is invoked and running
        await Task.Delay(100);

        // Cancel
        await vm.CancelCommand.Execute().FirstAsync();
        tcs.SetCanceled(capturedToken);

        await Task.Delay(100);

        capturedToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task SyncCommand_ClearsLogOnEachExecution()
    {
        var handler = MakeHandler(
            Result<SyncAllResult, Error>.Success(new SyncAllResult(1, 0, 0, 0, [])));

        var vm = CreateVm(handler);

        // First run — add an entry manually to simulate prior state
        vm.LogEntries.Add(new SyncProgressEntryViewModel(
            new SyncProgressEntry(DateTimeOffset.Now, "Old entry", SyncProgressSeverity.Info)));

        // Second run
        await vm.SyncCommand.Execute().FirstAsync();

        vm.LogEntries.Should().NotContain(e => e.Message == "Old entry");
    }

    [Fact]
    public async Task ProgressCallback_AddsEntriesToLog()
    {
        var handler = Substitute.For<ISyncAllCommandHandler>();
        handler.HandleAsync(
                Arg.Any<SyncAllCommand>(),
                Arg.Any<IProgress<SyncProgressEntry>>(),
                Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var p = ci.ArgAt<IProgress<SyncProgressEntry>>(1);
                p.Report(new SyncProgressEntry(DateTimeOffset.Now, "Step 1", SyncProgressSeverity.Info));
                p.Report(new SyncProgressEntry(DateTimeOffset.Now, "Step 2", SyncProgressSeverity.Warning));
                await Task.Yield();
                return Result<SyncAllResult, Error>.Success(new SyncAllResult(1, 0, 0, 0, []));
            });

        var vm = new SyncViewModel(handler, () => { }, ImmediateScheduler.Instance);

        await vm.SyncCommand.Execute().FirstAsync();

        vm.LogEntries.Should().Contain(e => e.Message == "Step 1");
        vm.LogEntries.Should().Contain(e => e.Message == "Step 2");
    }
}
