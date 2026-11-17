using FluentAssertions;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.Sync;
using Xunit;

namespace Rentier.Infrastructure.Tests;

/// <summary>
/// Pure tests for <see cref="ImapMailboxSyncService.NextCursor"/>: the mailbox cursor must never
/// move past a message that failed, because an incremental sync only searches UIDs above it.
/// </summary>
public class ImapCursorAdvanceTests
{
    private static readonly DateOnly TODAY = new(2026, 11, 17);
    private static readonly DateOnly LAST_SYNC = new(2026, 11, 1);

    [Fact]
    public void NextCursor_NoFailures_AdvancesToHighestProcessedUid()
    {
        var next = ImapMailboxSyncService.NextCursor(
            new MailboxCursor.SyncedTo(LAST_SYNC, 50), TODAY, maxUid: 120, lowestFailedUid: null, anyImporterIncomplete: false);

        next.Should().Be(new MailboxCursor.SyncedTo(TODAY, 120));
    }

    [Fact]
    public void NextCursor_NoMessagesProcessed_KeepsPreviousUid()
    {
        var next = ImapMailboxSyncService.NextCursor(
            new MailboxCursor.SyncedTo(LAST_SYNC, 50), TODAY, maxUid: null, lowestFailedUid: null, anyImporterIncomplete: false);

        next.Should().Be(new MailboxCursor.SyncedTo(TODAY, 50));
    }

    [Fact]
    public void NextCursor_LowerUidFailedHigherSucceeded_CapsCursorJustBelowFailedUid()
    {
        var next = ImapMailboxSyncService.NextCursor(
            new MailboxCursor.SyncedTo(LAST_SYNC, 50), TODAY, maxUid: 101, lowestFailedUid: 100, anyImporterIncomplete: false);

        next.Should().Be(new MailboxCursor.SyncedTo(TODAY, 99));
    }

    [Fact]
    public void NextCursor_FailedUidAboveHighestProcessed_AdvancesToHighestProcessed()
    {
        var next = ImapMailboxSyncService.NextCursor(
            new MailboxCursor.SyncedTo(LAST_SYNC, 50), TODAY, maxUid: 80, lowestFailedUid: 90, anyImporterIncomplete: false);

        next.Should().Be(new MailboxCursor.SyncedTo(TODAY, 80));
    }

    [Fact]
    public void NextCursor_FirstSearchedUidFailed_DoesNotMoveCursorBackwards()
    {
        var next = ImapMailboxSyncService.NextCursor(
            new MailboxCursor.SyncedTo(LAST_SYNC, 50), TODAY, maxUid: 60, lowestFailedUid: 51, anyImporterIncomplete: false);

        next.Should().Be(new MailboxCursor.SyncedTo(TODAY, 50));
    }

    [Fact]
    public void NextCursor_NeverSyncedAndMessageFailed_SetsUidCursorBelowFailure()
    {
        // A date-only cursor would skip the failed message's delivery date; a UID cursor retries it.
        var next = ImapMailboxSyncService.NextCursor(
            MailboxCursor.NeverSynced.Instance, TODAY, maxUid: 12, lowestFailedUid: 7, anyImporterIncomplete: false);

        next.Should().Be(new MailboxCursor.SyncedTo(TODAY, 6));
    }

    [Fact]
    public void NextCursor_ImporterFailedAsAWhole_KeepsCurrentCursor()
    {
        var next = ImapMailboxSyncService.NextCursor(
            new MailboxCursor.SyncedTo(LAST_SYNC, 50), TODAY, maxUid: 120, lowestFailedUid: null, anyImporterIncomplete: true);

        next.Should().BeNull();
    }
}
