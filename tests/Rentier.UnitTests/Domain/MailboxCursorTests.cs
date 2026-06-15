using FluentAssertions;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

public class MailboxCursorTests
{
    // ── NeverSynced ───────────────────────────────────────────────────────────

    [Fact]
    public void NeverSynced_Instance_IsSingleton()
    {
        var a = MailboxCursor.NeverSynced.Instance;
        var b = MailboxCursor.NeverSynced.Instance;

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void NeverSynced_IsMailboxCursor()
    {
        MailboxCursor.NeverSynced.Instance.Should().BeAssignableTo<MailboxCursor>();
    }

    [Fact]
    public void NeverSynced_PatternMatch_IsNeverSynced()
    {
        MailboxCursor cursor = MailboxCursor.NeverSynced.Instance;

        var isNever = cursor is MailboxCursor.NeverSynced;

        isNever.Should().BeTrue();
    }

    [Fact]
    public void NeverSynced_IsNotSyncedTo()
    {
        MailboxCursor cursor = MailboxCursor.NeverSynced.Instance;

        var isSynced = cursor is MailboxCursor.SyncedTo;

        isSynced.Should().BeFalse();
    }

    // ── SyncedTo ──────────────────────────────────────────────────────────────

    [Fact]
    public void SyncedTo_SetsDateAndUid()
    {
        var date = new DateOnly(2024, 6, 17);
        var uid = 42L;

        var cursor = new MailboxCursor.SyncedTo(date, uid);

        cursor.Date.Should().Be(date);
        cursor.Uid.Should().Be(uid);
    }

    [Fact]
    public void SyncedTo_NullUid_IsAllowed()
    {
        var date = new DateOnly(2024, 6, 17);

        var cursor = new MailboxCursor.SyncedTo(date, null);

        cursor.Date.Should().Be(date);
        cursor.Uid.Should().BeNull();
    }

    [Fact]
    public void SyncedTo_IsMailboxCursor()
    {
        var cursor = new MailboxCursor.SyncedTo(new DateOnly(2024, 1, 1), null);

        cursor.Should().BeAssignableTo<MailboxCursor>();
    }

    [Fact]
    public void SyncedTo_PatternMatch_IsSyncedTo()
    {
        MailboxCursor cursor = new MailboxCursor.SyncedTo(new DateOnly(2024, 1, 1), 99L);

        var isSynced = cursor is MailboxCursor.SyncedTo;

        isSynced.Should().BeTrue();
    }

    [Fact]
    public void SyncedTo_PatternMatch_ExtractsProperties()
    {
        var date = new DateOnly(2024, 6, 17);
        MailboxCursor cursor = new MailboxCursor.SyncedTo(date, 55L);

        DateOnly? extractedDate = null;
        long? extractedUid = null;
        if (cursor is MailboxCursor.SyncedTo s)
        {
            extractedDate = s.Date;
            extractedUid = s.Uid;
        }

        extractedDate.Should().Be(date);
        extractedUid.Should().Be(55L);
    }

    // ── Record equality ───────────────────────────────────────────────────────

    [Fact]
    public void NeverSynced_TwoInstances_AreEqual()
    {
        // Record equality: two NeverSynced instances with same data are equal
        // (though Instance is singleton, record equality should still hold)
        MailboxCursor a = MailboxCursor.NeverSynced.Instance;
        MailboxCursor b = MailboxCursor.NeverSynced.Instance;

        a.Should().Be(b);
    }

    [Fact]
    public void SyncedTo_SameValues_AreEqual()
    {
        var date = new DateOnly(2024, 6, 17);
        var a = new MailboxCursor.SyncedTo(date, 10L);
        var b = new MailboxCursor.SyncedTo(date, 10L);

        a.Should().Be(b);
    }

    [Fact]
    public void SyncedTo_DifferentDate_AreNotEqual()
    {
        var a = new MailboxCursor.SyncedTo(new DateOnly(2024, 1, 1), 10L);
        var b = new MailboxCursor.SyncedTo(new DateOnly(2024, 6, 1), 10L);

        a.Should().NotBe(b);
    }

    [Fact]
    public void NeverSynced_AndSyncedTo_AreNotEqual()
    {
        MailboxCursor a = MailboxCursor.NeverSynced.Instance;
        MailboxCursor b = new MailboxCursor.SyncedTo(new DateOnly(2024, 1, 1), null);

        a.Should().NotBe(b);
    }
}
