---
description: "Task list for Rentier Initial Project Setup (001-initial-setup)"
---

<!--
  Constitution Compliance Checklist (T003):
  ✅ `decimal` for Money.Amount and ExchangeRate.RateToRsd — no double/float
  ✅ `DateOnly` for all domain date fields (ExchangeRate.Date, Filing.TaxPeriod, Report.ImportDate, MailboxCursor.LastSyncDate, HolidayConf.Holidays)
  ✅ `async Task` for all I/O methods — no .Result or .Wait()
  ✅ Desktop → Application → Domain dependency direction (inward-only)
  ✅ ICredentialStore as sole secret abstraction
  ✅ No credentials in source files
-->

# Tasks: Rentier Initial Project Setup

**Feature**: `001-initial-setup`
**Branch**: `001-initial-setup`
**Input**: `.specify/specs/001-initial-setup/` (spec.md, plan.md, data-model.md, contracts/)
**Prerequisites**: plan.md ✅ · spec.md ✅ · data-model.md ✅ · contracts/repositories.md ✅ · contracts/cqrs.md ✅ · contracts/infrastructure.md ✅

**Tests**: Included — Domain requires 100% state-machine coverage (constitution Principle V); Application and Infrastructure smoke tests; Desktop ViewModel smoke tests.

**Organisation**: Tasks are grouped by user story phase to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no blocking dependency on incomplete tasks)
- **[Story]**: User story this task belongs to ([US1]–[US5])
- Setup / Foundational / Polish phases carry no `[Story]` label
- Every description includes the exact file path

---

## Phase 1: Setup

**Purpose**: Repository-level tooling and boilerplate files. No source code. These tasks do not block each other.

- [x] T001 Create .gitignore at repository root covering .NET build artefacts (`bin/`, `obj/`, `*.user`, `*.suo`), IDE files (`.vs/`, `.idea/`, `.vscode/`, `*.DotSettings.user`), and OS metadata (`.DS_Store`, `Thumbs.db`, `desktop.ini`)
- [x] T002 [P] Create .editorconfig at repository root: `indent_style = space`, `indent_size = 4`, `charset = utf-8-bom`, C# 12 `sealed` class preference, `record` value-object conventions, `async` method suffix rule, Conventional Commits commit-message format hints
- [x] T003 Add constitution compliance checklist as a comment block at the top of this tasks.md confirming: `decimal` for `Money.Amount`/`ExchangeRate.RateToRsd`, `DateOnly` for all domain date fields, `async Task` for all I/O methods, `Desktop → Application → Domain` dependency direction, `ICredentialStore` as sole secret abstraction, no credentials in source files

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Solution file and all nine `.csproj` project files. Every user story phase depends on this phase being complete.

**⚠️ CRITICAL**: No user story work can begin until all tasks in this phase are done. `dotnet restore Rentier.sln` must succeed as the checkpoint.

- [x] T004 Create Rentier.sln at repository root using `dotnet new sln -n Rentier`
- [x] T005 Create src/Rentier.Domain/Rentier.Domain.csproj: `net8.0`; `<Nullable>enable</Nullable>`; `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`; `<LangVersion>12</LangVersion>`; `<ImplicitUsings>enable</ImplicitUsings>`; NO `<ProjectReference>` entries; NO I/O NuGet packages (no EF Core, no MailKit, no HttpClient wrappers); note: these properties will be centralised in Directory.Build.props in T067 (Phase 7) and removed from individual .csproj files in T069 — do not remove them manually before then
- [x] T006 [P] Create src/Rentier.Application/Rentier.Application.csproj: `net8.0`; same quality properties as T005; single `<ProjectReference>` to `../Rentier.Domain/Rentier.Domain.csproj`; no EF Core or MailKit package references
- [x] T007 [P] Create src/Rentier.Infrastructure/Rentier.Infrastructure.csproj: `net8.0`; same quality properties; `<ProjectReference>` to Rentier.Domain and Rentier.Application; `<PackageReference>` for `Microsoft.EntityFrameworkCore.Sqlite` 8.x, `Microsoft.EntityFrameworkCore.Tools` 8.x, `MailKit` (latest stable)
- [x] T008 [P] Create src/Rentier.Desktop/Rentier.Desktop.csproj: `net8.0`; same quality properties; `<ProjectReference>` to Rentier.Application and Rentier.Domain; `<PackageReference>` for `Avalonia` 11.x, `Avalonia.Themes.Fluent` 11.x, `Avalonia.ReactiveUI` 11.x, `ReactiveUI` 20.x, `CommunityToolkit.Mvvm` 8.x, `Microsoft.Extensions.DependencyInjection` 8.x; add `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`
- [x] T009 Create tests/Rentier.Domain.Tests/Rentier.Domain.Tests.csproj: `net8.0`; same quality properties; `<ProjectReference>` to Rentier.Domain; `<PackageReference>` for `xunit` 2.x, `xunit.runner.visualstudio` 2.x, `Microsoft.NET.Test.Sdk`, `FluentAssertions` 6.x, `NSubstitute` 5.x, `coverlet.collector`; `<IsPackable>false</IsPackable>`
- [x] T010 [P] Create tests/Rentier.Application.Tests/Rentier.Application.Tests.csproj: same test packages as T009; `<ProjectReference>` to Rentier.Application; `<IsPackable>false</IsPackable>`
- [x] T011 [P] Create tests/Rentier.Infrastructure.Tests/Rentier.Infrastructure.Tests.csproj: same test packages as T009; `<ProjectReference>` to Rentier.Infrastructure; add `<PackageReference>` for `Microsoft.EntityFrameworkCore.InMemory` 8.x (kept for lightweight tests); prefer `Microsoft.EntityFrameworkCore.Sqlite` for migration-fidelity tests — see T052; `<IsPackable>false</IsPackable>`
- [x] T012 [P] Create tests/Rentier.Desktop.Tests/Rentier.Desktop.Tests.csproj: same test packages as T009; `<ProjectReference>` to Rentier.Desktop and Rentier.Application; `<IsPackable>false</IsPackable>`
- [x] T013 Add all 8 projects to Rentier.sln using `dotnet sln Rentier.sln add` for each: `src/Rentier.Domain/`, `src/Rentier.Application/`, `src/Rentier.Infrastructure/`, `src/Rentier.Desktop/`, `tests/Rentier.Domain.Tests/`, `tests/Rentier.Application.Tests/`, `tests/Rentier.Infrastructure.Tests/`, `tests/Rentier.Desktop.Tests/`

