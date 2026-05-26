using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;

namespace Rentier.Infrastructure.Persistence.Configurations;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.ReportName).IsRequired().HasMaxLength(500);
        builder.Property(r => r.AttachmentContent).IsRequired(false);
        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ReportStatus.Init);
        builder.Property(r => r.MailboxMessageId).IsRequired(false);
        builder.Property(r => r.EmailDate).IsRequired(false);
        builder.Property(r => r.OriginalReportId).IsRequired(false);
        builder.HasIndex(r => r.ImporterId);
        builder.HasIndex(new[] { "ImporterId", "ReportName" }).IsUnique();
        builder.HasIndex(r => r.OriginalReportId);
        builder.HasOne<Importer>()
            .WithMany()
            .HasForeignKey(r => r.ImporterId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Report>()
            .WithMany()
            .HasForeignKey(r => r.OriginalReportId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
