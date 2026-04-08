using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Handlers;
using Rentier.Application.Interfaces;
using Xunit;

namespace Rentier.Application.Tests;

public class SyncAllCommandHandlerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>> MakeSyncMailboxHandler(
        Result<SyncResult, Error>? result = null)
    {
        var h = Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>();
        h.HandleAsync(Arg.Any<SyncMailboxCommand>(), Arg.Any<CancellationToken>())
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
        ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>? syncHandler = null,
        ICommandHandler<ProcessReportsCommand, Result<ProcessReportsResult, Error>>? processHandler = null)
        => new(
            syncHandler ?? MakeSyncMailboxHandler(),
            processHandler ?? MakeProcessReportsHandler());

    private static IProgress<SyncProgressEntry> NoProgress() =>
        Substitute.For<IProgress<SyncProgressEntry>>();

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_BothSucceed_ReturnsCombinedResult()
    {
        var syncHandler = MakeSyncMailboxHandler(
            Result<SyncResult, Error>.Success(new SyncResult(3, [])));
        var processHandler = MakeProcessReportsHandler(
            Result<ProcessReportsResult, Error>.Success(new ProcessReportsResult(2, 5, 0, [])));

        var handler = CreateHandler(syncHandler, processHandler);
        var result = await handler.HandleAsync(new SyncAllCommand(), NoProgress());

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
        var result = await handler.HandleAsync(new SyncAllCommand(), NoProgress());

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
        var result = await handler.HandleAsync(new SyncAllCommand(), NoProgress());

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
        var result = await handler.HandleAsync(new SyncAllCommand(), NoProgress());

        result.IsSuccess.Should().BeTrue();
        result.Value.Errors.Should().ContainSingle(e => e == "DB error");
    }

    [Fact]
    public async Task HandleAsync_SyncSucceeds_AttachmentsFromReportsCreated()
    {
        var syncHandler = MakeSyncMailboxHandler(
            Result<SyncResult, Error>.Success(new SyncResult(7, [])));

        var handler = CreateHandler(syncHandler);
        var result = await handler.HandleAsync(new SyncAllCommand(), NoProgress());

        result.IsSuccess.Should().BeTrue();
        result.Value.AttachmentsDownloaded.Should().Be(7);
    }

    [Fact]
    public async Task HandleAsync_BothSucceed_ProgressReportedInOrder()
    {
        var reported = new List<SyncProgressEntry>();
        var progress = new Progress<SyncProgressEntry>(e => reported.Add(e));

        var syncHandler = MakeSyncMailboxHandler(
            Result<SyncResult, Error>.Success(new SyncResult(2, [])));
        var processHandler = MakeProcessReportsHandler(
            Result<ProcessReportsResult, Error>.Success(new ProcessReportsResult(1, 3, 0, [])));

        var handler = CreateHandler(syncHandler, processHandler);
        await handler.HandleAsync(new SyncAllCommand(), progress);

        // Give Progress<T> callbacks a chance to fire (they may dispatch on the thread pool)
        await Task.Delay(50);

        reported.Should().NotBeEmpty();
        reported[0].Message.Should().Be("Starting mailbox sync...");
        reported[0].Severity.Should().Be(SyncProgressSeverity.Info);
    }

    [Fact]
    public async Task HandleAsync_SyncErrors_ProgressReportedAsWarning()
    {
        var syncHandler = MakeSyncMailboxHandler(
            Result<SyncResult, Error>.Success(new SyncResult(0, ["mailbox error"])));

        var reported = new List<SyncProgressEntry>();
        var progress = new Progress<SyncProgressEntry>(e => reported.Add(e));

        var handler = CreateHandler(syncHandler);
        await handler.HandleAsync(new SyncAllCommand(), progress);

        await Task.Delay(50);

        reported.Should().Contain(e => e.Message == "mailbox error" && e.Severity == SyncProgressSeverity.Warning);
    }

    [Fact]
    public async Task HandleAsync_ZeroReports_ReportsNoNewMessage()
    {
        var processHandler = MakeProcessReportsHandler(
            Result<ProcessReportsResult, Error>.Success(new ProcessReportsResult(0, 0, 0, [])));

        var reported = new List<SyncProgressEntry>();
        var progress = new Progress<SyncProgressEntry>(e => reported.Add(e));

        var handler = CreateHandler(processHandler: processHandler);
        await handler.HandleAsync(new SyncAllCommand(), progress);

        await Task.Delay(50);

        reported.Should().Contain(e => e.Message == "No new reports to process.");
    }

    [Fact]
    public async Task HandleAsync_CancellationToken_PassedThrough()
    {
        using var cts = new CancellationTokenSource();

        var syncHandler = Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>();
        syncHandler.HandleAsync(Arg.Any<SyncMailboxCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SyncResult, Error>.Success(new SyncResult(0, [])));

        var processHandler = MakeProcessReportsHandler();

        var handler = CreateHandler(syncHandler, processHandler);
        await handler.HandleAsync(new SyncAllCommand(), NoProgress(), cts.Token);

        await syncHandler.Received(1)
            .HandleAsync(Arg.Any<SyncMailboxCommand>(), cts.Token);
        await processHandler.Received(1)
            .HandleAsync(Arg.Any<ProcessReportsCommand>(), cts.Token);
    }

    [Fact]
    public async Task HandleAsync_PassesProgressViaCommandConstructor_NotAsMethodArg()
    {
        var syncHandler = Substitute.For<ICommandHandler<SyncMailboxCommand, Result<SyncResult, Error>>>();
        syncHandler.HandleAsync(Arg.Any<SyncMailboxCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SyncResult, Error>.Success(new SyncResult(0, [])));

        var handler = CreateHandler(syncHandler);
        await handler.HandleAsync(new SyncAllCommand(), NoProgress());

        await syncHandler.Received(1)
            .HandleAsync(
                Arg.Is<SyncMailboxCommand>(c => c.Progress != null),
                Arg.Any<CancellationToken>());
    }
}
