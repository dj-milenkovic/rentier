# Quickstart: Holidays Settings Repair

**Feature**: 020-holidays-settings-repair
**Branch**: `feature/020-holidays-settings-repair`

---

## Prerequisites

- .NET 8 SDK
- Avalonia UI 11+ (pulled via NuGet restore)
- AngleSharp 1.* (already in Rentier.Infrastructure.csproj)

## Build & Run

```powershell
cd F:\Projects\Rentier\rentier
dotnet restore Rentier.slnx
dotnet build Rentier.slnx --no-restore
dotnet run --project src/Rentier.Desktop
```

## Run Tests

```powershell
# All tests
dotnet test Rentier.slnx --no-build

# Holiday-specific tests only
dotnet test tests/Rentier.Infrastructure.Tests --filter "FullyQualifiedName~Holiday"
dotnet test tests/Rentier.Application.Tests --filter "FullyQualifiedName~Holiday"
dotnet test tests/Rentier.Desktop.Tests --filter "FullyQualifiedName~Holiday"
dotnet test tests/Rentier.Domain.Tests --filter "FullyQualifiedName~Holiday"
```

## Key Files to Modify

### Infrastructure (Parser Fix)
- `src/Rentier.Infrastructure/Scraping/TimeAndDateHolidayScraper.cs` — Fix date extraction (`<th>` not `<td>`), name extraction (anchor text in 2nd `<td>`), national holiday filtering (`showrow` class), error codes

### Desktop (DataGrid Editing + Layout)
- `src/Rentier.Desktop/Converters/DateOnlyToStringConverter.cs` — **NEW**: IValueConverter for DateOnly↔string
- `src/Rentier.Desktop/Views/HolidaySettingsView.axaml` — Replace DataGridTextColumn with DataGridTemplateColumn, fix NumericUpDown widths, add ImportYear input
- `src/Rentier.Desktop/ViewModels/HolidaySettingsViewModel.cs` — Add `HasUnsavedChanges = true` after import, add `IsLoading` gating for commands

### Tests
- `tests/Rentier.Infrastructure.Tests/` — Add scraper parser tests with captured HTML fixture
- `tests/Rentier.Desktop.Tests/HolidaySettingsViewModelTests.cs` — Update for import state changes
- `tests/Rentier.Application.Tests/ImportHolidaysFromWebCommandHandlerTests.cs` — Update error codes

## Manual Verification

1. **Parser**: Run Infrastructure tests with HTML fixture from `holiday-scraped.txt`
2. **DataGrid editing**: Launch app → Settings → Holidays → double-click date cell → type new date → Enter/Escape
3. **Import**: Launch app → Settings → Holidays → enter year → click Import → verify grid populated
4. **Layout**: Resize window to minimum width → verify year fields fully visible
