# Quickstart: 018 Sync Replay Controls

**Feature Branch**: `feature/018-sync-replay-controls`

## Prerequisites

- .NET 8 SDK
- Git (feature branch checked out)
- SQLite (managed by EF Core, no external install)

## Setup

```bash
git checkout feature/018-sync-replay-controls
cd F:\Projects\Rentier\rentier
dotnet restore
dotnet build
```

## Run Migrations

```bash
cd src/Rentier.Infrastructure
dotnet ef database update --startup-project ../Rentier.Desktop
```

## Run Tests

```bash
# All tests
dotnet test

# Domain tests only (SyncMode, DuplicateStrategy, SyncParameters validation)
dotnet test --filter "FullyQualifiedName~Rentier.Domain.Tests"

# Application tests only (sync handlers with mode/strategy parameters)
dotnet test --filter "FullyQualifiedName~Rentier.Application.Tests"
```

## Run Application

```bash
cd src/Rentier.Desktop
dotnet run
```

## Key Files to Implement

### Phase 1 — Domain (no dependencies)

| File | What to Create/Modify |
|------|----------------------|
| `src/Rentier.Domain/Enums/SyncMode.cs` | New: SyncMode enum (Incremental, ReplayFromDate, FullReplay) |
| `src/Rentier.Domain/Enums/DuplicateStrategy.cs` | New: DuplicateStrategy enum (SkipExisting, CreateNewRevision, ReprocessInPlace) |
| `src/Rentier.Domain/ValueObjects/SyncParameters.cs` | New: SyncParameters record with GetEffectiveStartDate() |
| `src/Rentier.Domain/Entities/Mailbox.cs` | Modify: Remove InitialSyncDate, update Create() factory |
| `src/Rentier.Domain/Entities/Report.cs` | Modify: Add OriginalReportId, add CreateRevision() factory |

### Phase 2 — Application (depends on Domain)

| File | What to Create/Modify |
|------|----------------------|
| `src/Rentier.Application/Commands/SyncMailboxCommand.cs` | Modify: Add SyncParameters parameter |
| `src/Rentier.Application/Commands/SyncAllCommand.cs` | Modify: Add SyncParameters parameter |
| `src/Rentier.Application/Commands/AddMailboxCommand.cs` | Modify: Remove InitialSyncDate |
| `src/Rentier.Application/Commands/UpdateMailboxCommand.cs` | Modify: Remove InitialSyncDate |
| `src/Rentier.Application/Interfaces/IMailboxSyncService.cs` | Modify: Add SyncParameters to SyncAsync |
| `src/Rentier.Application/Repositories/IReportRepository.cs` | Modify: Add GetByImporterAndMessageIdAsync, GetByImporterAndNameAsync |
| `src/Rentier.Application/Repositories/IFilingRepository.cs` | Modify: Add HasAdvancedFilingsAsync |
| `src/Rentier.Application/DTOs/SyncResult.cs` | Modify: Add skip/revision/reprocess counts |

### Phase 3 — Infrastructure (depends on Application + Domain)

| File | What to Create/Modify |
|------|----------------------|
| `src/Rentier.Infrastructure/Persistence/Migrations/0010_SyncReplayControls.cs` | New: EF migration |
| `src/Rentier.Infrastructure/Persistence/Configurations/MailboxConfiguration.cs` | Modify: Remove InitialSyncDate mapping |
| `src/Rentier.Infrastructure/Persistence/Configurations/ReportConfiguration.cs` | Modify: Add OriginalReportId mapping |
| `src/Rentier.Infrastructure/Sync/ImapMailboxSyncService.cs` | Modify: Mode-based query, strategy-based duplicate handling |
| `src/Rentier.Infrastructure/Repositories/ReportRepository.cs` | Modify: Implement new query methods |
| `src/Rentier.Infrastructure/Repositories/FilingRepository.cs` | Modify: Implement HasAdvancedFilingsAsync |

### Phase 4 — Desktop (depends on Application)

| File | What to Create/Modify |
|------|----------------------|
| `src/Rentier.Desktop/ViewModels/SyncViewModel.cs` | Modify: Add mode/strategy/date properties, impact preview |
| `src/Rentier.Desktop/Views/SyncView.axaml` | Modify: Add mode selector, date picker, strategy selector |
| `src/Rentier.Desktop/Converters/SyncModeDisplayConverter.cs` | New: FuncValueConverter for SyncMode |
| `src/Rentier.Desktop/Converters/DuplicateStrategyDisplayConverter.cs` | New: FuncValueConverter for DuplicateStrategy |
| `src/Rentier.Desktop/ViewModels/MailboxSettingsViewModel.cs` | Modify: Remove InitialSyncDate properties |
| `src/Rentier.Desktop/Views/MailboxSettingsView.axaml` | Modify: Remove DatePicker for InitialSyncDate |
| `src/Rentier.Desktop/Resources/Strings.resx` | Modify: Add localized strings for modes/strategies |

## Testing Focus

| Area | Coverage Target | Key Tests |
|------|----------------|-----------|
| SyncParameters validation | 100% | Mode/date combinations, future date rejection |
| Mailbox.Create without InitialSyncDate | 100% | Default 90-day cursor |
| Report.CreateRevision | 100% | Name uniqueness, OriginalReportId linking |
| Cursor max() logic | 100% | No regression under any scenario |
| Duplicate strategy branching | 90%+ | All 3 strategies × duplicate/no-duplicate |
| ReprocessInPlace safety | 100% | Blocked when filing.Status ≠ Init |
| SyncViewModel reactive chains | 90%+ | Mode selection → visibility toggling |
