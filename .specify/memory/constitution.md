<!--
Sync Impact Report
- Version change: 0.0.0 -> 1.0.0
- Modified principles:
	- Template Principle 1 -> I. Clean Architecture Dependency Rule (NON-NEGOTIABLE)
	- Template Principle 2 -> II. Local-First Security and Privacy
	- Template Principle 3 -> III. Financial and Temporal Correctness
	- Template Principle 4 -> IV. Async and UI Responsiveness
	- Template Principle 5 -> V. Specification-Driven Quality Gates
- Added sections:
	- Mission
	- Architecture: Clean Architecture
	- Technology Stack
	- Application Layer Patterns
	- Domain Model
	- Coding Standards
	- Testing Requirements
	- Git Workflow
	- AI Development Rules
	- Definition of Done
	- Domain Glossary
- Removed sections:
	- None
- Templates requiring updates:
	- ✅ .specify/templates/plan-template.md
	- ✅ .specify/templates/spec-template.md
	- ✅ .specify/templates/tasks-template.md
	- ⚠ pending: .specify/templates/commands/*.md (directory not present in repository)
- Deferred TODOs:
	- None
-->

# Rentier Constitution

## Core Principles

### I. Clean Architecture Dependency Rule (NON-NEGOTIABLE)
Rentier MUST follow Clean Architecture with inward-only dependencies. `Rentier.Domain`
MUST remain independent of external frameworks and I/O packages. `Rentier.Application`
MUST depend only on Domain and define use-case and repository contracts.
`Rentier.Infrastructure` MAY implement Application contracts but MUST NOT leak outward
concerns into Domain. `Rentier.Desktop` MUST call Application use cases only and MUST
NOT directly invoke repository or infrastructure services.
Rationale: strict boundaries preserve testability, long-term maintainability, and safe
refactoring.

### II. Local-First Security and Privacy
Rentier MUST store all user data locally in SQLite and MUST NOT use cloud sync,
accounts, or telemetry. Outbound network access is restricted to IMAP (user mailbox)
and NBS exchange-rate endpoints. IMAP passwords MUST be stored in OS credential stores
(Windows Credential Locker or macOS Keychain abstraction) and MUST NEVER be stored in
SQLite, plaintext files, or environment variables.
Rationale: tax and identity data are highly sensitive and require minimal exposure.

### III. Financial and Temporal Correctness
All monetary values, rates, tax values, and percentages MUST use `decimal`; `double`
and `float` are prohibited. All business dates MUST use `DateOnly`; any external
datetime representations MUST be converted at infrastructure boundaries. Domain rules,
including filing status transitions and deadline calculations, MUST be enforced in
Domain entities/value objects.
Rationale: tax correctness depends on deterministic arithmetic and explicit date models.

### IV. Async and UI Responsiveness
All I/O-bound operations MUST be `async Task`/`async Task<T>` and MUST NOT use `.Result`
or `.Wait()`. Desktop-layer workflows MUST NOT block the UI thread and MUST use
`ReactiveCommand.CreateFromTask` with UI updates scheduled via
`RxApp.MainThreadScheduler`.
Rationale: prevents deadlocks and keeps desktop UX responsive during network and file
operations.

### V. Specification-Driven Quality Gates
Implementation MUST be traceable to an approved task in `.specify/tasks/`. Domain code
MUST maintain 100% rule/state coverage, Application code MUST maintain at least 90%
coverage, and CI MUST stay warning-free across supported platforms. AI-generated code
MUST be reviewed for architecture-boundary compliance and monetary `decimal` correctness
before merge.
Rationale: explicit gates reduce regressions in a compliance-heavy domain.

## Technical Standards and Domain Constraints

### Mission
Rentier automates PP-OPO passive income (dividend and interest) tax filings for
Serbian taxpayers holding foreign brokerage accounts. It is a native cross-platform
desktop application built in C# and Avalonia UI.

### Architecture: Clean Architecture

```text
Rentier.Desktop        (outermost - knows everything below)
		down depends on
Rentier.Application    (use cases - knows Domain only)
		down depends on
Rentier.Domain         (innermost - knows nothing)
		up implemented by
Rentier.Infrastructure (knows Domain + Application interfaces)
```

#### Layer Responsibilities

| Project | Responsibility | Allowed References |
|---|---|---|
| `Rentier.Domain` | Entities, Value Objects, Domain Events, interfaces | None |
| `Rentier.Application` | Use Cases, CQRS Commands/Queries, DTOs, repository interfaces | Domain only |
| `Rentier.Infrastructure` | EF Core, MailKit, HttpClient, NBS scraper, XML serializer | Domain + Application |
| `Rentier.Desktop` | Avalonia Views, ViewModels, DI wiring, app startup | Application + Domain |

#### Forbidden
- `Domain` MUST NOT reference any NuGet package that performs I/O.
- `Application` MUST NOT reference EF Core or MailKit directly.
- `Desktop` MUST NOT directly call repositories or infrastructure services.
- Circular dependencies between projects are prohibited.

### Technology Stack

| Concern | Choice |
|---|---|
| UI | Avalonia UI 11+ with FluentTheme |
| MVVM | ReactiveUI + CommunityToolkit.Mvvm source generators |
| ORM | EF Core 8 with SQLite provider |
| IMAP | MailKit |
| HTTP | `System.Net.Http.HttpClient` (NBS scraping only) |
| XML | `System.Xml.Linq` (`XDocument`) |
| CSV | CsvHelper |
| DI | Microsoft.Extensions.DependencyInjection |
| Testing | xUnit + FluentAssertions + NSubstitute |
| CI | GitHub Actions (.NET 8, Windows + macOS matrix) |

### Application Layer Patterns

#### CQRS
Commands mutate state and queries read state. Handlers MUST return `Result<T>` or
`Result<T, Error>` for expected failures.

Commands: `CreateFilingCommand`, `SyncMailboxCommand`, `UpdateFilingStatusCommand`
Queries: `GetFilingsQuery`, `GetReportsQuery`, `GetMailboxCursorQuery`

#### Use Case Interface
Every use case MUST implement one of:
- `ICommandHandler<TCommand, TResult>`
- `IQueryHandler<TQuery, TResult>`

#### Repository Interfaces (defined in Application)
- `IFilingRepository`
- `IReportRepository`
- `IMailboxRepository`
- `IImporterRepository`
- `ITaxpayerProfileRepository`
- `IExchangeRateCacheRepository`

### Domain Model

| Type | Kind | Description |
|---|---|---|
| `TaxpayerProfile` | Entity | JMBG, name, address, opstina |
| `Mailbox` | Entity | IMAP connection + cursor |
| `MailboxCursor` | Value Object | Date-based or UID-based position |
| `Importer` | Entity | IBKR CSV filter config |
| `Report` | Entity | Parsed activity statement |
| `Filing` | Aggregate Root | PP-OPO filing with status machine |
| `Money` | Value Object | `decimal Amount` + `string Currency` |
| `ExchangeRate` | Value Object | `DateOnly Date`, `string Currency`, `decimal RateToRsd` |
| `HolidayConf` | Value Object | Holiday date list for deadline calculation |

Filing status machine enforced in Domain:

```text
init --(submit XML)--> filed --(confirm payment)--> paid
```

Invalid status transitions MUST throw `DomainException`.

### Coding Standards
- Use C# 12 language features where they improve clarity.
- Use `record` for value objects and DTOs.
- Mark concrete classes `sealed` unless inheritance is explicitly required.
- Domain models MUST avoid null state; use required constructors or `Option<T>`.
- Async methods MUST use `Async` suffix.
- Views MUST be `ReactiveUserControl<TViewModel>` with no view logic in code-behind.
- User-visible strings MUST be in `Resources/Strings.resx`.
- Dialogs MUST be async (`ShowDialog<T>`); direct modal message boxes are prohibited.

## Delivery Workflow and Quality Gates

### Testing Requirements
- Domain tests MUST cover every rule, calculation, and state transition.
- Application tests MUST target >=90% coverage and mock infrastructure dependencies.
- Infrastructure tests SHOULD use integration tests with SQLite in-memory provider.
- Desktop tests MUST cover ViewModels; UI automation is optional at this stage.
- Test naming MUST follow `MethodName_StateUnderTest_ExpectedBehavior`.

### Git Workflow
- `main` is always releasable.
- `develop` is the integration branch.
- Feature branches MUST be named `feature/TASK-XXX-short-name`.
- Commits MUST follow Conventional Commits.
- PRs MUST have green CI, no new warnings, and a linked spec task.

### AI Development Rules
- Specs MUST precede code; no implementation without a task in `.specify/tasks/`.
- Use `se-ux-ui-designer` for UX flow and journey design tasks.
- Use `CSharpExpert` for C# implementation guidance.
- Use `se-system-architecture-reviewer` for architecture decisions.
- AI-generated monetary logic MUST be reviewed for `decimal` usage.
- AI-generated infrastructure code MUST be reviewed for layer-boundary compliance.

### Definition of Done
- Spec task complete and marked in `.specify/tasks/`.
- Domain invariants tested (100%).
- Application use cases tested (90%+).
- No compiler warnings.
- PR reviewed and merged to `develop`.
- Constitution updated when architectural or policy decisions change.

### Domain Glossary

| Term | Meaning |
|---|---|
| PP-OPO | Serbian tax form for passive income |
| JMBG | Serbian personal ID number |
| Opstina | Serbian municipality code |
| ePorezi | Serbian Tax Administration portal |
| NBS | Narodna Banka Srbije, exchange-rate source |
| IBKR | Interactive Brokers, supported CSV format |
| Filing Deadline | Payment date + 30 days, adjusted for weekends/holidays |
| Withholding Tax | Tax deducted at source by paying entity |
| Cursor | Mailbox sync position (date or UID-based) |
| Rentier | Person living off passive income |

## Governance
This constitution is the highest-priority engineering policy for the repository.
When any document conflicts with this constitution, this constitution takes
precedence.

Amendment policy:
- Amendments MUST be submitted as pull requests with rationale and impact summary.
- Amendments MUST include updates to affected templates and guidance artifacts.
- Amendments MUST be approved by maintainers before merge.

Versioning policy:
- MAJOR: incompatible governance changes or principle removal/redefinition.
- MINOR: new principle/section or materially expanded obligations.
- PATCH: wording clarifications, typos, or non-semantic refinements.

Compliance review policy:
- Every implementation plan MUST include a Constitution Check against all principles.
- Code review MUST block merges that violate architecture boundaries or core type rules.
- CI and test coverage gates are mandatory enforcement mechanisms.

**Version**: 1.0.0 | **Ratified**: 2026-04-06 | **Last Amended**: 2026-04-06