**Checkpoint**: `dotnet restore Rentier.sln` completes without error. All 8 project files listed in solution explorer.

---

## Phase 3: User Story 1 — Solution Scaffolding (Priority: P1) 🎯 MVP

**Goal**: `dotnet build Rentier.sln -warnaserror` exits with code 0, zero diagnostic messages, on both Windows and macOS. All 8 projects produce output artefacts. Project reference graph enforces Clean Architecture inward-only dependency rule. Each test project contains at least one passing test so `dotnet test` never reports "no tests found."

**Independent Test**: Clone the repository; run `dotnet build Rentier.sln -warnaserror` on Windows; repeat on macOS; verify exit code 0 and no warning annotations in either run.

### Tests for User Story 1

> **NOTE: Write these stubs FIRST so each test project compiles. Real test logic is added in Phases 4–6.**

- [x] T014 [US1] Create tests/Rentier.Domain.Tests/FilingStatusTransitionTests.cs with a single placeholder fact: `[Fact] public void Placeholder_DomainProjectCompiles() => Assert.True(true);` — this file will be replaced with real Filing state-machine tests in Phase 4 (T026)
- [x] T015 [P] [US1] Create tests/Rentier.Application.Tests/DiRegistrationSmokeTests.cs with a single placeholder fact: `[Fact] public void Placeholder_ApplicationProjectCompiles() => Assert.True(true);` — will be replaced with real DI smoke tests in Phase 5 (T038)
- [x] T016 [P] [US1] Create tests/Rentier.Infrastructure.Tests/AppDbContextSmokeTests.cs with a single placeholder fact: `[Fact] public void Placeholder_InfrastructureProjectCompiles() => Assert.True(true);` — will be replaced with real DbContext smoke tests in Phase 6 (T051)
- [x] T017 [P] [US1] Create tests/Rentier.Desktop.Tests/MainWindowViewModelSmokeTests.cs with a single placeholder fact: `[Fact] public void Placeholder_DesktopProjectCompiles() => Assert.True(true);` — will be replaced with real ViewModel smoke tests in Phase 6 (T050)

### Implementation for User Story 1

- [x] T018 [P] [US1] Create src/Rentier.Domain/Exceptions/DomainException.cs: `public sealed class DomainException : Exception` with constructors `(string message)` and `(string message, Exception inner)` — minimum stub required for domain types to reference in Phase 4
- [x] T019 [P] [US1] Create src/Rentier.Infrastructure/Persistence/AppDbContext.cs: `public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options) { }` — C# 12 primary constructor; no `DbSet<>` properties; no entity configurations; `using Microsoft.EntityFrameworkCore;`
- [x] T019A [US1] Generate initial EF Core migration: run `dotnet ef migrations add 0001_InitialCreate --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop` from repository root; commit the generated migration files in `src/Rentier.Infrastructure/Persistence/Migrations/`; prerequisite: `dotnet-ef` global tool must be installed (`dotnet tool install --global dotnet-ef`)
- [x] T020 [US1] Create src/Rentier.Infrastructure/Security/OsCredentialStore.cs:`public sealed class OsCredentialStore` with three stub methods — `SaveCredentialAsync`, `GetCredentialAsync`, `DeleteCredentialAsync` — all throwing `new NotImplementedException("ICredentialStore implementation deferred to IMAP mailbox feature")` and returning `Task`/`Task<string?>`; add `// TODO: implement ICredentialStore once Application/Interfaces/ICredentialStore.cs is created in Phase 5`
- [x] T021 [US1] Create src/Rentier.Desktop/Program.cs: Avalonia entry point — `AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().UseReactiveUI().StartWithClassicDesktopLifetime(args)` inside `[STAThread] static int Main(string[] args)`; `using Avalonia;`
- [x] T022 [US1] Create src/Rentier.Desktop/App.axaml: `<Application>` root with `<Application.Styles><FluentTheme /></Application.Styles>` and `x:Class="Rentier.Desktop.App"` using `xmlns:avaloniaXaml` and standard Avalonia XML namespaces
- [x] T023 [US1] Create src/Rentier.Desktop/App.axaml.cs: `public partial class App : Application` with `override void OnFrameworkInitializationCompleted()` — instantiate `MainWindow` and assign to `(ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow`; `base.OnFrameworkInitializationCompleted()` called last; DI wiring added in Phase 6 (T063)
- [x] T024 [US1] Create src/Rentier.Desktop/Views/MainWindow.axaml: minimal `<Window>` stub with `Title="{x:Static res:Strings.AppTitle}"` and a single empty `<Grid/>`; add `xmlns:res` pointing to `Rentier.Desktop.Resources`; full sidebar layout added in Phase 6 (T057)
- [x] T025 [US1] Create src/Rentier.Desktop/Views/MainWindow.axaml.cs: `public partial class MainWindow : Window` with default constructor calling `InitializeComponent()`; `using Avalonia.Controls;`
- [x] T026 [US1] Create src/Rentier.Desktop/Resources/Strings.resx: resource file with key `AppTitle` = `"Rentier"`; additional navigation label keys added in Phase 6 (T052); right-click → Add → New Item → Resources File in the IDE, or create XML manually with `<data name="AppTitle"><value>Rentier</value></data>`
- [x] T027 [US1] Verify `dotnet build Rentier.sln -warnaserror` exits code 0; confirm all 8 project output assemblies are present under `bin/`; run `dotnet test Rentier.sln --no-build` and verify ≥4 passing tests (one placeholder per layer) and 0 failing tests

