using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;

namespace Rentier.Infrastructure.Persistence.Configurations;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    private static readonly ValueComparer<byte[]?> ATTACHMENT_REFERENCE_COMPARER = new(
        (left, right) => ReferenceEquals(left, right),
        content => content == null ? 0 : RuntimeHelpers.GetHashCode(content),
        content => content);

    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.ReportName).IsRequired().HasMaxLength(500);
        // Reference comparer: the domain only ever replaces the attachment array, never
        // mutates it in place, so EF Core need not deep-copy the (multi-MB) BLOB into its
        // original-value snapshot each time a report is attached or loaded for tracking.
        builder.Property(r => r.AttachmentContent)
            .IsRequired(false)
            .Metadata.SetValueComparer(ATTACHMENT_REFERENCE_COMPARER);
        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ReportStatus.Init);
        builder.Property(r => r.MailboxMessageId).IsRequired(false);
        builder.Property(r => r.EmailDate).IsRequired(false);
        builder.Property(r => r.OriginalReportId).IsRequired(false);
        builder.HasIndex(r => r.ImporterId);
        builder.HasIndex("ImporterId", "ReportName").IsUnique();
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
