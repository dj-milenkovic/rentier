using FluentAssertions;
using Rentier.Application.DTOs;
using Rentier.Desktop.ViewModels;
using Xunit;

namespace Rentier.UnitTests;

public class MailboxItemViewModelTests
{
    private static MailboxDto MakeDto(
        Guid? id = null,
        string host = "imap.example.com",
        int port = 993,
        string username = "user",
        DateOnly? lastSyncDate = null,
        long? lastUid = null) =>
        new(
            Id: id ?? Guid.NewGuid(),
            Host: host,
            Port: port,
            Username: username,
            LastSyncDate: lastSyncDate,
            LastUid: lastUid);

    [Fact]
    public void From_MapsAllDtoProperties()
    {
        var id = Guid.NewGuid();
        var lastSync = new DateOnly(2025, 1, 15);
        var dto = MakeDto(
            id: id,
            host: "mail.server.com",
            port: 587,
            username: "testuser",
            lastSyncDate: lastSync,
            lastUid: 12345);

        var vm = MailboxItemViewModel.From(dto);

        vm.Id.Should().Be(id);
        vm.Host.Should().Be("mail.server.com");
        vm.Port.Should().Be(587);
        vm.Username.Should().Be("testuser");
        vm.LastSyncDate.Should().Be(lastSync);
        vm.LastUid.Should().Be(12345);
    }

    [Fact]
    public void DisplayName_CombinesUsernameHostPort()
    {
        var dto = MakeDto(
            username: "user",
            host: "imap.example.com",
            port: 993);

        var vm = MailboxItemViewModel.From(dto);

        vm.DisplayName.Should().Be("user @ imap.example.com:993");
    }

    [Fact]
    public void UpdateFrom_UpdatesAllProperties()
    {
        var originalId = Guid.NewGuid();
        var originalDto = MakeDto(
            id: originalId,
            host: "old.server.com",
            port: 143,
            username: "olduser",
            lastSyncDate: new DateOnly(2024, 1, 1),
            lastUid: 100);

        var vm = MailboxItemViewModel.From(originalDto);

        var newId = Guid.NewGuid();
        var newLastSync = new DateOnly(2025, 6, 15);
        var newDto = MakeDto(
            id: newId,
            host: "new.server.com",
            port: 993,
            username: "newuser",
            lastSyncDate: newLastSync,
            lastUid: 999);

        vm.UpdateFrom(newDto);

        vm.Id.Should().Be(newId);
        vm.Host.Should().Be("new.server.com");
        vm.Port.Should().Be(993);
        vm.Username.Should().Be("newuser");
        vm.LastSyncDate.Should().Be(newLastSync);
        vm.LastUid.Should().Be(999);
        vm.DisplayName.Should().Be("newuser @ new.server.com:993");
    }

    [Fact]
    public void From_WithNullOptionalFields_MapsCorrectly()
    {
        var dto = MakeDto(
            lastSyncDate: null,
            lastUid: null);

        var vm = MailboxItemViewModel.From(dto);

        vm.LastSyncDate.Should().BeNull();
        vm.LastUid.Should().BeNull();
    }
}