**Checkpoint**: `dotnet build Rentier.sln -warnaserror` passes. `dotnet test Rentier.sln` reports ≥4 tests, 0 failures. All artefacts present.

---

## Phase 4: User Story 2 — Domain Foundations (Priority: P2)

**Goal**: `Rentier.Domain` contains all 9 domain type declarations (5 entities + 4 value objects) with correct C# types (`decimal` for monetary fields, `DateOnly` for date fields, `record` for value objects). The `Filing` aggregate root enforces its `Init → Filed → Paid` state machine — invalid transitions throw `DomainException`. Zero I/O NuGet packages in `Rentier.Domain.csproj`.

**Independent Test**: Construct instances of all 9 types in `Rentier.Domain.Tests`; assert `Filing.AdvanceStatus(FilingStatus.Init)` throws `DomainException` from `Paid`; run `grep -rn "double\|float\|DateTime" src/Rentier.Domain/` and confirm zero matches.

### Tests for User Story 2

> **NOTE: Replace the placeholder in FilingStatusTransitionTests.cs. Tests MUST FAIL before Filing.cs is implemented.**

- [x] T028 [US2] Replace the placeholder fact in tests/Rentier.Domain.Tests/FilingStatusTransitionTests.cs with real test methods following `MethodName_StateUnderTest_ExpectedBehavior` naming: (1) `AdvanceStatus_FromInitToFiled_StatusBecomesFiled` — valid forward transition; (2) `AdvanceStatus_FromFiledToPaid_StatusBecomesPaid` — valid forward transition; (3) `AdvanceStatus_FromPaidToInit_ThrowsDomainException` — invalid backward; (4) `AdvanceStatus_FromInitToPaid_ThrowsDomainException` — invalid skip; (5) `AdvanceStatus_FromFiledToInit_ThrowsDomainException` — invalid backward; use FluentAssertions for assertions (`act.Should().Throw<DomainException>()`)

### Implementation for User Story 2

- [x] T029 [P] [US2] Create src/Rentier.Domain/ValueObjects/Money.cs: `public record Money(decimal Amount, string Currency)` — `decimal` is MANDATORY per constitution Principle III; no validation in stub (deferred to future feature); `using` only `System` namespace
- [x] T030 [P] [US2] Create src/Rentier.Domain/ValueObjects/MailboxCursor.cs: `public record MailboxCursor(DateOnly? LastSyncDate, long? LastUid)` — both nullable (null = no sync yet); `DateOnly` MANDATORY per constitution Principle III
- [x] T031 [P] [US2] Create src/Rentier.Domain/ValueObjects/ExchangeRate.cs: `public record ExchangeRate(DateOnly Date, string Currency, decimal RateToRsd)` — `DateOnly` and `decimal` MANDATORY; constructor body validates `RateToRsd > 0` else throw `new DomainException($"RateToRsd must be positive, got {RateToRsd}")`
- [x] T032 [P] [US2] Create src/Rentier.Domain/ValueObjects/HolidayConf.cs: `public record HolidayConf(IReadOnlyList<DateOnly> Holidays)` — constructor validates `Holidays is not null` else throw `new DomainException("Holidays list must not be null")`; empty list is valid
- [x] T033 [P] [US2] Create src/Rentier.Domain/Entities/TaxpayerProfile.cs: `public sealed class TaxpayerProfile` with primary constructor `(Guid Id, string Jmbg, string FullName, string Address, string OpstinaCode)`; body validates Jmbg is exactly 13 digit characters (`jmbg.Length == 13 && jmbg.All(char.IsDigit)`), FullName/Address/OpstinaCode not null/whitespace — all throw `DomainException` with descriptive messages on violation
- [x] T034 [P] [US2] Create src/Rentier.Domain/Entities/Mailbox.cs: `public sealed class Mailbox` with `(Guid Id, string Host, int Port, string Username, MailboxCursor Cursor)`; validates Host/Username not null/whitespace; Port in range 1–65535 — all throw `DomainException` on violation
- [x] T035 [P] [US2] Create src/Rentier.Domain/Entities/Importer.cs: `public sealed class Importer` with `(Guid Id, string DisplayName, string FilterExpression = "")`; validates DisplayName not null/whitespace — throw `DomainException` on violation
- [x] T036 [P] [US2] Create src/Rentier.Domain/Entities/Report.cs: `public sealed class Report` with `(Guid Id, DateOnly ImportDate, Guid ImporterId)`; validates `ImportDate <= DateOnly.FromDateTime(DateTime.UtcNow.Date)` — throw `DomainException("ImportDate must not be in the future")` on violation; `DateOnly` MANDATORY per constitution Principle III
- [x] T037 [US2] Create src/Rentier.Domain/Entities/Filing.cs: `public sealed class Filing` with `(Guid Id, Guid TaxpayerProfileId, DateOnly TaxPeriod, FilingStatus Status = FilingStatus.Init)`; method `public void AdvanceStatus(FilingStatus newStatus)` — permits only `Init→Filed` and `Filed→Paid`; any other transition throws `new DomainException($"Invalid Filing status transition: {Status} → {newStatus}")` — this is the aggregate root; also create nested or sibling `public enum FilingStatus { Init = 0, Filed = 1, Paid = 2 }` in same file or `src/Rentier.Domain/Entities/FilingStatus.cs`
- [x] T038 [US2] Run `dotnet test tests/Rentier.Domain.Tests/ -warnaserror` and verify all 5 FilingStatusTransitionTests pass; run `grep -rn "double\|float\|DateTime" src/Rentier.Domain/` and confirm zero matches confirming constitution compliance

