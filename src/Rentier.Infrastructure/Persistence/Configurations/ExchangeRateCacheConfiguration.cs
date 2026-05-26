using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rentier.Domain.ValueObjects;

namespace Rentier.Infrastructure.Persistence.Configurations;

public sealed class ExchangeRateCacheConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("ExchangeRateCache");
        builder.HasKey(e => new { e.Date, e.Currency });

        builder.Property(e => e.Date)
            .IsRequired();

        builder.Property(e => e.Currency)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.RateToRsd)
            .HasPrecision(18, 6)
            .IsRequired();
    }
}
