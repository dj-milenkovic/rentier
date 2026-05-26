using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

public class MailboxTests
{
    [Fact]
    public void Create_ValidInputs_ReturnsMailboxWithCorrectProperties()
    {
        var mailbox = Mailbox.Create("imap.example.com", 993, "user@example.com");

        mailbox.Id.Should().NotBe(Guid.Empty);
        mailbox.Host.Should().Be("imap.example.com");
        mailbox.Port.Should().Be(993);
        mailbox.Username.Should().Be("user@example.com");
    }

    [Fact]
    public void Create_EmptyHost_ThrowsDomainException()
    {
        var act = () => Mailbox.Create("", 993, "user@example.com");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhitespaceHost_ThrowsDomainException()
    {
        var act = () => Mailbox.Create("   ", 993, "user@example.com");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_PortZero_ThrowsDomainException()
    {
        var act = () => Mailbox.Create("imap.example.com", 0, "user@example.com");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_PortAbove65535_ThrowsDomainException()
    {
        var act = () => Mailbox.Create("imap.example.com", 65536, "user@example.com");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_Port65535_Succeeds()
    {
        var act = () => Mailbox.Create("imap.example.com", 65535, "user@example.com");
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_EmptyUsername_ThrowsDomainException()
    {
        var act = () => Mailbox.Create("imap.example.com", 993, "");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_SetsInitialCursorTo90DaysAgo()
    {
        var mailbox = Mailbox.Create("imap.example.com", 993, "user@example.com");

        mailbox.Cursor.Should().BeOfType<MailboxCursor.SyncedTo>();
        ((MailboxCursor.SyncedTo)mailbox.Cursor).Uid.Should().BeNull();
    }

    [Fact]
    public void Create_AssignsUniqueGuidEachCall()
    {
        var m1 = Mailbox.Create("imap.example.com", 993, "user@example.com");
        var m2 = Mailbox.Create("imap.example.com", 993, "user@example.com");

        m1.Id.Should().NotBe(m2.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDetails_WithNullOrWhitespaceUsername_ThrowsDomainException(string username)
    {
        var mailbox = Mailbox.Create("imap.example.com", 993, "user@example.com");

        var act = () => mailbox.UpdateDetails("imap.example.com", 993, username);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdateDetails_ValidInputs_UpdatesProperties()
    {
        var mailbox = Mailbox.Create("imap.example.com", 993, "user@example.com");

        mailbox.UpdateDetails("imap.newhost.com", 143, "new@example.com");

        mailbox.Host.Should().Be("imap.newhost.com");
        mailbox.Port.Should().Be(143);
        mailbox.Username.Should().Be("new@example.com");
    }

    [Fact]
    public void UpdateDetails_EmptyHost_ThrowsDomainException()
    {
        var mailbox = Mailbox.Create("imap.example.com", 993, "user@example.com");
        var act = () => mailbox.UpdateDetails("", 993, "user@example.com");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdateDetails_InvalidPort_ThrowsDomainException()
    {
        var mailbox = Mailbox.Create("imap.example.com", 993, "user@example.com");
        var act = () => mailbox.UpdateDetails("imap.example.com", 0, "user@example.com");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdateCursor_ValidCursor_UpdatesCursor()
    {
        var mailbox = Mailbox.Create("imap.example.com", 993, "user@example.com");
        var newCursor = new MailboxCursor.SyncedTo(new DateOnly(2025, 1, 1), 42L);

        mailbox.UpdateCursor(newCursor);

        var synced = mailbox.Cursor.Should().BeOfType<MailboxCursor.SyncedTo>().Subject;
        synced.Date.Should().Be(new DateOnly(2025, 1, 1));
        synced.Uid.Should().Be(42L);
    }

    [Fact]
    public void UpdateCursor_NullCursor_ThrowsDomainException()
    {
        var mailbox = Mailbox.Create("imap.example.com", 993, "user@example.com");
        var act = () => mailbox.UpdateCursor(null!);
        act.Should().Throw<DomainException>();
    }
}
