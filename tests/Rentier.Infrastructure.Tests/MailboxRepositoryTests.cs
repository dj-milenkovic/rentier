using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Domain.Entities;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;
using Xunit;

namespace Rentier.Infrastructure.Tests;

[Trait("Category", "Integration")]
public class MailboxRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private MailboxRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new MailboxRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static Mailbox MakeMailbox(string host = "imap.example.com")
        => Mailbox.Create(host, 993, "user@example.com");

    [Fact]
    public async Task GetAllAsync_EmptyDb_ReturnsEmptyList()
    {
        var result = await _repository.GetAllAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_NewMailbox_PersistsToDb()
    {
        var mailbox = MakeMailbox();
        await _repository.AddAsync(mailbox);

        var all = await _repository.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Id.Should().Be(mailbox.Id);
        all[0].Host.Should().Be("imap.example.com");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsMailbox()
    {
        var mailbox = MakeMailbox();
        await _repository.AddAsync(mailbox);

        var result = await _repository.GetByIdAsync(mailbox.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(mailbox.Id);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ModifiedMailbox_PersistsChanges()
    {
        var mailbox = MakeMailbox("imap.old.com");
        await _repository.AddAsync(mailbox);

        mailbox.UpdateDetails("imap.new.com", 143, "new@example.com");
        await _repository.UpdateAsync(mailbox);

        var retrieved = await _repository.GetByIdAsync(mailbox.Id);
        retrieved!.Host.Should().Be("imap.new.com");
        retrieved.Port.Should().Be(143);
    }

    [Fact]
    public async Task DeleteAsync_ExistingMailbox_RemovesFromDb()
    {
        var mailbox = MakeMailbox();
        await _repository.AddAsync(mailbox);

        await _repository.DeleteAsync(mailbox.Id);

        var all = await _repository.GetAllAsync();
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_IsNoOp()
    {
        var act = async () => await _repository.DeleteAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }
}
