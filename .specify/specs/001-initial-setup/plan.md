# Implementation Plan: Rentier Initial Project Setup

**Branch**: `001-initial-setup` | **Date**: 2026-04-06 | **Spec**: `.specify/specs/001-initial-setup/spec.md`  
**Input**: Feature specification from `.specify/specs/001-initial-setup/spec.md`

---

## Summary

This feature scaffolds the entire Rentier solution from an empty repository. It delivers a
four-project Clean Architecture .NET 8 solution (`Rentier.Domain`, `Rentier.Application`,
`Rentier.Infrastructure`, `Rentier.Desktop`), four test projects (one per layer), the domain
entity and value-object stubs, application CQRS interfaces and repository contracts, an EF Core
8 + SQLite Infrastructure skeleton with an empty initial migration, an Avalonia UI 11 desktop
shell with sidebar navigation (ReactiveUI + CommunityToolkit.Mvvm), a DI composition root,
cross-platform GitHub Actions CI/CD (Windows + macOS, zero-warning gate), `.editorconfig`, and
`.gitignore`. No business logic is implemented; this feature establishes the compilable,
testable foundation every subsequent feature builds upon.

---

## Technical Context

**Language/Version**: C# 12 / .NET 8 (`net8.0` TFM exclusively)  
**Primary Dependencies**:
- Avalonia UI 11.x (FluentTheme, ReactiveUI integration)
- ReactiveUI 20.x + CommunityToolkit.Mvvm 8.x (source generators)
- EF Core 8 + Microsoft.EntityFrameworkCore.Sqlite
- MailKit (Infrastructure — stub only, no active calls)
- Microsoft.Extensions.DependencyInjection 8.x
- xUnit 2.x + FluentAssertions 6.x + NSubstitute 5.x

**Storage**: SQLite (local file, managed by EF Core 8); initial empty migration only  
**Testing**: xUnit + FluentAssertions + NSubstitute; four test projects; smoke tests only at this stage  
**Target Platform**: Windows 10+ (primary), macOS 12+ (secondary); CI matrix covers both; Linux build-only (not CI-tested)  
**Project Type**: Native cross-platform desktop application (Clean Architecture, 4-layer)  
**Performance Goals**: Application window opens within 3 seconds on a standard developer machine; cold build under 5 minutes from clone  
**Constraints**: Zero compiler warnings (`-warnaserror`); no `double`/`float` in monetary fields; no `DateTime` in domain date fields; no credentials in source; no cloud sync or telemetry  
**Scale/Scope**: Single-user desktop application; 4 main projects + 4 test projects; ~50 stub type/interface declarations; 1 CI workflow

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- [x] **Clean Architecture boundary is preserved** (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only). Project references are enforced by `.csproj` declarations and validated at build time. `Desktop` does NOT directly reference `Infrastructure`.
- [x] **All monetary/rate/percentage values are modeled as `decimal`**. `Money.Amount`, `ExchangeRate.RateToRsd` are `decimal`. No `double` or `float` in any domain monetary field. *(CA-002 / FR-005)*
- [x] **All business dates are modeled as `DateOnly`**. `ExchangeRate.Date`, `Report.ImportDate`, `MailboxCursor.LastSyncDate`, `Filing.TaxPeriod`, and `HolidayConf.Holidays` all use `DateOnly`. No `DateTime` in domain types. *(CA-002 / FR-005)*
- [x] **Security/privacy constraints hold**: No IMAP passwords, JMBG values, or sensitive data appear in any source file. `ICredentialStore` in Application is the sole abstraction; Infrastructure stub delegates to OS stores (`PasswordVault` / macOS `Keychain`). No telemetry. *(CA-003 / FR-011 / FR-019)*
- [x] **External network usage is limited to approved endpoints or explicitly justified**: This scaffold performs zero outbound network calls. IMAP and NBS endpoint access is deferred to future features. *(CA-004)*
- [x] **All I/O paths are async; UI avoids blocking calls**: No I/O operations exist in this scaffold. The `ReactiveCommand.CreateFromTask` pattern is documented in `MainWindowViewModel` comments as the mandatory future pattern for UI-initiated async work. *(CA-005)*
- [x] **Tests and coverage impact are defined**: Four test projects, one smoke test per layer (minimum). Domain test covers `Filing` invalid status transition → `DomainException`. Application test covers DI service registration. No coverage gate at this stage (first feature). *(CA-006 / FR-002)*
- [x] **Feature work is mapped to an approved spec task**: Spec task `sdlc-plan` in `.specify/tasks/` governs this feature; constitution and spec are both approved.

*Post-Phase 1 re-check: All checks remain satisfied. The data model, contracts, and architecture design introduced in Phase 1 introduce no new violations.*

---

## Project Structure

### Documentation (this feature)

```text
.specify/specs/001-initial-setup/
├── plan.md              # This file (speckit.plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── repositories.md
│   ├── cqrs.md
│   └── infrastructure.md
└── tasks.md             # Phase 2 output (speckit.tasks — NOT created by speckit.plan)
```

### Source Code (repository root)