**Checkpoint**: Domain tests pass (≥5 facts in FilingStatusTransitionTests). All 9 domain types compile. `decimal` and `DateOnly` types confirmed by grep scan.

---

## Phase 5: User Story 3 — Application Contracts (Priority: P3)

**Goal**: `Rentier.Application` contains all 6 repository interface stubs, `ICommandHandler<TCommand, TResult>`, `IQueryHandler<TQuery, TResult>`, and `ICredentialStore`. All are empty contracts — zero EF Core or MailKit references in `Rentier.Application.csproj`. `OsCredentialStore` updated to explicitly implement `ICredentialStore`.

**Independent Test**: Reference `Rentier.Application` in a test; create NSubstitute mocks for each of the 9 interfaces; verify the mocks resolve from an `IServiceCollection` without exception.

### Tests for User Story 3

> **NOTE: Replace the placeholder in DiRegistrationSmokeTests.cs. The test MUST FAIL until all interfaces exist.**

- [x] T039 [US3] Replace the placeholder fact in tests/Rentier.Application.Tests/DiRegistrationSmokeTests.cs with real test method `ServiceCollection_WithSubstituteStubs_ResolvesAllApplicationInterfaces` that: (1) creates `new ServiceCollection()`, (2) registers NSubstitute stubs for `IFilingRepository`, `IReportRepository`, `IMailboxRepository`, `IImporterRepository`, `ITaxpayerProfileRepository`, `IExchangeRateCacheRepository`, `ICredentialStore`, (3) calls `provider.GetRequiredService<T>()` for each, (4) asserts each result `.Should().NotBeNull()` using FluentAssertions; use `using NSubstitute;`

### Implementation for User Story 3

- [x] T040 [P] [US3] Create src/Rentier.Application/Interfaces/ICommandHandler.cs: `public interface ICommandHandler<TCommand, TResult> { Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default); }` — add XML doc comment: "Execute a command that mutates domain state. Never use .Result or .Wait() on the returned Task."
- [x] T041 [P] [US3] Create src/Rentier.Application/Interfaces/IQueryHandler.cs: `public interface IQueryHandler<TQuery, TResult> { Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default); }` — add XML doc comment: "Execute a read-only query. Returns structured result; never throws for empty-result cases."
- [x] T042 [P] [US3] Create src/Rentier.Application/Interfaces/ICredentialStore.cs: interface with 3 async methods per contracts/infrastructure.md: `Task SaveCredentialAsync(string key, string secret, CancellationToken ct = default)`, `Task<string?> GetCredentialAsync(string key, CancellationToken ct = default)`, `Task DeleteCredentialAsync(string key, CancellationToken ct = default)`; add XML doc comment with key format `Rentier/<entity-type>/<entity-id>/<field>` and security constraint "IMAP passwords MUST be stored exclusively via this interface"
- [x] T043 [P] [US3] Create src/Rentier.Application/Repositories/IFilingRepository.cs: interface with 6 methods per contracts/repositories.md: `GetByIdAsync(Guid, CancellationToken)`, `GetAllAsync(CancellationToken)`, `GetByTaxPeriodAsync(Guid, DateOnly, CancellationToken)`, `AddAsync(Filing, CancellationToken)`, `UpdateAsync(Filing, CancellationToken)`, `DeleteAsync(Guid, CancellationToken)` — all `Task`/`Task<T>` with `CancellationToken ct = default`; `using Rentier.Domain.Entities;`
- [x] T044 [P] [US3] Create src/Rentier.Application/Repositories/IReportRepository.cs: interface with 5 methods per contracts/repositories.md: `GetByIdAsync`, `GetAllAsync`, `GetByImporterAsync(Guid, CancellationToken)`, `AddAsync`, `DeleteAsync` — all async; `using Rentier.Domain.Entities;`
- [x] T045 [P] [US3] Create src/Rentier.Application/Repositories/IMailboxRepository.cs: interface with 5 methods per contracts/repositories.md: `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync` — all async; `using Rentier.Domain.Entities;`
- [x] T046 [P] [US3] Create src/Rentier.Application/Repositories/IImporterRepository.cs: interface with 5 methods per contracts/repositories.md: `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync` — all async; `using Rentier.Domain.Entities;`
- [x] T047 [P] [US3] Create src/Rentier.Application/Repositories/ITaxpayerProfileRepository.cs: interface with 3 methods per contracts/repositories.md: `GetAsync(CancellationToken)`, `SaveAsync(TaxpayerProfile, CancellationToken)`, `DeleteAsync(CancellationToken)` — all async; `using Rentier.Domain.Entities;`
- [x] T048 [P] [US3] Create src/Rentier.Application/Repositories/IExchangeRateCacheRepository.cs: interface with 4 methods per contracts/repositories.md: `GetAsync(DateOnly, string, CancellationToken)`, `GetByDateRangeAsync(DateOnly, DateOnly, string, CancellationToken)`, `SaveAsync(ExchangeRate, CancellationToken)`, `SaveBatchAsync(IReadOnlyList<ExchangeRate>, CancellationToken)` — all async; `using Rentier.Domain.ValueObjects;`
- [x] T049 [US3] Update src/Rentier.Infrastructure/Security/OsCredentialStore.cs: add `using Rentier.Application.Interfaces;` and change class declaration to `public sealed class OsCredentialStore : ICredentialStore` — all three method bodies still throw `new NotImplementedException("Full OS credential store implementation deferred to IMAP mailbox feature")`; remove the TODO comment added in T020
- [x] T050 [US3] Run `dotnet test tests/Rentier.Application.Tests/ -warnaserror` and verify DiRegistrationSmokeTests pass; run `dotnet build src/Rentier.Application/ -warnaserror` and grep output for "EntityFramework" or "MailKit" — confirm zero matches confirming no infrastructure references in Application

