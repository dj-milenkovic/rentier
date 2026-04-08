using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;
using Xunit;

namespace Rentier.Domain.Tests;

public class MailboxTests
{
    private static readonly DateOnly TestDate = new(2024, 1, 1);

    [Fact]
    public void Create_ValidInputs_ReturnsMailboxWithCorrectProperties()
    {
        var mailbox = Mailbox.Create("imap.example.com", 993, "user@example.com", TestDate);

        mailbox.Id.Should().NotBe(Guid.Empty);
        mailbox.Host.Should().Be("imap.example.com");
        mailbox.Port.Should().Be(993);
        mailbox.Username.Should().Be("user@example.com");
        mailbox.InitialSyncDate.Should().Be(TestDate);
    }

    [Fact]
    public void Create_EmptyHost_ThrowsDomainException()
    {
        var act = () => Mailbox.Create("", 993, "user@example.com", TestDate);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhitespaceHost_ThrowsDomainException()
    {
        var act = () => Mailbox.Create("   ", 993, "user@example.com", TestDate);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_PortZero_ThrowsDomainException()
    {
        var act = () => Mailbox.Create("imap.example.com", 0, "user@example.com", TestDate);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_PortAbove65535_ThrowsDomainException()
    {
        var act = () => Mailbox.Create("imap.example.com", 65536, "user@example.com", TestDate);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_Port65535_Succeeds()
    {
        var act = () => Mailbox.Create("imap.example.com", 65535, "user@example.com", TestDate);
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_EmptyUsername_ThrowsDomainException()
    {
        var act = () => Mailbox.Create("imap.example.com", 993, "", TestDate);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_SetsInitialCursorFromInitialSyncDate()
    {
        var mailbox = Mailbox.Create("imap.example.com", 993, "user@example.com", TestDate);

        mailbox.Cursor.LastSyncDate.Should().Be(TestDate);
        mailbox.Cursor.LastUid.Should().BeNull();
    }

    [Fact]
    public void Create_AssignsUniqueGuidEachCall()
    {
        var m1 = Mailbox.Create("imap.example.com", 993, "user@example.com", TestDate);
        var m2 = Mailbox.Create("imap.example.com", 993, "user@example.com", TestDate);

        m1.Id.Should().NotBe(m2.Id);
    }

    [Fact]
    public void UpdateDetails_ValidInputs_UpdatesAllMutableFields()
    {
        var mailbox = Mailbox.Create("imap.example.com", 993, "user@example.com", TestDate);
        var newDate = new DateOnly(2025, 6, 15);

        mailbox.UpdateDetails("imap.newhost.com", 143, "new@example.com", newDate);

        mailbox.Host.Should().Be("imap.newhost.com");
        mailbox.Port.Should().Be(143);
        mailbox.Username.Should().Be("new@example.com");
        mailbox.InitialSyncDate.Should().Be(newDate);
    }

    [Fact]
    public void UpdateDetails_EmptyHost_ThrowsDomainException()
    {
        var mailbox = Mailbox.Create("imap.example.com", 993, "user@example.com", TestDate);
        var act = () => mailbox.UpdateDetails("", 993, "user@example.com", TestDate);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdateDetails_InvalidPort_ThrowsDomainException()
    {
        var mailbox = Mailbox.Create("imap.example.com", 993, "user@example.com", TestDate);
        var act = () => mailbox.UpdateDetails("imap.example.com", 0, "user@example.com", TestDate);
        act.Should().Throw<DomainException>();
    }
}
