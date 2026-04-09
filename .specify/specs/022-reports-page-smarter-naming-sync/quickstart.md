# Quickstart: Reports Page – Smarter Naming and Sync Clarification

**Feature**: `003-reports-naming-sync` | **Date**: 2025-07-09

## Prerequisites

- .NET 8 SDK
- Windows or macOS development environment
- Rentier solution builds cleanly (`dotnet build Rentier.slnx`)
- Existing test suite passes (`dotnet test Rentier.slnx`)

## Implementation Order

This feature has a natural bottom-up implementation order following Clean Architecture:

```text
1. Application: IFilingRepository interface  ← new method signature
2. Infrastructure: FilingRepository           ← implement new method
3. Application: ReportRowDto                  ← add DisplayName + EarliestIncomeDate fields
4. Application: GetReportsQueryHandler        ← derive display name using new method
5. Application Tests: GetReportsQueryHandlerTests ← new test cases + update existing
6. Desktop: ReportRowViewModel                ← add DisplayName + OriginalFileName properties
7. Desktop: Strings.resx + Strings.Designer.cs ← new localized strings
8. Desktop: ReportsView.axaml                 ← update column + add tooltip + add subtitle
```

## Key Files to Modify

| Layer | File | Change |
|-------|------|--------|
| Application | `src/Rentier.Application/Repositories/IFilingRepository.cs` | Add `GetEarliestIncomeDateByReportIdAsync` method |
| Application | `src/Rentier.Application/DTOs/ReportRowDto.cs` | Add `DisplayName`, `EarliestIncomeDate` parameters |
| Application | `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs` | Compute display name in `HandleAsync` loop |
| Infrastructure | `src/Rentier.Infrastructure/Repositories/FilingRepository.cs` | Implement `GetEarliestIncomeDateByReportIdAsync` with EF Core |
| Desktop | `src/Rentier.Desktop/ViewModels/ReportRowViewModel.cs` | Add `DisplayName`, `OriginalFileName` properties |
| Desktop | `src/Rentier.Desktop/Views/ReportsView.axaml` | Replace first column with template column + tooltip; add sync subtitle |
| Desktop | `src/Rentier.Desktop/Resources/Strings.resx` | Add `Reports_Sync_Subtitle`, `Reports_Col_DisplayName` |
| Desktop | `src/Rentier.Desktop/Resources/Strings.Designer.cs` | Regenerate |
| Tests | `tests/Rentier.Application.Tests/GetReportsQueryHandlerTests.cs` | Add display name tests, update existing tests for new DTO shape |

## Build & Test Commands

```bash
# Build the full solution
dotnet build Rentier.slnx

# Run all tests
dotnet test Rentier.slnx

# Run only the Application tests (where display name logic lives)
dotnet test tests/Rentier.Application.Tests/

# Run a specific test by name
dotnet test tests/Rentier.Application.Tests/ --filter "DisplayName"
```

## Display Name Derivation Formula

```
importerName = importerNames.GetValueOrDefault(report.ImporterId, "Unknown")
earliestDate = await _filings.GetEarliestIncomeDateByReportIdAsync(report.Id, ct)
effectiveDate = earliestDate ?? report.ImportDate
displayName = $"{importerName} \u2013 {effectiveDate:yyyy-MM-dd}"
```

**Separator**: En dash (–, U+2013), NOT hyphen (-).

## Testing Checklist

- [ ] Report with filings → uses earliest IncomeDate
- [ ] Report without filings → falls back to ImportDate
- [ ] Report with unresolvable importer → uses "Unknown"
- [ ] Multiple reports with varying data → each correctly derived
- [ ] Existing GetReportsQueryHandler tests still pass after DTO change
- [ ] Tooltip shows original file name on hover
- [ ] Sync subtitle text is visible near "Sync Mailboxes" button
- [ ] All new text is from `Strings.resx` (not hardcoded)
- [ ] No compiler warnings
- [ ] CI green on Windows + macOS