**Checkpoint**: Application contracts compile. DiRegistrationSmokeTests reports ≥1 passing test. `Rentier.Application.csproj` has no EF Core or MailKit references.

---

## Phase 6: User Story 4 — Desktop Shell (Priority: P4)

**Goal**: The Avalonia desktop application launches; a sidebar shows Filings, Reports, and Settings destinations; clicking each destination renders a non-empty placeholder `ReactiveUserControl<TViewModel>` pane; DI composition root registers all services and resolves them without exception at startup; all user-visible strings live in `Resources/Strings.resx`.

**Independent Test**: Run `dotnet run --project src/Rentier.Desktop`; confirm window opens within 3 seconds; click all three sidebar entries; verify non-empty placeholder pane renders for each; grep source for hard-coded navigation strings in XAML — expect zero matches.

### Tests for User Story 4

> **NOTE: Write real tests FIRST — they MUST FAIL before ViewModels are implemented.**

- [x] T051 [P] [US4] Replace the placeholder in tests/Rentier.Desktop.Tests/MainWindowViewModelSmokeTests.cs with two real test methods: (1) `MainWindowViewModel_Constructed_NavigationEntriesHasThreeItems` — construct `MainWindowViewModel` with stub dependencies; assert `NavigationEntries.Count.Should().Be(3)`; (2) `MainWindowViewModel_Constructed_InitialViewModelIsFilingsViewModel` — assert `CurrentViewModel.Should().BeOfType<FilingsViewModel>()`; use `NSubstitute` for any injected dependencies
- [x] T052 [P] [US4] Replace the placeholder in tests/Rentier.Infrastructure.Tests/AppDbContextSmokeTests.cs with real test method `AppDbContext_CreatedWithSqliteMemory_DoesNotThrow` that constructs `AppDbContext` using `new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options`, calls `context.Database.EnsureCreated()`, and asserts no exception is thrown using FluentAssertions `.Should().NotThrow()`

> **MVVM Strategy**: ViewModels use `ReactiveObject` (ReactiveUI) exclusively with `RaiseAndSetIfChanged` for property change notification. `CommunityToolkit.Mvvm` `[ObservableProperty]` is NOT used on ViewModels because its source generators require `ObservableObject` as base class, which is incompatible with `ReactiveObject`. CommunityToolkit.Mvvm remains a package reference for future use of `[RelayCommand]` on non-ViewModel types if needed.

### Implementation for User Story 4

