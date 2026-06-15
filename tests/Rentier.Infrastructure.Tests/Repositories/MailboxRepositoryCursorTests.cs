using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Domain.Entities;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.Persistence;
using Rentier.Infrastructure.Repositories;

namespace Rentier.Infrastructure.Tests.Repositories;

/// <summary>
/// Covers the <see cref="MailboxRepository.UpdateAsync"/> paths that involve the
/// <see cref="MailboxCursor"/> discriminated union — scenarios not exercised by the
/// root-level <c>MailboxRepositoryTests</c>, which only tests <c>UpdateDetails</c>.
/// </summary>
[Trait("Category", "Integration")]
public class MailboxRepositoryCursorTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private MailboxRepository _repository = null!;

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

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static Mailbox MakeMailbox(string host = "imap.example.com")
        => Mailbox.Create(host, 993, "user@example.com");

    // ── UpdateCursor → UpdateAsync round-trips ─────────────────────────────

    [Fact]
    public async Task UpdateAsync_CursorSyncedToWithUid_PersistsDateAndUid()
    {
        var mailbox = MakeMailbox();
        await _repository.AddAsync(mailbox, TestContext.Current.CancellationToken);

        var syncDate = new DateOnly(2024, 3, 15);
        mailbox.UpdateCursor(new MailboxCursor.SyncedTo(syncDate, 42L));
        await _repository.UpdateAsync(mailbox, TestContext.Current.CancellationToken);

        var retrieved = await _repository.GetByIdAsync(mailbox.Id, TestContext.Current.CancellationToken);

        retrieved.Should().NotBeNull();
        retrieved!.Cursor.Should().BeOfType<MailboxCursor.SyncedTo>();
        var synced = (MailboxCursor.SyncedTo)retrieved.Cursor;
        synced.Date.Should().Be(syncDate);
        synced.Uid.Should().Be(42L);
    }

    [Fact]
    public async Task UpdateAsync_CursorSyncedToWithoutUid_PersistsDateAndNullUid()
    {
        var mailbox = MakeMailbox();
        await _repository.AddAsync(mailbox, TestContext.Current.CancellationToken);

        var syncDate = new DateOnly(2024, 6, 1);
        mailbox.UpdateCursor(new MailboxCursor.SyncedTo(syncDate, Uid: null));
        await _repository.UpdateAsync(mailbox, TestContext.Current.CancellationToken);

        var retrieved = await _repository.GetByIdAsync(mailbox.Id, TestContext.Current.CancellationToken);

        retrieved!.Cursor.Should().BeOfType<MailboxCursor.SyncedTo>();
        var synced = (MailboxCursor.SyncedTo)retrieved.Cursor;
        synced.Date.Should().Be(syncDate);
        synced.Uid.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_CursorSyncedToDateOnly_PreservesExactDateOnly()
    {
        var mailbox = MakeMailbox();
        await _repository.AddAsync(mailbox, TestContext.Current.CancellationToken);

        // Verify value converter round-trip: DateOnly(2024, 12, 31) must survive TEXT storage
        var lastDayOfYear = new DateOnly(2024, 12, 31);
        mailbox.UpdateCursor(new MailboxCursor.SyncedTo(lastDayOfYear, 999L));
        await _repository.UpdateAsync(mailbox, TestContext.Current.CancellationToken);

        var retrieved = await _repository.GetByIdAsync(mailbox.Id, TestContext.Current.CancellationToken);

        var synced = (MailboxCursor.SyncedTo)retrieved!.Cursor;
        synced.Date.Should().Be(lastDayOfYear);
    }

    [Fact]
    public async Task UpdateAsync_CursorFromSyncedToNeverSynced_ClearsPersistedDate()
    {
        // Start with a SyncedTo cursor (set in the constructor)
        var mailbox = new Mailbox(
            Guid.NewGuid(),
            "imap.test.com", 993, "user@test.com",
            new MailboxCursor.SyncedTo(new DateOnly(2024, 1, 1), null));
        await _repository.AddAsync(mailbox, TestContext.Current.CancellationToken);

        // Revert to NeverSynced — cursor columns must become NULL
        mailbox.UpdateCursor(MailboxCursor.NeverSynced.Instance);
        await _repository.UpdateAsync(mailbox, TestContext.Current.CancellationToken);

        var retrieved = await _repository.GetByIdAsync(mailbox.Id, TestContext.Current.CancellationToken);

        retrieved!.Cursor.Should().BeOfType<MailboxCursor.NeverSynced>();
    }

    // ── UpdateAsync isolation ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_CursorUpdate_DoesNotAffectOtherMailboxes()
    {
        var unchanged = MakeMailbox("imap.unchanged.com");
        var toChange = MakeMailbox("imap.change.com");
        await _repository.AddAsync(unchanged, TestContext.Current.CancellationToken);
        await _repository.AddAsync(toChange, TestContext.Current.CancellationToken);

        toChange.UpdateCursor(new MailboxCursor.SyncedTo(new DateOnly(2024, 6, 15), 100L));
        await _repository.UpdateAsync(toChange, TestContext.Current.CancellationToken);

        var unchangedAfter = await _repository.GetByIdAsync(unchanged.Id, TestContext.Current.CancellationToken);
        // The unchanged mailbox was created via Mailbox.Create which uses a default SyncedTo cursor,
        // but the date and UID should not have been modified by the other save.
        unchangedAfter!.Cursor.Should().BeOfType<MailboxCursor.SyncedTo>();
        ((MailboxCursor.SyncedTo)unchangedAfter.Cursor).Uid.Should().BeNull(); // default
    }
}
