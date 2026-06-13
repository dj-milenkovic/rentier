using FluentAssertions;
using NSubstitute;
using Rentier.Application.Handlers;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Xunit;

namespace Rentier.UnitTests.Application;

public class GetMailboxesQueryHandlerTests
{
    private readonly IMailboxRepository _repo = Substitute.For<IMailboxRepository>();
    private readonly GetMailboxesQueryHandler _handler;

    public GetMailboxesQueryHandlerTests()
    {
        _handler = new GetMailboxesQueryHandler(_repo);
    }

    [Fact]
    public async Task HandleAsync_NoMailboxes_ReturnsEmptyList()
    {
        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Mailbox>());

        var result = await _handler.HandleAsync(new GetMailboxesQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WithTwoMailboxes_ReturnsMappedDtos()
    {
        var m1 = Mailbox.Create("imap.host1.com", 993, "user1@example.com");
        var m2 = Mailbox.Create("imap.host2.com", 143, "user2@example.com");

        _repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Mailbox> { m1, m2 });

        var result = await _handler.HandleAsync(new GetMailboxesQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var dto1 = result.Value.First(d => d.Id == m1.Id);
        dto1.Host.Should().Be("imap.host1.com");
        dto1.Port.Should().Be(993);
        dto1.Username.Should().Be("user1@example.com");

        var dto2 = result.Value.First(d => d.Id == m2.Id);
        dto2.Host.Should().Be("imap.host2.com");
        dto2.Port.Should().Be(143);
    }
}
