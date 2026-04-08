# Feature Specification: Rentier Initial Project Setup

**Feature Branch**: `001-initial-setup`  
**Created**: 2026-04-06  
**Status**: Draft  
**Input**: Scaffold the entire Rentier solution from scratch — four Clean Architecture projects, domain stubs, application contracts, infrastructure skeleton, desktop shell, DI wiring, test infrastructure, and CI/CD pipeline.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Solution Scaffolding (Priority: P1)

A developer clones the repository and runs a single `dotnet build` command at the solution root. The entire solution compiles with zero errors and zero warnings across all four main projects and all test projects.

**Why this priority**: The foundation every subsequent feature depends on. Nothing else can be built, tested, or reviewed until the solution structure is correct and stable.

**Independent Test**: Clone a fresh copy of the repository, run `dotnet build Rentier.sln -warnaserror` on both Windows and macOS, and verify exit code 0 with no diagnostic messages.

**Acceptance Scenarios**:

1. **Given** a developer has cloned the repository on Windows, **When** they run `dotnet build Rentier.sln`, **Then** all four main projects and all test projects compile successfully with zero errors and zero warnings.
2. **Given** a developer has cloned the repository on macOS, **When** they run `dotnet build Rentier.sln`, **Then** the solution compiles successfully with zero errors and zero warnings.
3. **Given** the solution is open, **When** a developer inspects project references, **Then** `Rentier.Domain` has no project references, `Rentier.Application` references only `Rentier.Domain`, `Rentier.Infrastructure` references `Rentier.Domain` and `Rentier.Application`, and `Rentier.Desktop` references `Rentier.Application` and `Rentier.Domain`.
4. **Given** any project in the solution, **When** a developer checks its target framework, **Then** it targets `net8.0` exclusively.

---

### User Story 2 — Domain Foundations (Priority: P2)

A developer opens `Rentier.Domain` and sees all nine domain entities and value objects as compilable stubs with correct C# types, without any persistence or framework dependencies.

**Why this priority**: The domain model is the shared vocabulary of the entire application. All other layers reference domain types; stubs must exist before Application or Infrastructure code can compile.

**Independent Test**: Add a reference to `Rentier.Domain` in an isolated test project, construct instances of all nine domain types, and assert the types are accessible without errors.

**Acceptance Scenarios**:

1. **Given** `Rentier.Domain` is compiled, **When** a developer inspects it, **Then** they find stub type declarations for `TaxpayerProfile`, `Mailbox`, `MailboxCursor`, `Importer`, `Report`, `Filing`, `Money`, `ExchangeRate`, and `HolidayConf`.
2. **Given** the `Money` value object, **When** a developer inspects its `Amount` property, **Then** its type is `decimal` (not `double` or `float`).
3. **Given** the `ExchangeRate` value object, **When** a developer inspects its `Date` property, **Then** its type is `DateOnly`.
4. **Given** the `Filing` aggregate root, **When** a developer inspects its status, **Then** it reflects the three-state lifecycle: `init`, `filed`, `paid`.
5. **Given** `Rentier.Domain`, **When** a developer inspects its NuGet references, **Then** there are no I/O-bound or persistence packages (no Entity Framework, no MailKit, no HttpClient).
6. **Given** an invalid Filing status transition is attempted (e.g., `paid → init`), **When** the domain enforces the invariant, **Then** a `DomainException` is thrown.

---

### User Story 3 — Application Contracts (Priority: P3)

A developer opens `Rentier.Application` and sees all repository interfaces and CQRS handler interfaces as empty but compilable contract definitions.

**Why this priority**: Application contracts define the boundary between use-case logic and infrastructure. They must exist as stubs before Infrastructure can provide implementations.

**Independent Test**: Reference `Rentier.Application` in an isolated test project, declare a mock class implementing each interface, and confirm the code compiles with zero errors.

**Acceptance Scenarios**:

