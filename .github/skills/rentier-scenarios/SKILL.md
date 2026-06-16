---
name: rentier-scenarios
description: >
  Design and write scenario tests that exercise Domain + Application + Infrastructure together
  using ScenarioFixture with real DI, real SQLite, and real repositories. Use this skill when
  a feature spans multiple layers and you need to verify the layers compose correctly, not just
  that each one works in isolation. Covers ScenarioFixture setup, test isolation, assertion
  strategy, and when to write a scenario vs. an integration or unit test.
---

# Rentier Cross-Layer Test Orchestration (Scenarios)

Scenario tests live in `Rentier.Scenarios.Tests`. They exercise complete user stories using
real DI, real repositories, and a real SQLite in-memory database. They are the highest-confidence
tests and the slowest — use them sparingly and purposefully.

---

## When to Write a Scenario Test

Write a scenario test when **all three** are true:
1. The feature involves at least two layers (domain state machine + handler + repository)
2. You need to verify the layers compose correctly — not just that each works in isolation
3. A unit or integration test alone cannot catch the bug (e.g., FK cascade, cross-handler
   state change, domain error surviving persistence round-trip)

**Good scenario candidates:**
- Filing lifecycle: create → file → pay (state machine + handler + EF round-trip)
- Profile creation + filing attachment (FK correctness)
- Sync workflow: mailbox sync → report parse → filing creation (multiple handlers in sequence)
- Invalid state transition returning an error without mutating the DB

**Poor scenario candidates (use a cheaper test instead):**
- Verifying a single handler's routing logic → Application unit test
- Verifying value converter precision → Infrastructure integration test
- Verifying domain invariants → Domain unit test

---

## Boundary: Scenario vs. Integration Test

| Question | If YES → Use |
|---|---|
| Requires multiple handlers firing in sequence? | Scenario |
| Requires domain state machine + DB persistence? | Scenario |
| Requires FK-correct entity graph? | Either (Scenario or Integration) |
| Involves only one repository method? | Integration |
| Involves only value converter round-trip? | Integration |
| Involves only handler routing with mocked repo? | Application unit test |

---

## ScenarioFixture — One Per Test Class

```csharp
using FluentAssertions;
using NSubstitute;
using Rentier.Application.Commands;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Xunit;

namespace Rentier.Scenarios.Tests;

[Trait("Category", "Scenario")]
public sealed class FilingLifecycleScenario : IDisposable
{
    private readonly ScenarioFixture _fixture;
    private readonly IFilingRepository _filingRepo;
    private readonly ITaxpayerProfileRepository _profileRepo;

    // One fixture per class — xUnit creates a fresh class instance per [Fact],
    // so each test gets its own isolated ScenarioFixture automatically.
    public FilingLifecycleScenario()
    {
        _fixture    = new ScenarioFixture();
        _filingRepo = _fixture.GetService<IFilingRepository>();
        _profileRepo = _fixture.GetService<ITaxpayerProfileRepository>();
    }

    public void Dispose() => _fixture.Dispose();
}
```

Rules:
- `[Trait("Category","Scenario")]` is mandatory on every class
- `sealed` — scenarios have no base class
- `IDisposable` (sync `Dispose`), not `IAsyncLifetime` — `ScenarioFixture.Dispose()` closes the SQLite connection
- One `ScenarioFixture` per test class, resolved via field. Never one per `[Fact]`.
- Never share mutable entity objects between tests in the same class.

---

## Test Structure: Build → Act → Assert Result + DB

Every scenario test has three phases. Assert **both** the handler `Result` and the re-queried
DB state — without the DB assertion you're only testing the return value, not persistence.

```csharp
[Fact]
public async Task FilingLifecycle_FromInitToFiled_StatusPersistedCorrectly()
{
    var ct = TestContext.Current.CancellationToken;

    // ── Phase 1: Build entity graph via domain factory methods ────────────
    var profile = new TaxpayerProfile(
        Guid.NewGuid(), "1234567890123", "Test User", "Belgrade", "018");
    await _profileRepo.SaveAsync(profile, ct);

    var filing = Filing.CreateFromIncome(
        profile.Id, IncomeType.Dividend, "AAPL",
        new DateOnly(2024, 6, 15),
        100_000m, 15_000m, 15_000m, 0m,
        new DateOnly(2024, 7, 15));
    await _filingRepo.AddAsync(filing, ct);

    // ── Phase 2: Execute the use case through the handler ─────────────────
    var handler = new UpdateFilingStatusCommandHandler(_filingRepo);
    var result = await handler.HandleAsync(
        new UpdateFilingStatusCommand(filing.Id, FilingStatus.Filed), ct);

    // ── Phase 3: Assert Result AND re-query DB state ──────────────────────
    result.IsSuccess.Should().BeTrue("Init → Filed is a valid transition");

    var persisted = await _filingRepo.GetByIdAsync(filing.Id, ct);
    persisted.Should().NotBeNull();
    persisted!.Status.Should().Be(FilingStatus.Filed,
        because: "status change must survive the EF Core persistence round-trip");
}
```

Use `var ct = TestContext.Current.CancellationToken;` when the token appears more than twice.

---

## Multi-Handler Scenarios (Sequential Operations)

Assert intermediate state after each handler call — do not chain blindly.

