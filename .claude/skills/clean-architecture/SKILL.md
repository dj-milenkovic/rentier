---
name: clean-architecture
description: >
  The authoritative layer map, CQRS contracts, DI composition, navigation model, and
  MVVM patterns for the Rentier desktop application. Make sure to use this skill when
  adding any feature, command/query/handler, DTO, repository interface, view, or
  ViewModel; when registering services; when deciding which layer or folder new code
  belongs in; or when reviewing code for layering violations — even if the user only
  says "add X to the app" without mentioning architecture.
tags:
  - architecture
  - clean-architecture
  - avalonia
  - cqrs
  - desktop
---

# Clean Architecture + Avalonia UI Patterns for Rentier

Defines how layers are organized, how the UI layer is built with Avalonia, and how
CQRS connects ViewModels to domain logic. Dependencies point inward only:
Desktop → Application → Domain, with Infrastructure implementing Application
interfaces. For step-by-step feature scaffolding with code templates, use
`.claude/skills/rentier-new-feature`.

## Layer Map

```
Rentier.Domain            (no external dependencies)
  └── Entities/       Filing, Report, Mailbox, Importer, TaxpayerProfile,
                      UserPreference, PublicHoliday, HolidayYearRange
  └── ValueObjects/   Money, ExchangeRate, FilingInfo, MailboxCursor,
                      HolidayConf, SyncParameters
  └── Services/       TaxCalculationService, FilingDeadlineCalculator (pure logic)
  └── Enums/          FilingStatus, IncomeType, ReportStatus, ReportType,
                      SyncMode, DuplicateStrategy, ExchangeRateSourceType
  └── Exceptions/     DomainException (business-rule violations)

Rentier.Application       (references Domain only)
  └── Commands/ + Queries/   one sealed record per use case
  └── Handlers/              one handler class per command/query
  └── Interfaces/            ICommandHandler<TCommand,TResult>, IQueryHandler<TQuery,TResult>
  └── Repositories/          IFilingRepository, IReportRepository, … (ports; Infrastructure implements)
  └── Common/                Result<TValue,TError>, Error (Code+Message with factories), VoidResult
  └── DTOs/                  FilingDto, ReportDto, … (Desktop binds to these, never to entities)

Rentier.Infrastructure    (implements Application interfaces; EF Core 10 + SQLite)
  └── Persistence/       AppDbContext, repositories, Migrations/ (forward-only, never edited)
  └── ExchangeRates/     NbsExchangeRateFetcher, NbsWebScraper
  └── Parsing/           IbkrCsvParser        └── Serialization/  PpOpoXmlSerializer
  └── mail sync (MailKit), OS credential store

Rentier.Desktop           (Avalonia + ReactiveUI; calls Application only)
  └── Views/             AXAML + ReactiveUserControl<TViewModel> code-behind
  └── ViewModels/        ReactiveObject (+ IActivatableViewModel where lifecycle matters)
  └── Composition/       CompositionRoot.AddDesktopServices (DI wiring)
  └── Services/          IThemeService, ILocalizationService (UI-only concerns)
  └── Converters/, Dialogs/ (ConfirmDialogHelper, ImportDialogHelper), Models/, Resources/, Assets/
```

## CQRS contract

Handlers implement `ICommandHandler`/`IQueryHandler` and return `Result<T, Error>`.
Business rules live in Domain and throw `DomainException`; handlers translate that to
`Error.Domain(...)` — expected failures are values, not exceptions:

