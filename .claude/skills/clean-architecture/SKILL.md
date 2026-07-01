---
name: clean-architecture
description: >
  Defines the authoritative clean architecture, CQRS flow, Avalonia UI
  composition, and UX contracts for the Rentier desktop application.
tags:
  - architecture
  - clean-architecture
  - avalonia
  - cqrs
  - desktop
---

# Skill: Clean Architecture + Avalonia UI Patterns for Rentier

## Purpose
Defines how layers are organized, how the UI layer is built with Avalonia, and
how CQRS connects ViewModels to domain logic. This is the single architectural
reference for Rentier — replacing any generic UI skill file.

## Layer Map

```
Rentier.Domain
  └── Entities: Filing, Report, Mailbox, Importer, TaxpayerProfile
  └── Value Objects: Money, ExchangeRate, MailboxCursor, HolidayConf
  └── Interfaces: (none — no persistence interfaces here)
  └── Domain Events: FilingStatusChanged, ReportProcessed
  └── Exceptions: DomainException

Rentier.Application
  └── Commands: SyncMailboxCommand, CreateFilingCommand, UpdateFilingStatusCommand
  └── Queries: GetFilingsQuery, GetReportsQuery, GetMailboxSettingsQuery
  └── Interfaces: IFilingRepository, IReportRepository, IMailboxRepository,
                  IImporterRepository, IExchangeRateCacheRepository,
                  ICredentialStore, INbsRateFetcher
  └── DTOs: FilingDto, ReportDto, SyncResultDto
  └── Handlers: one handler class per command/query

Rentier.Infrastructure
  └── Persistence: AppDbContext (EF Core 8 + SQLite)
                   Repositories implementing IRepository interfaces
                   Migrations/
  └── External: NbsRateFetcher (HttpClient)
                ImapSyncService (MailKit)
                OsCredentialStore (Windows/macOS)
  └── Serialization: PpOpoXmlSerializer (XDocument)
  └── Parsers: IbkrCsvParser (CsvHelper)

Rentier.Desktop
  └── Views/ (Avalonia XAML + ReactiveUserControl<TViewModel>)
  └── ViewModels/ (ReactiveObject, IActivatableViewModel)
  └── Navigation/ (INavigationService, SplitView shell)
  └── DI/ (AppBootstrapper, ServiceCollectionExtensions)
  └── Program.cs
```

## ViewModel Pattern

Every ViewModel follows this structure:
```csharp
public sealed class FilingsViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    [Reactive] public bool IsLoading { get; private set; }
    [Reactive] public string? ErrorMessage { get; private set; }
    [Reactive] public IReadOnlyList<FilingDto> Filings { get; private set; } = [];

    public ReactiveCommand<Unit, Unit> LoadCommand { get; }
    public ReactiveCommand<FilingDto, Unit> ExportXmlCommand { get; }

    public FilingsViewModel(IQueryHandler<GetFilingsQuery, IReadOnlyList<FilingDto>> handler)
    {
        LoadCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            IsLoading = true;
            var result = await handler.HandleAsync(new GetFilingsQuery());
            result.Match(
                filings => Filings = filings,
                error => ErrorMessage = error.Message
            );
            IsLoading = false;
        });
    }
}
```

## Navigation Model
Single-window `SplitView` shell (sidebar always visible):
```
┌──────────────┬──────────────────────────────────┐
│  🏠 Rentier  │                                  │
│  ─────────── │   Content Area                   │
│  ▶ Sync      │   (swapped by NavigationService) │
│    Reports   │                                  │
│    Filings   │                                  │
│  ─────────── │                                  │
│  Settings    │                                  │
│   Profile    │                                  │
│   Mailbox    │                                  │
│   Importers  │                                  │
│   Technical  │                                  │
└──────────────┴──────────────────────────────────┘
```

`INavigationService` abstracts page transitions. ViewModels navigate by requesting
a page type, not by knowing about Views.

## Loading / Error State Contract
Every View that loads data must bind to:
```xml
<ProgressBar IsVisible="{Binding IsLoading}" IsIndeterminate="True" />
<TextBlock IsVisible="{Binding ErrorMessage, Converter={x:Static ...IsNotNull}}"
           Text="{Binding ErrorMessage}" Foreground="Red" />
<Panel IsVisible="{Binding HasData}">
  <!-- content -->
</Panel>
```

## Filings Page UX Enhancement
Grouped by status. Three expander sections:
1. **⚠ Overdue / Due Soon** — deadline within 14 days (red/orange highlight)
2. **📋 Pending** — init status
3. **✅ Filed / Paid** — collapsed by default

Summary bar at top: `Total Payable: 45,230 RSD | Filed: 12,000 RSD | Paid: 33,230 RSD`

## Sync Page UX Enhancement
Structured log (not a raw text dump):
```
[▶ Start Sync]  [⬛ Cancel]
──────────────────────────────────────
✓  stmt_20240315.csv    → 3 filings created
✓  stmt_20240316.csv    → 1 filing created
⚠  stmt_20240317.csv    → parse error: missing header
──────────────────────────────────────
Sync complete: 4 filings created, 1 error.
```
Each row: icon (`✓`/`⚠`/`⏳`) + file name + outcome. Scrollable.

## SQLite Storage
- Single file: `%APPDATA%\Rentier\rentier.db` (Windows) / `~/Library/Application Support/Rentier/rentier.db` (macOS).
- Path resolved via `IFileSystem` abstraction (testable).
- EF Core migrations are forward-only. Never destructive.
- No server, no network, no sync. All data is local.