using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rentier.Domain.Entities;

namespace Rentier.Infrastructure.Persistence.Configurations;

/// <summary>
/// No HasData seeding here — seeding is handled by Application layer (GetHolidayConfQueryHandler) on first run.
/// </summary>
internal sealed class HolidayYearRangeConfiguration : IEntityTypeConfiguration<HolidayYearRange>
{
    public void Configure(EntityTypeBuilder<HolidayYearRange> builder)
    {
        builder.ToTable("HolidayYearRange");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.StartYear).IsRequired();
        builder.Property(r => r.EndYear).IsRequired();
    }
}