1. **Given** `Rentier.Application` is compiled, **When** a developer inspects it, **Then** they find the six repository interface declarations: `IFilingRepository`, `IReportRepository`, `IMailboxRepository`, `IImporterRepository`, `ITaxpayerProfileRepository`, and `IExchangeRateCacheRepository`.
2. **Given** `Rentier.Application` is compiled, **When** a developer inspects it, **Then** they find generic handler interfaces: `ICommandHandler<TCommand, TResult>` and `IQueryHandler<TQuery, TResult>`.
3. **Given** `Rentier.Application`, **When** a developer inspects it, **Then** they find an `ICredentialStore` interface stub with no credential-storage implementation details.
4. **Given** `Rentier.Application`, **When** a developer checks its NuGet references, **Then** there are no EF Core or MailKit references.

---

### User Story 4 — Desktop Shell (Priority: P4)

A developer launches the compiled application and sees a working native desktop window with a sidebar navigation panel and placeholder pane content for Filings, Reports, and Settings destinations.

**Why this priority**: The shell is the visible face of the application. Verifying it launches proves DI wiring, Avalonia bootstrapping, and the MVVM scaffold are all connected correctly end-to-end.

**Independent Test**: Run `dotnet run --project src/Rentier.Desktop`, confirm the window opens, confirm the three navigation entries are visible in the sidebar, click each one, and verify a non-empty placeholder pane renders without crashing.

**Acceptance Scenarios**:

1. **Given** the application is compiled and started, **When** the main window appears, **Then** a sidebar with at least three named destinations (Filings, Reports, Settings) is visible.
2. **Given** the application is running, **When** a developer clicks a sidebar entry, **Then** the content area updates to show the corresponding placeholder pane without any error or crash.
3. **Given** the application is running, **When** a developer inspects any ViewModel, **Then** it inherits from or uses `ReactiveUI` and the view is a `ReactiveUserControl<TViewModel>`.
4. **Given** the application starts, **When** its DI composition root runs, **Then** all registered Application and Infrastructure services resolve without exceptions.
5. **Given** any user-visible navigation label, **When** a developer inspects the source, **Then** the string is declared in `Resources/Strings.resx` and not hard-coded in XAML or code-behind.

---

### User Story 5 — CI Green (Priority: P5)

Every push to `develop` or `main`, and every pull request targeting `develop`, triggers a GitHub Actions workflow that builds the solution and runs all tests on both Windows and macOS with zero warnings and zero test failures.

**Why this priority**: A reproducible CI baseline is a prerequisite for collaborative development. No subsequent feature can be integrated safely without a passing CI gate.

**Independent Test**: Create a test pull request to `develop`, observe the GitHub Actions run, confirm both matrix jobs (Windows and macOS) show green status, zero annotation warnings, and all test cases pass.

**Acceptance Scenarios**:

1. **Given** a push to `develop`, **When** the CI workflow runs, **Then** the build step exits with code 0 on both `windows-latest` and `macos-latest` runners with zero compiler warnings.
2. **Given** a pull request targeting `develop`, **When** the CI workflow runs, **Then** all unit tests pass on both platforms and a coverage summary is posted to the job step summary.
3. **Given** the CI pipeline, **When** any compiler warning is introduced, **Then** the build step fails (treating warnings as errors is enabled).
4. **Given** the CI pipeline, **When** the workflow completes successfully, **Then** a Coverlet XML coverage report is produced as a workflow artifact or step summary.

---

### Edge Cases

- What happens if a developer attempts to add an EF Core reference to `Rentier.Domain`? The CI build MUST fail due to layer-boundary rules encoded in `.editorconfig` or a custom analyzer, or at minimum be detectable during code review via constitution check.
- What happens if the Avalonia application is run on Linux? The Desktop project targets `net8.0` (cross-platform) so it should build; runtime support on Linux is a best-effort and is not a P1 requirement for this feature.
- What happens if a developer runs `dotnet test` before any real test logic is added? At least one smoke test per layer must exist so the test run reports a non-zero test count rather than "no tests found."

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The solution MUST be named `Rentier` and contain exactly four main C# projects: `Rentier.Domain`, `Rentier.Application`, `Rentier.Infrastructure`, and `Rentier.Desktop`, all referencing `net8.0` as the target framework.
- **FR-002**: The solution MUST include test projects for all four layers: `Rentier.Domain.Tests`, `Rentier.Application.Tests`, `Rentier.Infrastructure.Tests`, and `Rentier.Desktop.Tests`, each containing at least one passing smoke test.
- **FR-003**: Project references MUST enforce inward-only Clean Architecture dependency rules:
  - `Rentier.Domain` → no project references
  - `Rentier.Application` → `Rentier.Domain` only
  - `Rentier.Infrastructure` → `Rentier.Domain` + `Rentier.Application`
  - `Rentier.Desktop` → `Rentier.Application` + `Rentier.Domain`