- [x] T053 [P] [US4]Update src/Rentier.Desktop/Resources/Strings.resx: add keys `Nav_Filings` = `"Filings"`, `Nav_Reports` = `"Reports"`, `Nav_Settings` = `"Settings"`, `ComingSoon` = `"Coming soon"` (AppTitle already added in T026); all navigation labels must be accessed via `Strings.*` in code and `{x:Static}` in XAML — no hard-coded inline strings
- [x] T054 [P] [US4] Create src/Rentier.Desktop/ViewModels/FilingsViewModel.cs: `public sealed class FilingsViewModel : ReactiveObject`; property `private string _placeholder = Strings.ComingSoon; public string Placeholder { get => _placeholder; set => this.RaiseAndSetIfChanged(ref _placeholder, value); }`; `using ReactiveUI;`; no data loading logic
- [x] T055 [P] [US4] Create src/Rentier.Desktop/ViewModels/ReportsViewModel.cs: same pattern as T054 — `public sealed class ReportsViewModel : ReactiveObject` with `private string _placeholder = Strings.ComingSoon; public string Placeholder { get => _placeholder; set => this.RaiseAndSetIfChanged(ref _placeholder, value); }`
- [x] T056 [P] [US4] Create src/Rentier.Desktop/ViewModels/SettingsViewModel.cs: same pattern as T054 — `public sealed class SettingsViewModel : ReactiveObject` with `private string _placeholder = Strings.ComingSoon; public string Placeholder { get => _placeholder; set => this.RaiseAndSetIfChanged(ref _placeholder, value); }`
- [x] T057 [US4] Create src/Rentier.Desktop/ViewModels/MainWindowViewModel.cs: `public sealed class MainWindowViewModel : ReactiveObject`; constructor injects `FilingsViewModel`, `ReportsViewModel`, `SettingsViewModel`; property `IReadOnlyList<NavigationEntry> NavigationEntries` initialised to three `NavigationEntry` records `{ Label = Strings.Nav_Filings, ViewModel = filingsVm }` etc.; `private ReactiveObject _currentViewModel; public ReactiveObject CurrentViewModel { get => _currentViewModel; set => this.RaiseAndSetIfChanged(ref _currentViewModel, value); }` initialised to `filingsVm`; `private NavigationEntry _selectedEntry; public NavigationEntry SelectedEntry { get => _selectedEntry; set => this.RaiseAndSetIfChanged(ref _selectedEntry, value); }`; `using ReactiveUI;`; add `ReactiveCommand.CreateFromTask` pattern XML doc comment per CA-005: "// For UI-initiated async work, use: SomeCommand = ReactiveCommand.CreateFromTask(async ct => await _handler.HandleAsync(cmd, ct)); Updates scheduled via RxApp.MainThreadScheduler."
- [x] T058 [P] [US4] Create src/Rentier.Desktop/ViewModels/NavigationEntry.cs: `public record NavigationEntry(string Label, ReactiveObject ViewModel)` — used by `MainWindowViewModel.NavigationEntries` to bind sidebar items and content simultaneously; `using ReactiveUI;`
- [x] T058A [P] [US4] Create src/Rentier.Desktop/ViewLocator.cs: `public sealed class ViewLocator : IDataTemplate` — implement `Build(object? param)` to resolve View type from ViewModel type by replacing "ViewModel" suffix with "View" in the fully qualified class name (e.g., `FilingsViewModel` → `FilingsView`); implement `Match(object? data)` to return `data is ReactiveObject`; `using Avalonia.Controls;`, `using Avalonia.Controls.Templates;`, `using ReactiveUI;`
- [x] T058B [P] [US4] Update src/Rentier.Desktop/App.axaml: add `<Application.DataTemplates><local:ViewLocator/></Application.DataTemplates>` inside the `<Application>` element after the `<Application.Styles>` block; add `xmlns:local="using:Rentier.Desktop"` to the root element's namespace declarations — this enables Avalonia's `ContentControl` to automatically resolve the correct View for each bound ViewModel
- [x] T059 [US4] Update src/Rentier.Desktop/Views/MainWindow.axaml:replace the minimal stub `<Grid/>` with a `<DockPanel>`: left-docked `<ListBox DockPanel.Dock="Left" Items="{Binding NavigationEntries}" SelectedItem="{Binding SelectedEntry}">` with `<ListBox.ItemTemplate>` showing `{Binding Label}`; right `<ContentControl Content="{Binding CurrentViewModel}">` for the page area; bind `MainWindowViewModel.SelectedEntry` to navigate: add a `WhenAnyValue` binding in code-behind to update `CurrentViewModel`; Title from `{x:Static res:Strings.AppTitle}`
- [x] T060 [US4] Update src/Rentier.Desktop/Views/MainWindow.axaml.cs: change to `public partial class MainWindow : ReactiveWindow<MainWindowViewModel>`; constructor `(MainWindowViewModel vm)` — assigns `DataContext = vm`; add `this.WhenActivated` subscription that syncs `SelectedEntry` → `CurrentViewModel` using `this.WhenAnyValue(x => x.ViewModel!.SelectedEntry).Subscribe(e => ViewModel!.CurrentViewModel = e.ViewModel)` pattern; `using ReactiveUI;`
- [x] T061 [P] [US4] Create src/Rentier.Desktop/Views/FilingsView.axaml + src/Rentier.Desktop/Views/FilingsView.axaml.cs: XAML is `<reactiveUi:ReactiveUserControl x:TypeArguments="vm:FilingsViewModel">`; displays `<TextBlock Text="{Binding Placeholder}"/>`; code-behind is `public partial class FilingsView : ReactiveUserControl<FilingsViewModel>` calling `InitializeComponent()`
- [x] T062 [P] [US4] Create src/Rentier.Desktop/Views/ReportsView.axaml + src/Rentier.Desktop/Views/ReportsView.axaml.cs: same pattern as T061 for `ReportsViewModel` — `ReactiveUserControl<ReportsViewModel>` with placeholder text binding
- [x] T063 [P] [US4] Create src/Rentier.Desktop/Views/SettingsView.axaml + src/Rentier.Desktop/Views/SettingsView.axaml.cs: same pattern as T061 for `SettingsViewModel` — `ReactiveUserControl<SettingsViewModel>` with placeholder text binding
- [x] T064 [US4] Create src/Rentier.Desktop/Composition/CompositionRoot.cs: `public static class CompositionRoot` with extension method `public static IServiceCollection AddDesktopServices(this IServiceCollection services)` that registers: `services.AddTransient<FilingsViewModel>()`, `services.AddTransient<ReportsViewModel>()`, `services.AddTransient<SettingsViewModel>()`, `services.AddSingleton<MainWindowViewModel>()` — note: `ICredentialStore → OsCredentialStore` registration added when Desktop gets a reference to Infrastructure (deferred to IMAP mailbox feature per architecture constraints); add `// Architecture note: Desktop references Application + Domain only per plan.md FR-003. Infrastructure services (ICredentialStore) registered when the IMAP feature introduces the Desktop → Infrastructure DI composition extension.`
- [x] T065 [US4] Update src/Rentier.Desktop/App.axaml.cs: in `OnFrameworkInitializationCompleted` build `var services = new ServiceCollection(); services.AddDesktopServices(); var provider = services.BuildServiceProvider();`; resolve `var mainVm = provider.GetRequiredService<MainWindowViewModel>();`; assign `new MainWindow(mainVm)` to the app lifetime's `MainWindow`; add XML doc comment: "DI composition root. Infrastructure services (ICredentialStore) will be registered here when the IMAP mailbox feature is implemented."; `using Microsoft.Extensions.DependencyInjection;`
- [x] T066 [US4] Run `dotnet test tests/Rentier.Desktop.Tests/ tests/Rentier.Infrastructure.Tests/ -warnaserror` and verify `MainWindowViewModelSmokeTests` and `AppDbContextSmokeTests` both pass; run `dotnet run --project src/Rentier.Desktop` and confirm window opens with sidebar; run full `dotnet test Rentier.sln` and confirm ≥8 passing tests across all 4 projects, 0 failures

**Checkpoint**: Desktop shell launches. Sidebar shows 3 destinations. Navigation works. DI composition root builds without exceptions. All 8 smoke tests pass.

---

## Phase 7: User Story 5 — CI Green (Priority: P5)

