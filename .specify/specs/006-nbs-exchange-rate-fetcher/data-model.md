# Data Model: NBS Exchange Rate Cache (Feature 006)

**Date**: 2026-04-07

---

## Overview

Feature 006 promotes the existing `ExchangeRate` Domain value object to a persisted EF entity by adding `DbSet<ExchangeRate>` to `AppDbContext` and a new `ExchangeRateCacheConfiguration` EF fluent configuration. No changes to `ExchangeRate.cs` are required.

---

## ExchangeRate — Domain Value Object (existing, unchanged)

**File**: `src/Rentier.Domain/ValueObjects/ExchangeRate.cs`

```csharp
public record ExchangeRate
{
    public DateOnly Date     { get; init; }
    public string Currency   { get; init; }
    public decimal RateToRsd { get; init; }

    public ExchangeRate(DateOnly date, string currency, decimal rateToRsd)
    {
        if (rateToRsd <= 0)
            throw new DomainException($"RateToRsd must be positive, got {rateToRsd}");
        Date      = date;
        Currency  = currency;
        RateToRsd = rateToRsd;
    }
}
```

### Properties

| Property | CLR Type | Constraints | Purpose |
|----------|----------|-------------|---------|
| `Date` | `DateOnly` | Non-nullable, PK component | NBS official rate date |
| `Currency` | `string` | Non-nullable, max 10, PK component | ISO 4217 code (e.g., `EUR`) |
| `RateToRsd` | `decimal` | > 0 | Middle_Rate ÷ Unit |

### Composite Primary Key

`(Date, Currency)` — one rate per currency per calendar day.

---

## EF Configuration

**New file**: `src/Rentier.Infrastructure/Persistence/Configurations/ExchangeRateCacheConfiguration.cs`

```csharp
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
```

### SQLite Table Schema

```sql
CREATE TABLE ExchangeRateCache (
    Date       TEXT    NOT NULL,   -- EF maps DateOnly → ISO 8601 TEXT in SQLite
    Currency   TEXT    NOT NULL,   -- VARCHAR(10) — ISO 4217 code
    RateToRsd  REAL    NOT NULL,   -- decimal with precision 18,6 (stored as TEXT by EF SQLite provider)
    CONSTRAINT PK_ExchangeRateCache PRIMARY KEY (Date, Currency)
);
```

> **Note on decimal storage**: EF Core's SQLite provider stores `decimal` columns as TEXT (ISO formatted) when `HasPrecision` is specified, preserving full decimal precision without floating-point rounding. This is the correct and expected behaviour for financial values.

---

## AppDbContext — Before / After

### Before (existing)

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaxpayerProfile> TaxpayerProfiles => Set<TaxpayerProfile>();
    public DbSet<PublicHoliday>   PublicHolidays   => Set<PublicHoliday>();
    public DbSet<HolidayYearRange> HolidayYearRange => Set<HolidayYearRange>();
    public DbSet<Mailbox>         Mailboxes         => Set<Mailbox>();
    public DbSet<Importer>        Importers         => Set<Importer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

### After (one line added)

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaxpayerProfile>  TaxpayerProfiles => Set<TaxpayerProfile>();
    public DbSet<PublicHoliday>    PublicHolidays    => Set<PublicHoliday>();
    public DbSet<HolidayYearRange> HolidayYearRange  => Set<HolidayYearRange>();
    public DbSet<Mailbox>          Mailboxes          => Set<Mailbox>();
    public DbSet<Importer>         Importers          => Set<Importer>();
    public DbSet<ExchangeRate>     ExchangeRateCache  => Set<ExchangeRate>(); // ← NEW

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

The `ApplyConfigurationsFromAssembly` call automatically picks up `ExchangeRateCacheConfiguration` — no explicit call needed.

---

## InfrastructureServiceExtensions — Before / After

### Added registrations (at end of `AddInfrastructureServices`)

```csharp
// Before (existing tail):
services.AddTransient<IImporterRepository, ImporterRepository>();
#pragma warning disable CA1416
services.AddTransient<ICredentialStore, OsCredentialStore>();
#pragma warning restore CA1416
return services;

// After (two new lines before return):
services.AddTransient<IImporterRepository, ImporterRepository>();
#pragma warning disable CA1416
services.AddTransient<ICredentialStore, OsCredentialStore>();
#pragma warning restore CA1416
services.AddTransient<IExchangeRateCacheRepository, ExchangeRateCacheRepository>(); // ← NEW
services.AddHttpClient<IExchangeRateFetcher, NbsExchangeRateFetcher>();              // ← NEW
return services;
```

---

## EF Migration: `0006_ExchangeRateCache`

**Command to generate** (run from repo root):
```shell
dotnet ef migrations add 0006_ExchangeRateCache \
    --project src/Rentier.Infrastructure \
    --startup-project src/Rentier.Desktop
```

**Expected `Up` body**:
```csharp
migrationBuilder.CreateTable(
    name: "ExchangeRateCache",
    columns: table => new
    {
        Date      = table.Column<DateOnly>(type: "TEXT", nullable: false),
        Currency  = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
        RateToRsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false)
    },
    constraints: table =>
    {
        table.PrimaryKey("PK_ExchangeRateCache", x => new { x.Date, x.Currency });
    });
```

---

## IExchangeRateCacheRepository (existing, unchanged)

**File**: `src/Rentier.Application/Repositories/IExchangeRateCacheRepository.cs`

```csharp
public interface IExchangeRateCacheRepository
{
    Task<ExchangeRate?> GetAsync(DateOnly date, string currency, CancellationToken ct = default);
    Task<IReadOnlyList<ExchangeRate>> GetByDateRangeAsync(DateOnly from, DateOnly to, string currency, CancellationToken ct = default);
    Task SaveAsync(ExchangeRate rate, CancellationToken ct = default);
    Task SaveBatchAsync(IReadOnlyList<ExchangeRate> rates, CancellationToken ct = default);
}
```

---

## Data Flow Summary

```
FetchRateAsync(date, currency)
        │
        ▼
GetAsync(date, currency)  ──── hit ──▶  return Result.Success(cached)
        │
       miss
        │
        ▼
  HTTP GET NBS XML
        │
        ▼
  Parse XDocument
  (15 ExchangeRateXml entries)
        │
        ├──▶ SaveBatchAsync(allRates)   [upsert all 15 into ExchangeRateCache]
        │
        └──▶ find requested currency in batch
                  │
              found ──▶ Result.Success(rate)
                  │
            not found ──▶ Result.Failure(RATE_NOT_FOUND)
```
