# Quickstart: Holiday Configuration (Feature 003)

**Branch**: `feature/003-holiday-configuration`  
**Date**: 2026-04-06

---

## Prerequisites

- .NET 8 SDK installed
- `dotnet ef` CLI tool installed:  
  ```bash
  dotnet tool install --global dotnet-ef
  ```
- Repository cloned; on branch `feature/003-holiday-configuration`
- Feature 002 migration (`0002_TaxpayerProfile`) already applied to your local DB

---

## Step 1 — Add AngleSharp to Infrastructure

```bash
cd src/Rentier.Infrastructure
dotnet add package AngleSharp --version "1.*"
```

Verify the entry appears in `Rentier.Infrastructure.csproj`:
```xml
<PackageReference Include="AngleSharp" Version="1.*" />
```

---

## Step 2 — Add Domain Entities

Create the two new domain entities (no test run yet — they have no EF references):

- `src/Rentier.Domain/Entities/PublicHoliday.cs`
- `src/Rentier.Domain/Entities/HolidayYearRange.cs`

See `data-model.md` for complete entity code.

Build the Domain project to verify no compilation errors:
```bash
dotnet build src/Rentier.Domain
```

---

## Step 3 — Add Application Layer

Create all new Application files:

- `src/Rentier.Application/DTOs/HolidayEntryDto.cs`
- `src/Rentier.Application/DTOs/HolidayConfDto.cs`
- `src/Rentier.Application/Interfaces/IHolidayRepository.cs`  ← see `contracts/IHolidayRepository.cs`
- `src/Rentier.Application/Interfaces/IHolidayImporter.cs`    ← see `contracts/IHolidayImporter.cs`
- `src/Rentier.Application/Commands/SaveHolidayConfCommand.cs`
- `src/Rentier.Application/Commands/ImportHolidaysFromWebCommand.cs`
- `src/Rentier.Application/Queries/GetHolidayConfQuery.cs`
- `src/Rentier.Application/Handlers/SaveHolidayConfCommandHandler.cs`
- `src/Rentier.Application/Handlers/GetHolidayConfQueryHandler.cs`
- `src/Rentier.Application/Handlers/ImportHolidaysFromWebCommandHandler.cs`

Build:
```bash
dotnet build src/Rentier.Application
```

---

## Step 4 — Add Infrastructure Layer

Create new Infrastructure files:

- `src/Rentier.Infrastructure/Persistence/Configurations/PublicHolidayConfiguration.cs`
- `src/Rentier.Infrastructure/Persistence/Configurations/HolidayYearRangeConfiguration.cs`
- `src/Rentier.Infrastructure/Repositories/HolidayRepository.cs`
- `src/Rentier.Infrastructure/Scraping/TimeAndDateHolidayScraper.cs`

Modify existing files:

- `src/Rentier.Infrastructure/Persistence/AppDbContext.cs` — add the two `DbSet<>` properties
- `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` — register new services

Build:
```bash
dotnet build src/Rentier.Infrastructure
```

---

## Step 5 — Generate EF Core Migration

```bash
dotnet ef migrations add 0003_HolidayConfiguration \
  --project src/Rentier.Infrastructure \
  --startup-project src/Rentier.Desktop
```

Verify the migration file is created at:
```
src/Rentier.Infrastructure/Persistence/Migrations/0003_HolidayConfiguration.cs
```

Check the generated migration contains `CreateTable("PublicHolidays", …)` and
`CreateTable("HolidayYearRange", …)`.

---

## Step 6 — Add Desktop Layer

Create new Desktop files:

- `src/Rentier.Desktop/ViewModels/HolidayEntryViewModel.cs`
- `src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs`
- `src/Rentier.Desktop/Views/HolidaySettingsView.axaml`
- `src/Rentier.Desktop/Views/HolidaySettingsView.axaml.cs`

Modify existing files:

- `src/Rentier.Desktop/ViewModels/SettingsViewModel.cs` — add `HolidayTab` property
- `src/Rentier.Desktop/Views/SettingsView.axaml` — add Holidays `TabItem`
- `src/Rentier.Desktop/Composition/CompositionRoot.cs` — register new services
- `src/Rentier.Desktop/Resources/Strings.resx` — add 14 new string keys (see plan.md)

Build:
```bash
dotnet build src/Rentier.Desktop
```

---

## Step 7 — Run the Application

```bash
dotnet run --project src/Rentier.Desktop
```

On first launch after the migration:
1. Open **Settings → Holidays**.
2. The DataGrid should auto-populate with 9 Serbian public holidays for the current year
   (seeded on first save, not on first open — click Save to trigger seeding if the grid is
   empty on first open, or verify `HolidayYearRange` row creation).

> **Note**: The seed fires when `SaveHolidayConfCommand` is dispatched AND `HolidayYearRange`
> does not yet exist. If the Holidays tab loads with an empty grid, click **Save** once to
> trigger seeding.

---

## Step 8 — Apply the Migration to an Existing DB

If you have an existing SQLite database from a previous run:

```bash
dotnet ef database update \
  --project src/Rentier.Infrastructure \
  --startup-project src/Rentier.Desktop
```

---

## Step 9 — Run Tests

```bash
# Domain tests (100% rule/state coverage required)
dotnet test tests/Rentier.Domain.Tests

# Application tests (>=90% coverage required)
dotnet test tests/Rentier.Application.Tests

# Infrastructure tests (EF Core InMemory integration)
dotnet test tests/Rentier.Infrastructure.Tests

# Desktop ViewModel tests
dotnet test tests/Rentier.Desktop.Tests

# All tests at once
dotnet test
```

---

## Step 10 — Test Web Import (Manual)

1. Launch the app.
2. Navigate to **Settings → Holidays**.
3. Click **Import from Web**.
4. Enter `2025` as the year.
5. Verify the DataGrid is populated with holidays from `timeanddate.com/holidays/serbia/2025`.
6. Verify that clicking **Save** persists the rows.
7. Restart the app and verify holidays are still present.

> **Network note**: The import requires an active internet connection. If offline, an inline
> error message should appear; the DataGrid contents should remain unchanged.

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| `No such table: PublicHolidays` | Migration not run | Run `dotnet ef database update` |
| Holidays tab not visible | `SettingsView.axaml` not updated | Add Holidays `TabItem` |
| Seeding doesn't fire | `HolidayYearRange` already exists | Check DB for Id=1 row |
| Import returns empty list | DOM structure changed | Update CSS selectors in `TimeAndDateHolidayScraper` |
| `AddHttpClient` not found | Missing `Microsoft.Extensions.Http` | Add NuGet package or verify it is pulled in transitively |
| AngleSharp build error | Version mismatch | Pin to `1.0.7` or latest `1.*` stable |