**Goal**: GitHub Actions workflow triggers on `push` to `develop`/`main` and `pull_request` targeting `develop`; builds with `-warnaserror` on `windows-latest` AND `macos-latest`; all tests pass; Coverlet XML coverage report produced and posted to job step summary.

**Independent Test**: Push a commit to `develop`; observe both matrix jobs reach green status in GitHub Actions; confirm zero warning annotations; confirm ≥8 passing tests in step summary; confirm coverage XML artifact is uploaded.

### Tests for User Story 5

> *No additional unit tests — the GitHub Actions run itself is the acceptance gate.*

### Implementation for User Story 5

- [x] T067 [US5] Create Directory.Build.props at repository root: shared MSBuild properties applied to all projects to avoid per-.csproj duplication: `<TargetFramework>net8.0</TargetFramework>`, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<LangVersion>12</LangVersion>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<Deterministic>true</Deterministic>`, `<EmbedUntrackedSources>true</EmbedUntrackedSources>`; update individual `.csproj` files created in Phase 2 (T005–T012) to REMOVE properties now centralised in Directory.Build.props to avoid duplication
- [x] T068 [US5] Create .github/workflows/ci.yml: `on: push: branches: [develop, main]` and `on: pull_request: branches: [develop]`; `jobs: build: strategy: matrix: os: [windows-latest, macos-latest]`; `runs-on: ${{ matrix.os }}`; steps: `actions/checkout@v4`, `actions/setup-dotnet@v4` with `dotnet-version: '8.x'`, `dotnet restore Rentier.sln`, `dotnet build Rentier.sln --no-restore -warnaserror -c Release`, `dotnet test Rentier.sln --no-build -c Release --collect:"XPlat Code Coverage" --results-directory ./coverage --logger "github;group=Summary"`, `Upload coverage artifact` using `actions/upload-artifact@v4` with `path: ./coverage`; post step summary using a dedicated step with `shell: bash` to ensure cross-platform compatibility: `echo "## Test Results" >> $GITHUB_STEP_SUMMARY`; the `shell: bash` directive works on both Windows (Git Bash) and macOS runners; add YAML comment in ci.yml: `# NOTE: Coverage enforcement gate (Domain 100%, Application 90%) MUST be added in the first feature that introduces a real Application-layer use case. Intentionally absent from scaffold CI per spec assumption A-008.`
- [x] T069 [US5] Verify `.csproj` files: after applying Directory.Build.props (T067), confirm no duplicate `<Nullable>`, `<TreatWarningsAsErrors>`, or `<TargetFramework>` entries remain in individual .csproj files — run `dotnet build Rentier.sln -warnaserror` locally and confirm still passes with zero warnings
- [x] T070 [US5] Create a `develop` branch in Git if it does not already exist; push the feature branch and open a draft pull request targeting `develop`; observe the GitHub Actions run; confirm both `windows-latest` and `macos-latest` jobs reach green; confirm zero warning annotations and ≥8 passing tests in the summary; save CI run URL in `.specify/specs/001-initial-setup/ci-baseline.txt` (informational reference only)

**Checkpoint**: CI green on both platforms. Zero warnings. ≥8 tests pass. Coverlet XML artifact uploaded.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, security scan, constitution compliance verification, and feature sign-off.

- [x] T071 [P] Run `grep -rn "double\|float\|DateTime" src/Rentier.Domain/` from repository root and confirm zero matches — validates constitution Principle III (no `double`/`float` for monetary fields, no `DateTime` for date fields); also run `grep -rn "double\|float\|DateTime" src/Rentier.Application/` and confirm zero matches
- [x] T072 [P] Verify project reference graph by running `dotnet list src/Rentier.Domain/ reference`, `dotnet list src/Rentier.Application/ reference`, `dotnet list src/Rentier.Infrastructure/ reference`, `dotnet list src/Rentier.Desktop/ reference`; confirm: Domain has 0 refs, Application has 1 (Domain), Infrastructure has 2 (Domain + Application), Desktop has 2 (Application + Domain); confirm no Desktop → Infrastructure reference exists at this stage
- [x] T073 [P] Run security scan: `grep -rni "password\|imap.*pass\|jmbg\|secret\|credential.*=.*['\"]" src/ tests/ .github/ --include="*.cs" --include="*.axaml" --include="*.csproj" --include="*.yml"` from repository root; confirm zero sensitive-data literals in any committed file (constitution Principle II / CA-003)
- [x] T074 Run final validation: `dotnet test Rentier.sln -warnaserror` — confirm ≥8 tests pass, 0 fail, 0 skip, 0 warnings; run `dotnet build Rentier.sln -warnaserror` as final gate; update `.specify/tasks/sdlc-tasks.md` or equivalent task tracker to mark `001-initial-setup` as "Implementation Complete"

---

## Dependencies & Execution Order

### Phase Dependencies

```text
Phase 1 (Setup)
    ↓ no blocking dependencies — can start immediately
Phase 2 (Foundational)
    ↓ BLOCKS all user story phases — solution + .csproj must exist first
Phase 3 (US1 — Solution Scaffolding)
    ↓ BLOCKS Phase 4–6 — domain stubs and test project content must compile
Phase 4 (US2 — Domain Foundations)
    ↓ BLOCKS Phase 5 — repository interfaces reference domain types
Phase 5 (US3 — Application Contracts)
    ↓ BLOCKS Phase 6 — ViewModels and DI reference Application interfaces
Phase 6 (US4 — Desktop Shell)
    ↓ BLOCKS Phase 7 — shell must work before CI gate is meaningful
Phase 7 (US5 — CI Green)
    ↓ Feeds into Phase 8
Phase 8 (Polish)
    ← final: depends on all prior phases
```

### User Story Dependencies

