using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;

namespace Rentier.Infrastructure.Tests;

[Trait("Category", "Integration")]
public class MailboxRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private MailboxRepository _repository = null!;

    [Fact]
    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        _repository = new MailboxRepository(_context);
    }

    [Fact]
    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static Mailbox MakeMailbox(string host = "imap.example.com")
        => Mailbox.Create(host, 993, "user@example.com");

    [Fact]
    public async Task GetAllAsync_EmptyDb_ReturnsEmptyList()
    {
        var result = await _repository.GetAllAsync(TestContext.Current.CancellationToken);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_NewMailbox_PersistsToDb()
    {
        var mailbox = MakeMailbox();
        await _repository.AddAsync(mailbox, TestContext.Current.CancellationToken);

        var all = await _repository.GetAllAsync(TestContext.Current.CancellationToken);
        all.Should().HaveCount(1);
        all[0].Id.Should().Be(mailbox.Id);
        all[0].Host.Should().Be("imap.example.com");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsMailbox()
    {
        var mailbox = MakeMailbox();
        await _repository.AddAsync(mailbox, TestContext.Current.CancellationToken);

        var result = await _repository.GetByIdAsync(mailbox.Id, TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.Id.Should().Be(mailbox.Id);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ModifiedMailbox_PersistsChanges()
    {
        var mailbox = MakeMailbox("imap.old.com");
        await _repository.AddAsync(mailbox, TestContext.Current.CancellationToken);

        mailbox.UpdateDetails("imap.new.com", 143, "new@example.com");
        await _repository.UpdateAsync(mailbox, TestContext.Current.CancellationToken);

        var retrieved = await _repository.GetByIdAsync(mailbox.Id, TestContext.Current.CancellationToken);
        retrieved!.Host.Should().Be("imap.new.com");
        retrieved.Port.Should().Be(143);
    }

    [Fact]
    public async Task DeleteAsync_ExistingMailbox_RemovesFromDb()
    {
        var mailbox = MakeMailbox();
        await _repository.AddAsync(mailbox, TestContext.Current.CancellationToken);

        await _repository.DeleteAsync(mailbox.Id, TestContext.Current.CancellationToken);

        var all = await _repository.GetAllAsync(TestContext.Current.CancellationToken);
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_IsNoOp()
    {
        var act = async () => await _repository.DeleteAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }
}