- **FR-004**: `Rentier.Domain` MUST declare stub type definitions for all nine domain entities and value objects: `TaxpayerProfile`, `Mailbox`, `MailboxCursor`, `Importer`, `Report`, `Filing`, `Money`, `ExchangeRate`, and `HolidayConf`. Value objects MUST be declared as `record` types.
- **FR-005**: The `Money` value object MUST declare `decimal Amount` and `string Currency`. The `ExchangeRate` value object MUST declare `DateOnly Date`, `string Currency`, and `decimal RateToRsd`. No `double` or `float` types are permitted in domain monetary or date fields.
- **FR-006**: The `Filing` aggregate root MUST model the three-state lifecycle (`init → filed → paid`) and MUST throw `DomainException` for invalid state transitions.
- **FR-007**: `Rentier.Domain` MUST NOT reference any NuGet package that performs I/O (no Entity Framework Core, no MailKit, no HttpClient wrappers).
- **FR-008**: `Rentier.Application` MUST declare empty interface definitions for the six repository contracts: `IFilingRepository`, `IReportRepository`, `IMailboxRepository`, `IImporterRepository`, `ITaxpayerProfileRepository`, and `IExchangeRateCacheRepository`.
- **FR-009**: `Rentier.Application` MUST declare generic CQRS handler interfaces: `ICommandHandler<TCommand, TResult>` and `IQueryHandler<TQuery, TResult>`.
- **FR-010**: `Rentier.Application` MUST declare an `ICredentialStore` interface stub for OS credential store abstraction. This interface MUST NOT contain any OS-specific implementation references.
- **FR-011**: `Rentier.Application` MUST NOT reference EF Core, MailKit, or any infrastructure package directly.
- **FR-012**: `Rentier.Infrastructure` MUST contain an `AppDbContext` class inheriting from EF Core `DbContext` (using the SQLite provider), with no `DbSet<>` properties or entity configurations yet. An empty initial migration named `0001_InitialCreate` MUST be generated.
- **FR-013**: `Rentier.Infrastructure` MUST contain a stub implementation of `ICredentialStore` that delegates to the OS credential store (Windows Credential Locker on Windows, macOS Keychain on macOS), with method bodies returning `NotImplementedException` until a later feature implements the full logic.
- **FR-014**: `Rentier.Desktop` MUST contain a valid Avalonia application entry point (`App.axaml` / `App.axaml.cs`) using `FluentTheme`.
- **FR-015**: `Rentier.Desktop` MUST contain a `MainWindow` with a sidebar navigation layout hosting at least three top-level destinations: **Filings**, **Reports**, and **Settings**. Each destination renders a placeholder `ReactiveUserControl<TViewModel>` view.
- **FR-016**: `Rentier.Desktop` MUST contain a DI composition root using `Microsoft.Extensions.DependencyInjection` that registers all Desktop-layer services (ViewModels) and makes them resolvable at application startup. Infrastructure service registration (`ICredentialStore → OsCredentialStore`) is deferred to the IMAP mailbox feature, when `Rentier.Infrastructure` will provide a DI extension method callable from the Desktop startup path.
- **FR-017**: `Rentier.Desktop` MUST use `ReactiveUI` for ViewModel bindings and `CommunityToolkit.Mvvm` source generators for `[ObservableProperty]` on ViewModels.
- **FR-018**: All user-visible strings (navigation labels, window title) MUST be declared in `Resources/Strings.resx` inside `Rentier.Desktop` and MUST NOT be hard-coded inline.
- **FR-019**: IMAP credentials MUST NOT appear anywhere in the scaffold. The `ICredentialStore` interface (defined in `Rentier.Application`) MUST be the sole abstraction for any secret storage.
- **FR-020**: A `.github/workflows/ci.yml` workflow MUST be created that triggers on `push` to `develop` and `main`, and on `pull_request` targeting `develop`. It MUST run on `windows-latest` and `macos-latest` in a matrix strategy with steps: checkout → setup .NET 8 → restore → build (with warnings-as-errors) → test (with Coverlet XML output) → post coverage summary.
- **FR-021**: A `.editorconfig` file MUST be committed at the repository root enforcing C# 12 style rules and Conventional Commits-aligned settings.
- **FR-022**: A `.gitignore` file MUST be committed at the repository root covering .NET build artefacts, IDE files, and OS-specific metadata.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature establishes the four-layer Clean Architecture foundation. `Rentier.Domain` is the innermost layer with no outbound project or I/O package references. `Rentier.Application` depends on Domain only. `Rentier.Infrastructure` implements Application contracts. `Rentier.Desktop` consumes Application use-case interfaces. The project reference graph MUST be validated at build time. No circular dependencies are permitted. *(Constitution Principle I)*
- **CA-002 (Money and Dates)**: The `Money.Amount` field MUST be `decimal`. The `ExchangeRate.RateToRsd` field MUST be `decimal`. The `ExchangeRate.Date` field MUST be `DateOnly`. All monetary and date field declarations in this scaffold establish the authoritative type contract for all future features. *(Constitution Principle III)*
- **CA-003 (Privacy and Security)**: No IMAP credentials, passwords, JMBG values, or sensitive personal data appear in any source file, configuration file, or migration script created by this feature. The `ICredentialStore` interface stub is the sole placeholder for secret access, and its Infrastructure implementation delegates to OS-managed secure storage. *(Constitution Principle II)*
- **CA-004 (Network Scope)**: This feature creates zero outbound network calls. The Infrastructure stub has no active HTTP or IMAP client invocations. Outbound network access scope (IMAP + NBS endpoints only) applies to future features. *(Constitution Principle II)*
- **CA-005 (Async and UI)**: No I/O operations exist in this scaffold, so there are no blocking async violations. However, the `ReactiveCommand.CreateFromTask` pattern MUST be documented in `MainWindowViewModel` comments as the expected pattern for future UI-initiated async operations, ensuring future developers follow the correct approach. *(Constitution Principle IV)*
- **CA-006 (Testing Impact)**: Each of the four layers receives a dedicated test project. Each test project MUST contain at least one passing smoke test to ensure the CI runner never reports "no tests found." Domain tests cover the `Filing` status transition invariant (invalid transition → `DomainException`). Application tests cover DI service registration. This baseline satisfies the "Specification-Driven Quality Gates" principle. *(Constitution Principle V)*

