# Quickstart: Default Sort & Column Sort for Filings and Reports

**Feature**: 027-default-sort-column-sort  
**Branch**: `feat/027-031-ux-improvements`

## What This Feature Does

Adds default descending sort order to both the Filings and Reports tables so users see the most relevant items first. Additionally enables interactive column-header sorting on the Filings DataGrid.

## Key Files to Touch

### Application Layer (new + modified)

| File | Action | Purpose |
|------|--------|---------|
| `src/Rentier.Application/Enums/FilingSortColumn.cs` | **CREATE** | New enum: `FilingDeadline`, `Status`, `IncomeType`, `PayingEntity`, `TaxPayable`, `PaymentReference` |
| `src/Rentier.Application/Queries/GetFilingsQuery.cs` | **MODIFY** | Add `FilingSortColumn SortColumn` and `bool SortDescending` parameters with defaults |
| `src/Rentier.Application/Queries/GetReportsQuery.cs` | **MODIFY** | Add `bool SortDescending = true` parameter |
| `src/Rentier.Application/Repositories/IFilingRepository.cs` | **MODIFY** | Add sort params to `GetPagedAsync` signature |
| `src/Rentier.Application/Repositories/IReportRepository.cs` | **MODIFY** | Add `bool sortDescending` to `GetAllAsync` signature |
| `src/Rentier.Application/Handlers/GetFilingsQueryHandler.cs` | **MODIFY** | Pass sort params through to repository |
| `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs` | **MODIFY** | Pass `SortDescending` to repository |

### Infrastructure Layer (modified)

| File | Action | Purpose |
|------|--------|---------|
| `src/Rentier.Infrastructure/Repositories/FilingRepository.cs` | **MODIFY** | Dynamic `OrderBy`/`OrderByDescending` based on `FilingSortColumn` + secondary `ThenBy(Id)` |
| `src/Rentier.Infrastructure/Repositories/ReportRepository.cs` | **MODIFY** | Conditional `OrderBy`/`OrderByDescending` on `ImportDate` + secondary `ThenBy(Id)` |

### Desktop Layer (modified)

| File | Action | Purpose |
|------|--------|---------|
| `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs` | **MODIFY** | Add `SortColumn`, `SortDescending` properties, `ApplySortCommand`, page-reset logic |
| `src/Rentier.Desktop/Views/FilingsView.axaml` | **MODIFY** | Set `CanUserSortColumns="True"`, add `Tag` to sortable columns, wire `Sorting` event |
| `src/Rentier.Desktop/Views/FilingsView.axaml.cs` | **MODIFY** | Add `DataGrid_Sorting` event handler |
| `src/Rentier.Desktop/Views/ReportsView.axaml` | **MODIFY** | Explicitly set `CanUserSortColumns="False"` |

### Tests (new + modified)

| File | Action | Purpose |
|------|--------|---------|
| `tests/Rentier.UnitTests/Application/GetFilingsQueryHandlerTests.cs` | **MODIFY** | Add tests for sort parameter pass-through |
| `tests/Rentier.UnitTests/Application/GetReportsQueryHandlerTests.cs` | **MODIFY** | Add tests for sort parameter pass-through |
| `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs` | **MODIFY** | Add tests for sort state, page-reset, command behavior |
| `tests/Rentier.Infrastructure.Tests/FilingRepositoryTests.cs` | **MODIFY** | Add integration tests for dynamic ORDER BY |

## Build & Test

```powershell
# From repo root
dotnet build Rentier.slnx
dotnet test Rentier.slnx --no-build
```

## Implementation Order

1. `FilingSortColumn` enum (no dependencies)
2. Query + interface changes (Application layer)
3. Handler changes (Application layer)
4. Repository implementations (Infrastructure layer)
5. ViewModel sort state (Desktop layer)
6. View AXAML + code-behind (Desktop layer)
7. Tests at each layer
