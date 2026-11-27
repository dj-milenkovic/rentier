# Contributing to Rentier

## Architecture

Rentier follows **Clean Architecture** with strict layer separation:

```
Rentier.Domain          → Pure C# records, value objects, domain logic (no external deps)
  ↑
Rentier.Application     → CQRS commands/queries, business rules, IRepository interfaces
  ↑
Rentier.Infrastructure  → EF Core, NBS scraper, IMAP sync, credential store
  ↑
Rentier.Desktop         → Avalonia UI, ReactiveUI ViewModels
```

**Key patterns:**
- **CQRS** – Commands (`*Command`) and Queries (`*Query`) with corresponding handlers
- **Result pattern** – Infrastructure returns `Result<T, Error>` (no exception-as-control-flow)
- **Value objects** – `Money`, `MailboxCursor`, `HolidayConf` enforce domain invariants
- **Dependency injection** – Composition root in `Rentier.Desktop/Composition/`

## Clone & Build

```bash
git clone https://github.com/djordje.milenkovic96/rentier.git
cd rentier
dotnet restore
dotnet build
```

## Run the Application

```bash
dotnet run --project src/Rentier.Desktop/Rentier.Desktop.csproj
```

## Run Tests

```bash
# All tests
dotnet test

# With coverage (requires coverlet)
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover

# Skip external integration tests (NBS API etc.)
dotnet test --filter "Category!=Integration"
```

## Implementation Guidelines

**Domain Layer** (`Rentier.Domain/`)
- Pure C# records, no external dependencies
- Enforce business rules in value object constructors
- Use `DomainException` for invalid state transitions

**Application Layer** (`Rentier.Application/`)
- Create `*Query`/`*Command` records in `Commands/` or `Queries/`
- Implement `*QueryHandler`/`*CommandHandler` in `Handlers/`
- Define repository interfaces in `Repositories/`

**Infrastructure Layer** (`Rentier.Infrastructure/`)
- Implement EF Core repositories
- Add migration: `dotnet ef migrations add MigrationName`
- Implement external service clients (NBS, IMAP, etc.)

**Desktop/UI** (`Rentier.Desktop/`)
- `*ViewModel` → calls Application use cases via `IMediator.Send()`
- All async: use `ReactiveCommand.CreateFromTask()`
- Bind to observables only — no event handlers in code-behind

## Monetary Values & Dates

Always use:
- `decimal` for all money amounts, tax rates, and exchange rates
- `DateOnly` (not `DateTime`) for all date-only values; convert at the infrastructure boundary

## Async/Await Standards

```csharp
// ✅ Good
public async Task<Result<Filing>> ProcessAsync(CancellationToken ct)
{
    var filing = await _repository.GetAsync(id, ct);
    return Result.Ok(filing);
}

// ❌ Bad — blocks the thread
public Task<Filing> Process() => Task.FromResult(_repository.Get(id).Result);
```

## Testing Conventions

Tests live in `tests/Rentier.*.Tests/`. Naming: `MethodName_StateUnderTest_ExpectedBehavior`.

**Domain tests** — pure logic, no mocks:
```csharp
[Fact]
public void AdvanceStatus_InitToFiled_Succeeds()
{
    var filing = Filing.CreateFromIncome(...);
    filing.AdvanceStatus(FilingStatus.Filed);
    filing.Status.Should().Be(FilingStatus.Filed);
}
```

**Application tests** — mock repositories with NSubstitute:
```csharp
[Fact]
public async Task ProcessReportsCommandHandler_WithValidReports_CreatesFilings()
{
    var mockRepo = Substitute.For<IReportRepository>();
    var handler = new ProcessReportsCommandHandler(mockRepo, ...);
    var result = await handler.Handle(new ProcessReportsCommand(...), CancellationToken.None);
    result.IsSuccess.Should().BeTrue();
}
```

**Integration tests** — real EF Core SQLite in-memory:
```csharp
[Fact, Trait("Category", "Integration")]
public async Task NbsExchangeRateFetcher_FetchesRates_CachesResults()
{
    // Uses real HttpClient with fake handler
}
```

## Commit Message Convention

```
feat: Add NBS exchange rate fetcher
fix: Correct holiday deadline calculation
refactor: Extract MailboxSyncService logic
test: Add edge cases for filing status transitions
docs: Update IBKR setup guide
chore: Update dependencies
```

## Contribution Guidelines

We welcome contributions! Before opening a pull request:

1. **Fork** this repository
2. **Create** a feature branch (`git checkout -b feature/descriptive-name`)
3. **Follow** the implementation guidelines above
4. **Write tests** — Domain, Application, and UI layers all require coverage
5. **Run tests locally** — `dotnet test`
6. **Commit** using conventional commit messages
7. **Push** and open a **Pull Request** with a clear description
8. **Fill in the "Release note" section** of the PR description if users will notice the change
   (see below)

### Release notes

The app shows what changed in each version (Settings → What's new, and **What's new** on the
update bar). Those notes come from the `## Release note` section of each merged PR's description,
which the PR template provides:

```
## Release note

### EN
- The update bar now spans the whole window.

### SR
- Traka za ažuriranje sada je preko celog prozora.
```

Keep the `### EN` / `### SR` headings: an `SR:` line written right after an English list shows
on GitHub as part of the last English bullet.

- Write for users: what they see or can do now, not how it was built. Serbian in Latin script,
  using the app's own words.
- **The PR title decides whether a note is required.** The **PR Release Note** check fails when:
  - a `feat`, `fix` or `perf` PR has no note. If users will not notice the change, give the title
    an internal scope instead: `build`, `ci`, `claude`, `deps`, `dev`, `docs`, `release` or
    `test(s)`, e.g. `fix(ci): flaky macOS job`.
  - a note has only one language.
- Every other type (`chore`, `ci`, `refactor`, `test`, `docs`, Dependabot's `chore: Bump ...`)
  needs no note; leave the lines empty.
- You can still fix a note after merging, until the next release is made: the release workflow
  reads the PR descriptions then.
- **A release needs at least one note.** If no PR merged since the last release has one, the
  Auto Release workflow stops before tagging. To release anyway (e.g. a dependency security fix),
  run it with **Release without user-facing changes** ticked; the app then shows "Stability and
  security improvements." for that version.
- The release tooling lives with the workflows: `.github/tools/Rentier.ReleaseNotes` builds the
  notes into a temporary file during the release; nothing is committed. Versions up to v1.4.0
  (`RELEASE_NOTES_SINCE` in `auto-release.yml`) predate release notes and are not shown. Only
  release builds carry notes; a local build's Settings → What's new is empty.

### Code Review Expectations

- Clean Architecture layer boundaries are preserved
- No passwords or secrets in source code
- All async methods accept and forward `CancellationToken`
- `decimal` for monetary values, `DateOnly` for dates
- No `.Result` or `.Wait()` blocking calls
- Tests follow xUnit + FluentAssertions naming conventions