### Key Entities

- **TaxpayerProfile** (Entity): Represents the Serbian taxpayer. Key stub fields: JMBG (string), full name (string), address (string), opstina code (string). No persistence logic in this feature.
- **Mailbox** (Entity): Represents an IMAP mailbox connection configuration. Key stub fields: host (string), port (int), username (string). Contains a `MailboxCursor` value object for sync state.
- **MailboxCursor** (Value Object — `record`): Represents the last-synced position in a mailbox. Key stub fields: last sync date (`DateOnly`) or UID (`long`).
- **Importer** (Entity): Represents an IBKR CSV import configuration filter. Key stub fields: display name (string), filter predicate stub.
- **Report** (Entity): Represents a parsed activity statement from an imported CSV. Key stub fields: import date (`DateOnly`), source importer reference.
- **Filing** (Aggregate Root): Represents a PP-OPO tax filing. Key stub fields: status (`FilingStatus` enum — `Init`, `Filed`, `Paid`), tax period (`DateOnly`). Status transitions enforced in domain.
- **Money** (Value Object — `record`): Represents a monetary amount with currency. Fields: `decimal Amount`, `string Currency`.
- **ExchangeRate** (Value Object — `record`): Represents an NBS exchange rate. Fields: `DateOnly Date`, `string Currency`, `decimal RateToRsd`.
- **HolidayConf** (Value Object — `record`): Represents a list of public holidays used for deadline calculation. Fields: `IReadOnlyList<DateOnly> Holidays`.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can clone the repository and execute a full solution build on both Windows and macOS in under 5 minutes from a cold start, with zero compiler errors and zero warnings.
- **SC-002**: All four layers (`Domain`, `Application`, `Infrastructure`, `Desktop`) produce independently buildable output artefacts; removing any one project reference from the chain breaks the build at the dependent project, confirming correct dependency isolation.
- **SC-003**: The Avalonia desktop application launches to the main window within 3 seconds on a standard developer machine, displays the sidebar with three navigation entries, and responds to navigation clicks without crashing.
- **SC-004**: Running `dotnet test Rentier.sln` produces at least four passing tests (one smoke test per layer) with zero failing tests and zero skipped tests.
- **SC-005**: The GitHub Actions CI pipeline reaches a green status on both `windows-latest` and `macos-latest` runners for every push and pull request, with zero warning annotations and a coverage summary posted to the job step summary.
- **SC-006**: A developer performing a code review can confirm all monetary fields use `decimal` and all business date fields use `DateOnly` in the domain model stubs, with no `double`, `float`, or `DateTime` types present.
- **SC-007**: No credentials, passwords, JMBG values, or other sensitive data appear anywhere in the committed source files, configuration, or migration output, as verified by a grep scan of the repository.

