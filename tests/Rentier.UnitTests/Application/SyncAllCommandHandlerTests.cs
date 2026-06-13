using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Rentier.Domain.ValueObjects;
using Rentier.UnitTests.Application.Common;
using Xunit;

namespace Rentier.UnitTests;

public class SyncAllCommandHandlerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ISyncMailboxCommandHandler MakeSyncMailboxHandler(
        Result<SyncResult, Error>? result = null)
    {
        var h = Substitute.For<ISyncMailboxCommandHandler>();
        h.HandleAsync(Arg.Any<SyncMailboxCommand>(), Arg.Any<IProgress<SyncProgressEntry>?>(), Arg.Any<CancellationToken>())
            .Returns(result ?? Result<SyncResult, Error>.Success(new SyncResult(0, [])));
        return h;
    }

    private static ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>> MakeProcessReportsHandler(
        Result<ProcessReportsResult, Error>? result = null)
    {
        var h = Substitute.For<ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>>();
        h.HandleAsync(Arg.Any<ProcessReportsCommand>(), Arg.Any<CancellationToken>())
            .Returns(result ?? Result<ProcessReportsResult, Error>.Success(new ProcessReportsResult(0, 0, 0, [])));
        return h;
    }

    private static SyncAllCommandHandler CreateHandler(
        ISyncMailboxCommandHandler? syncHandler = null,
        ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>? processHandler = null)
        => new(
            syncHandler ?? MakeSyncMailboxHandler(),
            processHandler ?? MakeProcessReportsHandler());

    private static IProgress<SyncProgressEntry> NoProgress() =>
        Substitute.For<IProgress<SyncProgressEntry>>();

    private static SyncAllCommand DefaultCommand() => new(SyncParameters.Default);

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_BothSucceed_ReturnsCombinedResult()
    {
        var syncHandler = MakeSyncMailboxHandler(
            Result<SyncResult, Error>.Success(new SyncResult(3, [])));
        var processHandler = MakeProcessReportsHandler(
            Result<ProcessReportsResult, Error>.Success(new ProcessReportsResult(2, 5, 0, [])));

        var handler = CreateHandler(syncHandler, processHandler);
        var result = await handler.HandleAsync(DefaultCommand(), NoProgress(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.MailboxesSynced.Should().Be(1);
        result.Value.AttachmentsDownloaded.Should().Be(3);
        result.Value.ReportsProcessed.Should().Be(5);
        result.Value.FilingsCreated.Should().Be(2);
        result.Value.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_SyncFails_StillProcessesReports()
    {
        var syncHandler = MakeSyncMailboxHandler(
            Result<SyncResult, Error>.Failure(new Error("SYNC_FAILED", "Connection refused")));
        var processHandler = MakeProcessReportsHandler(
            Result<ProcessReportsResult, Error>.Success(new ProcessReportsResult(1, 2, 0, [])));

        var handler = CreateHandler(syncHandler, processHandler);
        var result = await handler.HandleAsync(DefaultCommand(), NoProgress(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.FilingsCreated.Should().Be(1);
        result.Value.ReportsProcessed.Should().Be(2);

        await processHandler.Received(1)
            .HandleAsync(Arg.Any<ProcessReportsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SyncFails_ErrorInResult()
    {
        var syncHandler = MakeSyncMailboxHandler(
            Result<SyncResult, Error>.Failure(new Error("SYNC_FAILED", "Connection refused")));

        var handler = CreateHandler(syncHandler);
        var result = await handler.HandleAsync(DefaultCommand(), NoProgress(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Errors.Should().ContainSingle(e => e == "Connection refused");
        result.Value.MailboxesSynced.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_ProcessFails_ErrorInResult()
    {
        var processHandler = MakeProcessReportsHandler(
            Result<ProcessReportsResult, Error>.Failure(new Error("PROCESS_FAILED", "DB error")));

        var handler = CreateHandler(processHandler: processHandler);
        var result = await handler.HandleAsync(DefaultCommand(), NoProgress(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Errors.Should().ContainSingle(e => e == "DB error");
    }

    [Fact]
    public async Task HandleAsync_SyncSucceeds_AttachmentsFromReportsCreated()
    {
        var syncHandler = MakeSyncMailboxHandler(
            Result<SyncResult, Error>.Success(new SyncResult(7, [])));

        var handler = CreateHandler(syncHandler);
        var result = await handler.HandleAsync(DefaultCommand(), NoProgress(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.AttachmentsDownloaded.Should().Be(7);
    }

    [Fact]
    public async Task HandleAsync_BothSucceed_ProgressReportedInOrder()
    {
        var progress = new SynchronousProgress<SyncProgressEntry>();

        var syncHandler = MakeSyncMailboxHandler(
            Result<SyncResult, Error>.Success(new SyncResult(2, [])));
        var processHandler = MakeProcessReportsHandler(
            Result<ProcessReportsResult, Error>.Success(new ProcessReportsResult(1, 3, 0, [])));

        var handler = CreateHandler(syncHandler, processHandler);
        await handler.HandleAsync(DefaultCommand(), progress, TestContext.Current.CancellationToken);

        progress.Entries.Should().NotBeEmpty();
        progress.Entries[0].Message.Should().Be("Starting mailbox sync...");
        progress.Entries[0].Severity.Should().Be(SyncProgressSeverity.Info);
    }

    [Fact]
    public async Task HandleAsync_SyncErrors_ProgressReportedAsWarning()
    {
        var syncHandler = MakeSyncMailboxHandler(
            Result<SyncResult, Error>.Success(new SyncResult(0, ["mailbox error"])));

        var progress = new SynchronousProgress<SyncProgressEntry>();

        var handler = CreateHandler(syncHandler);
        await handler.HandleAsync(DefaultCommand(), progress, TestContext.Current.CancellationToken);

        progress.Entries.Should()
            .Contain(e => e.Message == "mailbox error" && e.Severity == SyncProgressSeverity.Warning);
    }

    [Fact]
    public async Task HandleAsync_ZeroReports_ReportsNoNewMessage()
    {
        var processHandler = MakeProcessReportsHandler(
            Result<ProcessReportsResult, Error>.Success(new ProcessReportsResult(0, 0, 0, [])));

        var progress = new SynchronousProgress<SyncProgressEntry>();

        var handler = CreateHandler(processHandler: processHandler);
        await handler.HandleAsync(DefaultCommand(), progress, TestContext.Current.CancellationToken);

        progress.Entries.Should().Contain(e => e.Message == "No new reports to process.");
    }

    [Fact]
    public async Task HandleAsync_CancellationToken_PassedThrough()
    {
        using var cts = new CancellationTokenSource();

        var syncHandler = Substitute.For<ISyncMailboxCommandHandler>();
        syncHandler.HandleAsync(Arg.Any<SyncMailboxCommand>(), Arg.Any<IProgress<SyncProgressEntry>?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SyncResult, Error>.Success(new SyncResult(0, [])));

        var processHandler = MakeProcessReportsHandler();

        var handler = CreateHandler(syncHandler, processHandler);
        await handler.HandleAsync(DefaultCommand(), NoProgress(), cts.Token);

        await syncHandler.Received(1)
            .HandleAsync(Arg.Any<SyncMailboxCommand>(), Arg.Any<IProgress<SyncProgressEntry>?>(), cts.Token);
        await processHandler.Received(1)
            .HandleAsync(Arg.Any<ProcessReportsCommand>(), cts.Token);
    }

    [Fact]
    public async Task HandleAsync_DelegatesMailboxSyncToSyncMailboxCommandHandler()
    {
        var syncHandler = Substitute.For<ISyncMailboxCommandHandler>();
        syncHandler.HandleAsync(Arg.Any<SyncMailboxCommand>(), Arg.Any<IProgress<SyncProgressEntry>?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SyncResult, Error>.Success(new SyncResult(0, [])));

        var handler = CreateHandler(syncHandler);
        await handler.HandleAsync(DefaultCommand(), NoProgress(), TestContext.Current.CancellationToken);

        await syncHandler.Received(1)
            .HandleAsync(Arg.Any<SyncMailboxCommand>(), Arg.Any<IProgress<SyncProgressEntry>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PassesProgressToSyncMailboxCommand()
    {
        IProgress<SyncProgressEntry>? capturedProgress = null;
        var syncHandler = Substitute.For<ISyncMailboxCommandHandler>();
        syncHandler
            .HandleAsync(
                Arg.Any<SyncMailboxCommand>(),
                Arg.Do<IProgress<SyncProgressEntry>?>(p => capturedProgress = p),
                Arg.Any<CancellationToken>())
            .Returns(Result<SyncResult, Error>.Success(new SyncResult(0, [])));

        var progress = Substitute.For<IProgress<SyncProgressEntry>>();
        var handler = CreateHandler(syncHandler);

        await handler.HandleAsync(DefaultCommand(), progress, TestContext.Current.CancellationToken);

        capturedProgress.Should().BeSameAs(progress);
    }

    // ── T009: IProgress passthrough to ProcessReportsCommand (US3) ───────────

    [Fact]
    public async Task HandleAsync_PassesProgressToProcessReportsCommand()
    {
        // T009(a): ProcessReportsCommand constructed inside HandleAsync has Progress set
        // to the same IProgress<SyncProgressEntry> instance passed to HandleAsync.
        ProcessReportsCommand? capturedCommand = null;

        var processHandler = Substitute.For<ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>>();
        processHandler
            .HandleAsync(Arg.Do<ProcessReportsCommand>(cmd => capturedCommand = cmd), Arg.Any<CancellationToken>())
            .Returns(Result<ProcessReportsResult, Error>.Success(new ProcessReportsResult(0, 0, 0, [])));

        var progress = Substitute.For<IProgress<SyncProgressEntry>>();

        var handler = CreateHandler(processHandler: processHandler);
        await handler.HandleAsync(DefaultCommand(), progress, TestContext.Current.CancellationToken);

        capturedCommand.Should().NotBeNull();
        capturedCommand!.Progress.Should().BeSameAs(progress);
    }

    [Fact]
    public async Task HandleAsync_AggregateLineAppearsAfterPerReportLines()
    {
        var progress = new SynchronousProgress<SyncProgressEntry>();

        var processHandler = MakeProcessReportsHandler(
            Result<ProcessReportsResult, Error>.Success(new ProcessReportsResult(3, 2, 0, [])));

        var handler = CreateHandler(processHandler: processHandler);
        await handler.HandleAsync(DefaultCommand(), progress, TestContext.Current.CancellationToken);

        var aggregateLine = progress.Entries.LastOrDefault(e =>
            e.Message.StartsWith("Processed") && e.Message.Contains("report(s)"));

        aggregateLine.Should().NotBeNull("aggregate summary line must still appear");
        aggregateLine!.Severity.Should().Be(SyncProgressSeverity.Info);
        aggregateLine.Message.Should().Be("Processed 2 report(s), created 3 filing(s).");
    }
}
