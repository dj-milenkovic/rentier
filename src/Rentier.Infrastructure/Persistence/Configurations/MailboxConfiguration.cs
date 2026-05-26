using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rentier.Domain.Entities;
using Rentier.Domain.ValueObjects;

namespace Rentier.Infrastructure.Persistence.Configurations;

public sealed class MailboxConfiguration : IEntityTypeConfiguration<Mailbox>
{
    public void Configure(EntityTypeBuilder<Mailbox> builder)
    {
        builder.ToTable("Mailboxes");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Host).IsRequired().HasMaxLength(253);
        builder.Property(m => m.Port).IsRequired();
        builder.Property(m => m.Username).IsRequired().HasMaxLength(320);

        // MailboxCursor is now an abstract discriminated union (MailboxCursor.NeverSynced |
        // MailboxCursor.SyncedTo). EF Core cannot own abstract types, so the two cursor
        // values are stored as direct columns on Mailboxes using private backing fields.
        // The Cursor property is ignored by EF; it is computed by Mailbox.Cursor from the fields.
        builder.Ignore(m => m.Cursor);

        builder.Property<DateOnly?>("_cursorDate")
            .HasField("_cursorDate")
            .UsePropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field)
            .HasColumnName("Cursor_LastSyncDate")
            .IsRequired(false);

        builder.Property<long?>("_cursorUid")
            .HasField("_cursorUid")
            .UsePropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field)
            .HasColumnName("Cursor_LastUid")
            .IsRequired(false);
    }
}
