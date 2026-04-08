using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rentier.Domain.Entities;

namespace Rentier.Infrastructure.Persistence.Configurations;

public sealed class ImporterConfiguration : IEntityTypeConfiguration<Importer>
{
    public void Configure(EntityTypeBuilder<Importer> builder)
    {
        builder.ToTable("Importers");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(i => i.ReportType).IsRequired().HasConversion<int>();
        builder.Property(i => i.FromFilter).HasMaxLength(500).HasDefaultValue(string.Empty);
        builder.Property(i => i.SubjectFilter).HasMaxLength(500).HasDefaultValue(string.Empty);
        builder.Property(i => i.AttachmentRegex).HasMaxLength(1000).HasDefaultValue(string.Empty);
        builder.Property(i => i.PaymentNotes).HasMaxLength(4000).HasDefaultValue(string.Empty);

        builder.HasOne<TaxpayerProfile>()
            .WithMany()
            .HasForeignKey(i => i.TaxpayerProfileId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Mailbox>()
            .WithMany()
            .HasForeignKey(i => i.MailboxId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