```csharp
[Fact]
public async Task FilingLifecycle_FullInitToFiledToPaid_AllTransitionsSucceed()
{
    var ct      = TestContext.Current.CancellationToken;
    var handler = new UpdateFilingStatusCommandHandler(_filingRepo);

    // Setup
    var profile = new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "Test User", "Belgrade", "018");
    await _profileRepo.SaveAsync(profile, ct);
    var filing = Filing.CreateFromIncome(
        profile.Id, IncomeType.Dividend, "MSFT",
        new DateOnly(2024, 3, 10), 50_000m, 7_500m, 7_500m, 0m,
        new DateOnly(2024, 4, 30));
    await _filingRepo.AddAsync(filing, ct);

    // Step 1: Init → Filed
    var step1 = await handler.HandleAsync(
        new UpdateFilingStatusCommand(filing.Id, FilingStatus.Filed), ct);
    step1.IsSuccess.Should().BeTrue("step 1 of lifecycle must succeed");

    // Step 2: Filed → Paid
    var step2 = await handler.HandleAsync(
        new UpdateFilingStatusCommand(filing.Id, FilingStatus.Paid), ct);
    step2.IsSuccess.Should().BeTrue("step 2 of lifecycle must succeed");

    // Final DB state
    var final = await _filingRepo.GetByIdAsync(filing.Id, ct);
    final!.Status.Should().Be(FilingStatus.Paid);
}
```

---

## Testing Invalid Transitions (Domain Errors via Handlers)

```csharp
[Fact]
public async Task FilingLifecycle_InvalidTransition_ReturnsErrorAndPreservesState()
{
    var ct = TestContext.Current.CancellationToken;

    var profile = new TaxpayerProfile(Guid.NewGuid(), "9876543210987", "Other User", "Nis", "050");
    await _profileRepo.SaveAsync(profile, ct);

    var filing = Filing.CreateFromIncome(
        profile.Id, IncomeType.Dividend, "Invalid Corp",
        new DateOnly(2024, 5, 20), 75_000m, 11_250m, 11_250m, 0m,
        new DateOnly(2024, 6, 30));
    await _filingRepo.AddAsync(filing, ct);

    var handler = new UpdateFilingStatusCommandHandler(_filingRepo);

    // Init → Paid is invalid (must go through Filed first)
    var result = await handler.HandleAsync(
        new UpdateFilingStatusCommand(filing.Id, FilingStatus.Paid), ct);

    // Assert failure
    result.IsFailure.Should().BeTrue("Init → Paid is not a valid transition");
    result.Error.Code.Should().Be("DOMAIN_ERROR");
    result.Error.Message.Should().Contain("Invalid Filing status transition");

    // Assert DB unchanged — failed transition must not mutate persisted state
    var unchanged = await _filingRepo.GetByIdAsync(filing.Id, ct);
    unchanged!.Status.Should().Be(FilingStatus.Init,
        because: "failed transition must not mutate persisted state");
}
```

---

## Available Services via ScenarioFixture

```csharp
// ── Real repositories (resolve as needed) ─────────────────────────────────
var filingRepo   = _fixture.GetService<IFilingRepository>();
var profileRepo  = _fixture.GetService<ITaxpayerProfileRepository>();
var reportRepo   = _fixture.GetService<IReportRepository>();
var importerRepo = _fixture.GetService<IImporterRepository>();
var rateRepo     = _fixture.GetService<IExchangeRateCacheRepository>();
var holidayRepo  = _fixture.GetService<IHolidayRepository>();
var mailboxRepo  = _fixture.GetService<IMailboxRepository>();

// ── Pre-registered mocks — configure behaviour as needed ──────────────────
var syncService = _fixture.GetService<IMailboxSyncService>();   // NSubstitute
var credStore   = _fixture.GetService<ICredentialStore>();       // FakeCredentialStore

// Configure the sync service for a specific scenario
syncService.SyncAsync(Arg.Any<Mailbox>(), Arg.Any<CancellationToken>())
    .Returns(new SyncResult(added: 5, skipped: 0, errors: []));
```

Only configure substitute behaviour you actually need for the test.
Never resolve `AppDbContext` directly — use repository interfaces.

---

## CI Trait Separation

```bash
dotnet test --filter "Category=Scenario"    # scenario tests only
dotnet test --filter "Category!=Scenario"   # everything except scenarios
```

Scenarios run in a dedicated CI stage after unit and integration tests pass.
They have a longer timeout budget — do not put fast assertions in a scenario.

---

## Anti-Patterns

| Anti-pattern | Fix |
|---|---|
| One `ScenarioFixture` created inside each `[Fact]` | One per class — xUnit re-instantiates the class |
| Sharing entity variables across `[Fact]` methods | Each test builds its own graph independently |
| Asserting only the handler `Result` | Always re-query DB and assert persisted state too |
| Using `SeedDataBuilder` for custom scenario data | Build entities inline via domain factory methods |
| More than 3–4 handler calls in one test | Split into separate scenario tests |
| Skipping invalid transition coverage | Cover both valid and invalid paths |
| Resolving `AppDbContext` from the fixture | Use repository interfaces only |
| Adding extra `Substitute.For<IRepo>()` to a scenario | All repositories in scenarios are real |