| Story | Depends On | Can Parallelise With |
|-------|-----------|---------------------|
| US1 — Solution Scaffolding | Phase 2 complete | — |
| US2 — Domain Foundations | US1 complete | — |
| US3 — Application Contracts | US2 complete (interfaces ref domain types) | — |
| US4 — Desktop Shell | US3 complete (ViewModels ref Application interfaces) | — |
| US5 — CI Green | US4 complete (CI runs the full test suite) | — |

> **Sequential by design**: Each user story in this feature introduces types used by the next story. Parallel execution is limited to within-phase task parallelism (see `[P]` markers).

### Within-Phase Parallel Tasks

**Phase 2 (Foundational)**: T006, T007, T008 run in parallel after T005; T010, T011, T012 run in parallel after T009.

**Phase 3 (US1)**: T015, T016, T017 run in parallel after T014; T018, T019 run in parallel; T021–T026 run after T020.

**Phase 4 (US2)**: T029, T030, T031, T032, T033, T034, T035, T036 all run in parallel after T028.

**Phase 5 (US3)**: T040, T041, T042, T043, T044, T045, T046, T047, T048 all run in parallel after T039.

**Phase 6 (US4)**: T051, T052 run in parallel; T054, T055, T056, T058 run in parallel after T053 and T057; T061, T062, T063 run in parallel after T059.

**Phase 8 (Polish)**: T071, T072, T073 all run in parallel before T074.

---

## Parallel Example: Phase 4 (Domain Foundations)

```text
# After T028 (FilingStatusTransitionTests written and failing), launch in parallel:

Task: T029 — Create src/Rentier.Domain/ValueObjects/Money.cs
Task: T030 — Create src/Rentier.Domain/ValueObjects/MailboxCursor.cs
Task: T031 — Create src/Rentier.Domain/ValueObjects/ExchangeRate.cs
Task: T032 — Create src/Rentier.Domain/ValueObjects/HolidayConf.cs
Task: T033 — Create src/Rentier.Domain/Entities/TaxpayerProfile.cs
Task: T034 — Create src/Rentier.Domain/Entities/Mailbox.cs
Task: T035 — Create src/Rentier.Domain/Entities/Importer.cs
Task: T036 — Create src/Rentier.Domain/Entities/Report.cs

# Then sequentially (Filing depends on FilingStatus which may be in same file):
Task: T037 — Create src/Rentier.Domain/Entities/Filing.cs (+ FilingStatus enum)

# Then validate:
Task: T038 — dotnet test + grep scan
```

---

## Parallel Example: Phase 5 (Application Contracts)

```text
# After T039 (DiRegistrationSmokeTests written and failing), launch in parallel:

Task: T040 — Create src/Rentier.Application/Interfaces/ICommandHandler.cs
Task: T041 — Create src/Rentier.Application/Interfaces/IQueryHandler.cs
Task: T042 — Create src/Rentier.Application/Interfaces/ICredentialStore.cs
Task: T043 — Create src/Rentier.Application/Repositories/IFilingRepository.cs
Task: T044 — Create src/Rentier.Application/Repositories/IReportRepository.cs
Task: T045 — Create src/Rentier.Application/Repositories/IMailboxRepository.cs
Task: T046 — Create src/Rentier.Application/Repositories/IImporterRepository.cs
Task: T047 — Create src/Rentier.Application/Repositories/ITaxpayerProfileRepository.cs
Task: T048 — Create src/Rentier.Application/Repositories/IExchangeRateCacheRepository.cs

# Then sequentially (depends on ICredentialStore existing):
Task: T049 — Update OsCredentialStore.cs to implement ICredentialStore

# Then validate:
Task: T050 — dotnet test + grep scan
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T003)
2. Complete Phase 2: Foundational (T004–T013) — **CRITICAL, blocks everything**
3. Complete Phase 3: User Story 1 (T014–T027)
4. **STOP and VALIDATE**: `dotnet build Rentier.sln -warnaserror` passes; `dotnet test` shows ≥4 passing tests; check in and tag as "scaffold-compiles"
5. Proceed to Phase 4 when ready

### Incremental Delivery

1. Phase 1 + Phase 2 → All projects created and in solution
2. Phase 3 (US1) → Build compiles with stubs; **first working baseline**
3. Phase 4 (US2) → Domain types present; Filing state machine enforced
4. Phase 5 (US3) → Application contracts defined; DI smoke tests pass
5. Phase 6 (US4) → Desktop shell visible; sidebar navigation works
6. Phase 7 (US5) → CI green on Windows + macOS; Coverlet reports produced
7. Phase 8 → Security scan clean; constitution compliance confirmed; feature closed

### Single Developer Strategy

With a single developer, execute phases sequentially. Within each phase, tackle all `[P]`-marked tasks before the non-parallel tasks to maximise throughput. Use `dotnet build -warnaserror` as a fast feedback loop after each task group.

---

## Notes

- `[P]` tasks write to different files with no incomplete-task dependencies — safe to run concurrently
- `[US#]` label maps each task to its acceptance criteria in spec.md for traceability
- All test methods follow `MethodName_StateUnderTest_ExpectedBehavior` convention (constitution Principle V)
- **Forbidden types**: `double`, `float` in monetary/rate fields; `DateTime` in domain date fields — enforced by T071 grep scan
- **Forbidden patterns**: `.Result`, `.Wait()` on Tasks — constitution Principle IV
- **Forbidden references**: Domain → any I/O package; Application → EF Core/MailKit; Desktop ViewModels → Infrastructure classes directly
- Commit after each phase checkpoint using Conventional Commits format (e.g., `feat(scaffold): add domain foundation types`)
- Stop at each phase **Checkpoint** to validate before proceeding
