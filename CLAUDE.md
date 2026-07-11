# Rentier — Claude Code Project Guide

## Identity
Rentier is a cross-platform **C# + Avalonia** desktop application that automates
**Serbian PP-OPO passive income tax filing** for foreign brokerage income (dividends,
interest). It parses IBKR Activity Statements, fetches NBS exchange rates, computes
15% Serbian tax with withholding credit, and exports submission-ready PP-OPO XML.
Financial correctness is the highest priority — treat every change touching money,
dates, or tax logic as safety-critical.

## Architecture: Clean Architecture (strict layering)
Dependencies only point inward. If you're about to put EF Core in Domain, or call a
repository directly from a ViewModel — stop and route through Application instead.

```
src/Rentier.Domain          no external dependencies — pure C# records & interfaces
src/Rentier.Application     references Domain only — CQRS commands/queries, IRepository interfaces
src/Rentier.Infrastructure  implements Application interfaces — EF Core, MailKit, HttpClient
src/Rentier.Desktop         Avalonia UI — calls Application use cases only, never Infrastructure directly
```

Layer-specific constraints are auto-loaded from `.claude/rules/` based on which files
you're editing. Read `.claude/skills/clean-architecture/SKILL.md` for the full layer
map, ViewModel pattern, navigation model, and UX contracts before adding a feature.

## Directory Map
```
src/Rentier.Domain/          Entities, value objects (Money, MailboxCursor, HolidayConf), DomainException
src/Rentier.Application/     Commands/, Handlers/, Queries/, DTOs, IRepository interfaces
src/Rentier.Infrastructure/  Persistence (EF Core/SQLite), ExchangeRates (NbsExchangeRateFetcher), Parsing (IbkrCsvParser), Serialization (PpOpoXmlSerializer), mail sync (MailKit)
src/Rentier.Desktop/         Views/ (Avalonia XAML), ViewModels/, Composition/, Services/, Converters/, Dialogs/, Program.cs
tests/Rentier.UnitTests/           Domain + Application tests (no I/O)
tests/Rentier.Infrastructure.Tests/  EF Core/SQLite, parser, serializer integration tests
tests/Rentier.Scenarios.Tests/     End-to-end scenario tests
tests/Rentier.Tests.Common/        Shared test fixtures/builders
```

## Common Commands
```bash
dotnet restore Rentier.slnx
dotnet build Rentier.slnx --no-restore -c Release
dotnet format Rentier.slnx --no-restore --verify-no-changes

# Unit + Application tests (excludes integration)
dotnet test Rentier.slnx --filter "Category!=Integration"

# Infrastructure/migration integration tests
dotnet test tests/Rentier.Infrastructure.Tests --filter "Category=Integration"

# Run the desktop app
dotnet run --project src/Rentier.Desktop/Rentier.Desktop.csproj
```
CI (`.github/workflows/ci.yml`) runs format + vulnerable-package check → migration tests
→ build/test matrix (Windows/macOS/Linux) → coverage merge → SonarCloud. Match this
locally before pushing (`dotnet list Rentier.slnx package --vulnerable --include-transitive`
catches the package gate).
The canonical remote is `github` (github.com/dj-milenkovic/rentier) — CI and PRs run there.

## Absolute Rules (non-negotiable)
1. **`decimal` only** for all monetary values, tax amounts, exchange rates, percentages.
2. **`DateOnly`** for all dates. Convert external `DateTime` only at the infrastructure boundary.
3. **No passwords in SQLite.** Always use the OS credential store.
4. **All async.** Every I/O method is `async Task<T>`. No `.Result`, no `.Wait()`.
5. **No UI thread blocking.** Use `ReactiveCommand.CreateFromTask`.
6. **Result pattern.** Infrastructure returns `Result<T, Error>`. No exception-as-flow-control.

## CQRS Pattern
When implementing a feature, create:
- A `*Command` record in `Rentier.Application/Commands/`
- A `*CommandHandler` in `Rentier.Application/Handlers/`
- Or a `*Query` + `*QueryHandler` for reads

## Domain Model Enforcement
- `Filing` status transitions are enforced in Domain: `init → filed → paid`. Invalid
  transitions throw `DomainException`.
- `Money` is a value object: `(decimal Amount, string Currency)`.
- `MailboxCursor` is a discriminated union via `abstract record`.

## Testing Standards
- xUnit + FluentAssertions + NSubstitute.
- Domain tests: no mocks — test pure logic.
- Application tests: mock repositories with NSubstitute.
- Infrastructure tests: EF Core InMemory or SQLite in-memory.
- Naming: `MethodName_StateUnderTest_ExpectedBehavior`.
- See `.claude/skills/rentier-unit-tests`, `rentier-ui-tests`, `rentier-integration-tests` for detailed playbooks.

## Domain Knowledge
- PP-OPO deadline = payment date + 30 days, shifted to the next business day if it
  falls on a weekend or Serbian public holiday.
- Serbian public holidays are configured in the `HolidayConf` value object.
- Withholding tax credit cannot exceed computed Serbian tax (15%).
- NBS exchange rates are fetched per-date and cached in SQLite.
- IBKR CSV: only process "Dividends" and "Interest" activity sections.
- Before changing tax computation, deadlines, exchange-rate handling, or PP-OPO XML,
  consult `.claude/skills/rentier-tax-rules` — it documents the rules as implemented.

## Where to Look Next
- **`.claude/rules/`** — path-scoped constraints for each layer (Domain, Application,
  Infrastructure, Desktop, Tests), auto-loaded only when you touch matching files.
- **`.claude/skills/`** — deep-dive playbooks (architecture, EF Core, async, xUnit,
  UI design/tests, package upgrades). Claude loads these on demand — invoke by name
  or let auto-matching trigger them.
- **`.claude/agents/`** — specialized subagents (C# expert, .NET janitor, .NET upgrade,
  accessibility review, Avalonia UX design) for isolated side-tasks via the Agent tool.
- **`README.md`, `GETTING-STARTED.md`, `IBKR-SETUP.md`, `TAX-OVERVIEW.md`** — product
  and domain documentation.
- **`docs/TESTING.md`** — test types, frameworks, trait/CI contract, and the
  add-X-add-Y checklist. Read before writing or reviewing tests.
