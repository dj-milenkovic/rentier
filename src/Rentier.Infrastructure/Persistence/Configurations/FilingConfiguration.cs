using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rentier.Domain.Entities;

namespace Rentier.Infrastructure.Persistence.Configurations;

public sealed class FilingConfiguration : IEntityTypeConfiguration<Filing>
{
    public void Configure(EntityTypeBuilder<Filing> builder)
    {
        builder.ToTable("Filings");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.PayingEntity).IsRequired().HasMaxLength(500);
        builder.Property(f => f.GrossIncomeRsd).HasPrecision(18, 2);
        builder.Property(f => f.WhtPaidRsd).HasPrecision(18, 2);
        builder.Property(f => f.GrossTaxPayableRsd).HasPrecision(18, 2);
        builder.Property(f => f.TaxPayableRsd).HasPrecision(18, 2);
        builder.HasOne<TaxpayerProfile>()
            .WithMany()
            .HasForeignKey(f => f.TaxpayerProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Report>()
            .WithMany()
            .HasForeignKey(f => f.ReportId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(f => f.TaxpayerProfileId);
        builder.HasIndex(f => f.ReportId);
        builder.Property(f => f.PaymentReference)
            .IsRequired(false)
            .HasMaxLength(200);
    }
}