---

## Assumptions

- **A-001**: The Avalonia UI framework version used is 11.x (the current stable major release). If version 12 is released before this feature lands, no upgrade is required; that would be a separate constitution amendment.
- **A-002**: The target framework `net8.0` is the sole TFM. No multi-targeting (`net8.0;net9.0`) is included in this scaffold. An upgrade to .NET 9 is deferred to a dedicated upgrade task.
- **A-003**: The placeholder navigation destinations (Filings, Reports, Settings) render static "Coming soon" or equivalent placeholder content. No ViewModels for these panes implement any data loading or command logic in this feature.
- **A-004**: The `AppDbContext` EF Core migration (`0001_InitialCreate`) creates an empty schema (no tables). Entity `DbSet<>` mappings are added as each domain entity's persistence feature is specified and implemented.
- **A-005**: The `ICredentialStore` infrastructure stub on Windows uses `Windows.Security.Credentials.PasswordVault` and on macOS uses `Security.SecKeychain`. Both method bodies initially throw `NotImplementedException`; full implementation is deferred to the feature that implements IMAP mailbox configuration.
- **A-006**: Test projects use xUnit as the test framework, FluentAssertions for assertion syntax, and NSubstitute for mock creation. No other test frameworks are introduced.
- **A-007**: The `.editorconfig` encodes C# 12 rules consistent with `sealed` class preferences, `record` value-object conventions, async naming, and Conventional Commits message format. Settings do not enforce Roslyn analyzers that would require additional NuGet package installation beyond what is already included.
- **A-008**: The GitHub Actions workflow does NOT enforce a minimum code coverage percentage for this feature (no coverage gate). Coverage gating is introduced when the first Application-layer use case is implemented. The pipeline only posts a coverage summary for visibility.
- **A-009**: Linux is not an officially supported runtime platform at this stage. The solution targets `net8.0` and will compile on Linux, but no CI matrix job runs on `ubuntu-latest` until cross-platform requirements are formally specified.
- **A-010**: The feature description's reference to ".NET MAUI" was a drafting error and is superseded by the project constitution, which mandates Avalonia UI 11+ as the authoritative UI framework. This tradeoff is documented in `clarify.md` and no further MAUI evaluation is required.

---

## Out of Scope

The following items are explicitly excluded from this feature to maintain a clean scaffold boundary:

- Business logic implementation (CSV import parsing, NBS exchange-rate scraping, XML PP-OPO generation, tax calculation algorithms)
- EF Core `DbSet<>` entity mappings or data seeding
- Full IMAP credential storage implementation (interface stub only)
- Avalonia UI automation tests
- Installer, packaging, or distribution setup
- `net9.0` TFM or multi-targeting
- Any outbound network calls (HTTP, IMAP)
- Hard code coverage enforcement gates in CI
- Linux CI matrix job
