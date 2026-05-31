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
using Rentier.Domain.Enums;
using Xunit;

namespace Rentier.UnitTests;

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
        ISyncAllCommandHandler? handler = null)
        => new(
            handler ?? MakeHandler(),
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

        var vm = new SyncViewModel(handler);

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

        var vm = new SyncViewModel(handler, ImmediateScheduler.Instance);

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
    public async Task SyncCommand_OnSuccessWithErrors_SetsHasErrors()
    {
        var handler = MakeHandler(
            Result<SyncAllResult, Error>.Success(new SyncAllResult(1, 0, 0, 0, ["some error"])));

        var vm = CreateVm(handler);

        await vm.SyncCommand.Execute().FirstAsync();

        vm.HasErrors.Should().BeTrue();
    }

    [Fact]
    public async Task SyncCommand_OnSuccessNoErrors_LogEntriesPreservedForInspection()
    {
        var handler = Substitute.For<ISyncAllCommandHandler>();
        handler.HandleAsync(
                Arg.Any<SyncAllCommand>(),
                Arg.Any<IProgress<SyncProgressEntry>>(),
                Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var p = ci.ArgAt<IProgress<SyncProgressEntry>>(1);
                p.Report(new SyncProgressEntry(DateTimeOffset.Now, "Downloading email 1/2: Dividend notice", SyncProgressSeverity.Info));
                p.Report(new SyncProgressEntry(DateTimeOffset.Now, "Downloading email 2/2: Interest notice", SyncProgressSeverity.Info));
                await Task.Yield();
                return Result<SyncAllResult, Error>.Success(new SyncAllResult(1, 2, 1, 1, []));
            });

        var vm = CreateVm(handler);

        await vm.SyncCommand.Execute().FirstAsync();

        vm.LogEntries.Should().HaveCount(2);
        vm.LogEntries.Should().Contain(e => e.Message.Contains("Dividend notice"));
        vm.LogEntries.Should().Contain(e => e.Message.Contains("Interest notice"));
    }

    [Fact]
    public async Task SyncCommand_OnSuccessNoErrors_SummaryMessageRemainsVisible()
    {
        var handler = MakeHandler(
            Result<SyncAllResult, Error>.Success(new SyncAllResult(1, 3, 2, 2, [])));

        var vm = CreateVm(handler);

        await vm.SyncCommand.Execute().FirstAsync();

        vm.SummaryMessage.Should().NotBeNullOrEmpty();
        vm.SummaryMessage.Should().Contain("2 filing(s) created");
        vm.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task SyncCommand_AfterSuccess_IsRunningFalseAndCommandCanRerun()
    {
        var handler = MakeHandler(
            Result<SyncAllResult, Error>.Success(new SyncAllResult(1, 0, 0, 0, [])));

        var vm = CreateVm(handler);
        using var _ = vm.Activator.Activate();

        await vm.SyncCommand.Execute().FirstAsync();

        vm.IsRunning.Should().BeFalse();

        bool? canExecute = null;
        vm.SyncCommand.CanExecute.Subscribe(v => canExecute = v);
        canExecute.Should().BeTrue("user should be able to re-run sync from the same page");
    }

    [Fact]
    public async Task SyncCommand_OnFailure_ErrorMessageAndSummaryVisibleForInspection()
    {
        var handler = MakeHandler(
            Result<SyncAllResult, Error>.Failure(new Error("SYNC_FAILED", "IMAP connection refused")));

        var vm = CreateVm(handler);

        await vm.SyncCommand.Execute().FirstAsync();

        vm.ErrorMessage.Should().Be("IMAP connection refused");
        vm.SummaryMessage.Should().NotBeNullOrEmpty();
        vm.HasErrors.Should().BeTrue();
        vm.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task SyncCommand_OnSuccessWithErrors_SummaryAndErrorsBothVisible()
    {
        var handler = MakeHandler(
            Result<SyncAllResult, Error>.Success(new SyncAllResult(1, 2, 1, 1, ["Exchange rate not found for AAPL"])));

        var vm = CreateVm(handler);

        await vm.SyncCommand.Execute().FirstAsync();

        vm.HasErrors.Should().BeTrue();
        vm.SummaryMessage.Should().Contain("1 filing(s) created");
        vm.ErrorSummaryMessage.Should().NotBeNullOrEmpty();
        vm.IsRunning.Should().BeFalse();
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

        var vm = new SyncViewModel(handler);
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

        var vm = new SyncViewModel(handler, ImmediateScheduler.Instance);

        await vm.SyncCommand.Execute().FirstAsync();

        vm.LogEntries.Should().Contain(e => e.Message == "Step 1");
        vm.LogEntries.Should().Contain(e => e.Message == "Step 2");
    }

    // ── Sync mode derived property tests ────────────────────────────────────

    [Fact]
    public void SelectedSyncMode_Default_IsIncremental()
    {
        var vm = CreateVm();

        vm.SelectedSyncMode.Should().Be(SyncMode.Incremental);
    }

    [Fact]
    public void SelectedSyncMode_ChangeToIncremental_SetsIsReplayModeFalse()
    {
        var vm = CreateVm();
        vm.SelectedSyncMode = SyncMode.FullReplay;

        vm.SelectedSyncMode = SyncMode.Incremental;

        vm.IsReplayMode.Should().BeFalse();
    }

    [Fact]
    public void SelectedSyncMode_ChangeToReplayFromDate_SetsIsReplayFromDateModeTrue()
    {
        var vm = CreateVm();

        vm.SelectedSyncMode = SyncMode.ReplayFromDate;

        vm.IsReplayFromDateMode.Should().BeTrue();
    }

    [Fact]
    public void SelectedSyncMode_ChangeToReplayFromDate_SetsIsReplayModeTrue()
    {
        var vm = CreateVm();

        vm.SelectedSyncMode = SyncMode.ReplayFromDate;

        vm.IsReplayMode.Should().BeTrue();
    }

    [Fact]
    public void SelectedSyncMode_ChangeToFullReplay_SetsIsFullReplayModeTrue()
    {
        var vm = CreateVm();

        vm.SelectedSyncMode = SyncMode.FullReplay;

        vm.IsFullReplayMode.Should().BeTrue();
    }

    [Fact]
    public void SelectedSyncMode_ChangeToFullReplay_SetsIsReplayModeTrue()
    {
        var vm = CreateVm();

        vm.SelectedSyncMode = SyncMode.FullReplay;

        vm.IsReplayMode.Should().BeTrue();
    }

    [Fact]
    public void SelectedSyncMode_ChangeToFullReplay_SetsIsReplayFromDateModeFalse()
    {
        var vm = CreateVm();

        vm.SelectedSyncMode = SyncMode.FullReplay;

        vm.IsReplayFromDateMode.Should().BeFalse();
    }

    // ── ImpactSummary tests ──────────────────────────────────────────────────

    [Fact]
    public void ImpactSummary_IncrementalSkipExisting_ShowsCorrectText()
    {
        var vm = CreateVm();
        // Default: Incremental + SkipExisting

        vm.ImpactSummary.Should().Be("Fetches new emails since last sync. Duplicates are skipped.");
    }

    [Fact]
    public void ImpactSummary_FullReplaySkipExisting_ShowsAllEmailsText()
    {
        var vm = CreateVm();
        vm.SelectedSyncMode = SyncMode.FullReplay;
        vm.SelectedDuplicateStrategy = DuplicateStrategy.SkipExisting;

        vm.ImpactSummary.Should().Be("Fetches ALL emails in the mailbox. Duplicates are skipped.");
    }

    [Fact]
    public void ImpactSummary_ReplayFromDateWithDate_IncludesFormattedDate()
    {
        var vm = CreateVm();
        vm.SelectedSyncMode = SyncMode.ReplayFromDate;
        vm.ReplayFromDateOffset = new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero);

        vm.ImpactSummary.Should().Contain("2024-03-15");
    }

    // ── ValidationError tests ────────────────────────────────────────────────

    [Fact]
    public void ValidationError_WhenIncrementalMode_IsNull()
    {
        var vm = CreateVm();
        // Default: Incremental — no date required

        vm.ValidationError.Should().BeNull();
    }

    [Fact]
    public void ValidationError_WhenFullReplayMode_IsNull()
    {
        var vm = CreateVm();

        vm.SelectedSyncMode = SyncMode.FullReplay;

        vm.ValidationError.Should().BeNull();
    }

    [Fact]
    public void ValidationError_WhenReplayFromDateModeAndNoDate_ReturnsError()
    {
        var vm = CreateVm();
        vm.SelectedSyncMode = SyncMode.ReplayFromDate;
        vm.ReplayFromDateOffset = null;

        vm.ValidationError.Should().Be("Replay date is required for this mode");
    }

    [Fact]
    public void ValidationError_WhenReplayFromDateModeAndFutureDate_ReturnsError()
    {
        var vm = CreateVm();
        vm.SelectedSyncMode = SyncMode.ReplayFromDate;
        vm.ReplayFromDateOffset = DateTimeOffset.UtcNow.AddDays(1);

        vm.ValidationError.Should().Be("Replay date cannot be in the future");
    }

    [Fact]
    public void ValidationError_WhenReplayFromDateModeAndValidPastDate_IsNull()
    {
        var vm = CreateVm();
        vm.SelectedSyncMode = SyncMode.ReplayFromDate;
        vm.ReplayFromDateOffset = DateTimeOffset.UtcNow.AddDays(-1);

        vm.ValidationError.Should().BeNull();
    }

    // ── SyncCommand.CanExecute tests ─────────────────────────────────────────

    [Fact]
    public void SyncCommand_CanExecute_FalseWhenValidationError()
    {
        var vm = CreateVm();
        bool? canExecute = null;
        vm.SyncCommand.CanExecute.Subscribe(v => canExecute = v);

        // Switching to ReplayFromDate with no date set triggers a validation error
        vm.SelectedSyncMode = SyncMode.ReplayFromDate;

        canExecute.Should().BeFalse();
    }

    [Fact]
    public void SyncCommand_CanExecute_TrueWhenNoValidationError()
    {
        var vm = CreateVm();
        // Default: Incremental — no ValidationError

        bool? canExecute = null;
        vm.SyncCommand.CanExecute.Subscribe(v => canExecute = v);

        canExecute.Should().BeTrue();
    }

    // ── ReplayFromDate computed property tests ───────────────────────────────

    [Fact]
    public void ReplayFromDateOffset_WhenSet_UpdatesReplayFromDate()
    {
        var vm = CreateVm();
        var offset = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero);

        vm.ReplayFromDateOffset = offset;

        vm.ReplayFromDate.Should().Be(new DateOnly(2024, 6, 15));
    }

    [Fact]
    public void ReplayFromDateOffset_WhenNull_ReplayFromDateIsNull()
    {
        var vm = CreateVm();

        vm.ReplayFromDateOffset = null;

        vm.ReplayFromDate.Should().BeNull();
    }
}
