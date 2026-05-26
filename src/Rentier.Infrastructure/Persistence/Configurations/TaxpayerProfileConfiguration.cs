using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rentier.Domain.Entities;

namespace Rentier.Infrastructure.Persistence.Configurations;

public sealed class TaxpayerProfileConfiguration : IEntityTypeConfiguration<TaxpayerProfile>
{
    public void Configure(EntityTypeBuilder<TaxpayerProfile> builder)
    {
        builder.ToTable("TaxpayerProfiles");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Jmbg).IsRequired().HasMaxLength(13);
        builder.Property(p => p.FullName).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Address).IsRequired().HasMaxLength(500);
        builder.Property(p => p.OpstinaCode).IsRequired().HasMaxLength(10);
        builder.Property(p => p.PhoneNumber).HasMaxLength(50);
        builder.Property(p => p.Email).HasMaxLength(200);
        builder.HasIndex(p => p.Jmbg).IsUnique();
    }
}
