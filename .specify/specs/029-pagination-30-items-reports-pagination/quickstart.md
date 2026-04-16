# Quickstart: Pagination — 30 Items per Page & Reports Pagination

**Feature**: 029-pagination-30-items-reports-pagination  
**Branch**: `feat/027-031-ux-improvements`

## Prerequisites

- .NET 8 SDK
- Repository cloned and on branch `feat/027-031-ux-improvements`
- `dotnet restore` completed

## Changed Files (by layer)

### Application Layer (`src/Rentier.Application/`)

| File | Action | Description |
|------|--------|-------------|
| `DTOs/ReportsPageResult.cs` | **CREATE** | New paginated result record mirroring `FilingsPageResult` |
| `Queries/GetReportsQuery.cs` | **MODIFY** | Add `Page` and `PageSize` parameters with defaults |
| `Queries/GetFilingsQuery.cs` | **MODIFY** | Change `PageSize` default from `20` to `30` |
| `Handlers/GetReportsQueryHandler.cs` | **MODIFY** | Add validation, in-memory slicing, return `ReportsPageResult` |

### Desktop Layer (`src/Rentier.Desktop/`)

| File | Action | Description |
|------|--------|-------------|
| `ViewModels/ReportsViewModel.cs` | **MODIFY** | Add pagination state, commands, page navigation logic |
| `Views/ReportsView.axaml` | **MODIFY** | Add pagination bar (Previous / indicator / Next) |
| `Resources/Strings.resx` | **MODIFY** | Add `Reports_Page_Previous`, `Reports_Page_Next`, `Reports_Page_Indicator` |
| `Resources/Strings.Designer.cs` | **AUTO** | Regenerated from `Strings.resx` |
| `ViewModels/FilingsViewModel.cs` | **MODIFY** | Change page size from `20` to `30` in `LoadPageAsync` |

### Test Layer (`tests/Rentier.UnitTests/`)

| File | Action | Description |
|------|--------|-------------|
| `Application/GetReportsQueryHandlerTests.cs` | **MODIFY** | Update for new return type, add pagination tests |
| `Application/GetFilingsQueryHandlerTests.cs` | **MODIFY** | Update page size expectations from `20` to `30` |
| `Desktop/ReportsViewModelTests.cs` | **MODIFY** | Add pagination state/command tests |
| `Desktop/FilingsViewModelTests.cs` | **MODIFY** | Update page size from `20` to `30` |

## Build & Test

```shell
# Build
dotnet build Rentier.slnx

# Run all tests
dotnet test Rentier.slnx

# Run only affected test files
dotnet test tests/Rentier.UnitTests --filter "FullyQualifiedName~GetReportsQueryHandler|FullyQualifiedName~GetFilingsQueryHandler|FullyQualifiedName~ReportsViewModel|FullyQualifiedName~FilingsViewModel"
```

## Key Patterns to Follow

1. **Query record with defaults**: See `GetFilingsQuery` for the pattern — `public sealed record GetReportsQuery(int Page = 1, int PageSize = 30);`
2. **Handler validation**: See `GetFilingsQueryHandler` lines 34–40 for page/pageSize validation guards
3. **ViewModel pagination**: See `FilingsViewModel` for the complete reactive pagination pattern (properties, commands, page clamping)
4. **View pagination bar**: See `FilingsView.axaml` lines 62–70 for the exact AXAML structure to replicate
5. **Resource strings**: Follow `Reports_*` naming convention in `Strings.resx`