```csharp
public sealed record UpdateFilingStatusCommand(Guid FilingId, FilingStatus NewStatus);

public sealed class UpdateFilingStatusCommandHandler
    : ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>>
{
    private readonly IFilingRepository _filings;
    public UpdateFilingStatusCommandHandler(IFilingRepository filings) => _filings = filings;

    public async Task<Result<VoidResult, Error>> HandleAsync(
        UpdateFilingStatusCommand command, CancellationToken ct = default)
    {
        var filing = await _filings.GetByIdAsync(command.FilingId, ct);
        if (filing is null)
            return Result<VoidResult, Error>.Failure(Error.NotFound($"Filing {command.FilingId} not found."));
        try { filing.AdvanceStatus(command.NewStatus); }
        catch (DomainException ex) { return Result<VoidResult, Error>.Failure(Error.Domain(ex.Message)); }
        await _filings.UpdateAsync(filing, ct);
        return Result<VoidResult, Error>.Success(VoidResult.Value);
    }
}
```

## Consuming results in ViewModels

`Result<T, Error>` exposes `IsSuccess`, `Value`, `Error` — **there is no `Match`
method**. Commands are `ReactiveCommand.CreateFromTask` (never block the UI thread):

```csharp
LoadCommand = ReactiveCommand.CreateFromTask(async () =>
{
    IsLoading = true;
    var result = await _getFilingsHandler.HandleAsync(new GetFilingsQuery());
    if (result.IsSuccess)
        Filings = result.Value;
    else
        ErrorMessage = result.Error.Message;
    IsLoading = false;
});
```

Standard state properties for data-loading ViewModels: `IsLoading`, `ErrorMessage`,
and a has-content flag (`HasData`/`HasItems`). Every data-loading View binds all three.

## DI composition

`CompositionRoot.AddDesktopServices` (`src/Rentier.Desktop/Composition/`) registers
Application handlers and ViewModels; Infrastructure registers itself via
`AddInfrastructureServices` in `App.axaml.cs`:

- Handlers → `AddTransient<ICommandHandler<TCmd, Result<T, Error>>, THandler>()`
- ViewModels with in-session state (settings pages, MainWindow) → `AddSingleton`
- Every new handler must be registered here —
  `tests/Rentier.UnitTests/Application/DiRegistrationSmokeTests.cs` guards this.

## Navigation model

Single-window sidebar shell, owned by `MainWindowViewModel` — there is **no
`INavigationService`**:

- `MainWindowViewModel.NavigationEntries` is the sidebar: `NavigationEntry` items
  carrying a label, icon, and content `ViewModel`.
- Selecting an entry sets `CurrentViewModel`; the shell's `ContentControl` +
  `ViewLocator` render the matching View.
- `NavigationEntry` supports collapsible groups (`IsGroup`/`Children` — the Settings
  section) and hidden entries (`IsVisible: false`) for sub-pages like Manual Filing.
- Pages: **Dashboard, Sync, Reports, Filings**, Manual Filing (hidden sub-page), and
  the **Settings** group → Profile, Holidays, Mailboxes, Importers, Appearance.
- Content ViewModels never reference Views or other pages; cross-page jumps are
  raised through delegates/observables that `MainWindowViewModel` subscribes to.

## UI conventions

- Sidebar labels and all user-facing strings come from `ILocalizationService`
  resource keys — never hardcode display text.
- Lists use `DataGrid`; the Filings and Reports grids use per-row ViewModels
  (`FilingRowViewModel`, `ReportRowViewModel`) with column filter flyouts
  (`TextFilterFlyoutViewModel`, `EnumFilterFlyoutViewModel`).
- The Sync page renders a structured progress log (`SyncProgressEntryViewModel`
  rows: status icon + file + outcome), not a raw text dump.
- Dialogs go through `Dialogs/` helpers (`ConfirmDialogHelper`, `ImportDialogHelper`),
  always awaited.
- Visual design (tokens, theming, icons) is governed by
  `.claude/skills/rentier-ui-design` — read it before touching AXAML.

## SQLite storage

- Single local file: `%APPDATA%\Rentier\rentier.db` (Windows),
  `~/Library/Application Support/Rentier/rentier.db` (macOS).
- EF Core 10 migrations are forward-only and never destructive — add a new migration,
  never edit a shipped one (`.claude/settings.json` denies edits there for a reason).
- No server, no network sync — all data local; credentials only in the OS store.