```text
Rentier.sln

src/
├── Rentier.Domain/
│   ├── Rentier.Domain.csproj            # net8.0; no project or I/O NuGet refs
│   ├── Entities/
│   │   ├── TaxpayerProfile.cs
│   │   ├── Mailbox.cs
│   │   ├── Importer.cs
│   │   ├── Report.cs
│   │   └── Filing.cs                    # aggregate root; FilingStatus enum; DomainException
│   ├── ValueObjects/
│   │   ├── MailboxCursor.cs             # record; DateOnly LastSyncDate or long LastUid
│   │   ├── Money.cs                     # record; decimal Amount, string Currency
│   │   ├── ExchangeRate.cs              # record; DateOnly Date, string Currency, decimal RateToRsd
│   │   └── HolidayConf.cs              # record; IReadOnlyList<DateOnly> Holidays
│   └── Exceptions/
│       └── DomainException.cs
│
├── Rentier.Application/
│   ├── Rentier.Application.csproj       # net8.0; refs Domain only
│   ├── Interfaces/
│   │   ├── ICommandHandler.cs           # ICommandHandler<TCommand, TResult>
│   │   ├── IQueryHandler.cs             # IQueryHandler<TQuery, TResult>
│   │   └── ICredentialStore.cs          # OS credential store abstraction
│   └── Repositories/
│       ├── IFilingRepository.cs
│       ├── IReportRepository.cs
│       ├── IMailboxRepository.cs
│       ├── IImporterRepository.cs
│       ├── ITaxpayerProfileRepository.cs
│       └── IExchangeRateCacheRepository.cs
│
├── Rentier.Infrastructure/
│   ├── Rentier.Infrastructure.csproj    # net8.0; refs Domain + Application; EF Core + SQLite + MailKit
│   ├── Persistence/
│   │   ├── AppDbContext.cs              # inherits DbContext; no DbSets yet
│   │   └── Migrations/
│   │       └── 0001_InitialCreate/      # empty migration (dotnet ef migrations add)
│   └── Security/
│       └── OsCredentialStore.cs         # ICredentialStore stub; throws NotImplementedException
│
└── Rentier.Desktop/
    ├── Rentier.Desktop.csproj           # net8.0; refs Application + Domain; Avalonia + ReactiveUI + MSDI
    ├── App.axaml                         # Avalonia application; FluentTheme
    ├── App.axaml.cs
    ├── Program.cs                        # Avalonia entry point
    ├── Composition/
    │   └── CompositionRoot.cs           # MSDI IServiceCollection wiring
    ├── Views/
    │   ├── MainWindow.axaml             # sidebar navigation shell
    │   ├── MainWindow.axaml.cs
    │   ├── FilingsView.axaml            # ReactiveUserControl<FilingsViewModel>
    │   ├── FilingsView.axaml.cs
    │   ├── ReportsView.axaml            # ReactiveUserControl<ReportsViewModel>
    │   ├── ReportsView.axaml.cs
    │   ├── SettingsView.axaml           # ReactiveUserControl<SettingsViewModel>
    │   └── SettingsView.axaml.cs
    ├── ViewModels/
    │   ├── MainWindowViewModel.cs       # ReactiveUI; sidebar state; ReactiveCommand.CreateFromTask pattern documented
    │   ├── FilingsViewModel.cs          # placeholder; "Coming soon"
    │   ├── ReportsViewModel.cs          # placeholder; "Coming soon"
    │   └── SettingsViewModel.cs         # placeholder; "Coming soon"
    └── Resources/
        └── Strings.resx                 # navigation labels, window title

tests/
├── Rentier.Domain.Tests/
│   ├── Rentier.Domain.Tests.csproj      # xUnit + FluentAssertions + NSubstitute; refs Domain
│   └── FilingStatusTransitionTests.cs   # smoke: DomainException on invalid transition
│
├── Rentier.Application.Tests/
│   ├── Rentier.Application.Tests.csproj # xUnit + FluentAssertions + NSubstitute; refs Application
│   └── DiRegistrationSmokeTests.cs      # smoke: all Application services register without exception
│
├── Rentier.Infrastructure.Tests/
│   ├── Rentier.Infrastructure.Tests.csproj # refs Infrastructure; EF Core in-memory or SQLite :memory:
│   └── AppDbContextSmokeTests.cs        # smoke: DbContext instantiates without error
│
└── Rentier.Desktop.Tests/
    ├── Rentier.Desktop.Tests.csproj     # refs Desktop + Application
    └── MainWindowViewModelSmokeTests.cs # smoke: ViewModel constructs; navigation state initialises

.github/
└── workflows/
    └── ci.yml                           # matrix: windows-latest + macos-latest; -warnaserror; Coverlet XML

.editorconfig                            # C# 12 rules; Conventional Commits settings
.gitignore                               # .NET artefacts; IDE files; OS metadata
```

**Structure Decision**: 4-layer Clean Architecture (Domain / Application / Infrastructure / Desktop) with a parallel `tests/` tree mirroring each layer. This is the only structure consistent with Constitution Principle I (inward-only dependencies) and is architecture-mandated, not an arbitrary choice.

---

## Complexity Tracking

> No Constitution Check violations exist for this feature. All architectural choices are constitution-mandated.

*(Section intentionally empty — no justifications required.)*
