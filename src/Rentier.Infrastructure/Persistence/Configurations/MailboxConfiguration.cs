using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rentier.Domain.Entities;

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
        builder.Property(m => m.InitialSyncDate).IsRequired();
        builder.OwnsOne(m => m.Cursor, cursor =>
        {
            cursor.Property(c => c.LastSyncDate)
                .HasColumnName("Cursor_LastSyncDate")
                .IsRequired(false);
            cursor.Property(c => c.LastUid)
                .HasColumnName("Cursor_LastUid")
                .IsRequired(false);
        });
    }
}
