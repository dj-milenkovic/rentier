---
name: rentier-mocks
description: >
  NSubstitute mocking patterns for Rentier. Guides when to mock vs. keep real, how to configure
  repository stubs, how to verify calls with Received(), and why domain services use Func<>
  delegates instead of interface mocks. Use this skill when writing Application handler tests,
  wiring ViewModel handler factories, or setting up HTTP mocks in infrastructure tests. If you
  reach for Substitute.For<> inside a domain test, stop — read this skill first.
---

# Rentier Mocking & Setup Strategies

## The Cardinal Rule: Mock by Layer

| Layer | Mock strategy | Never mock |
|---|---|---|
| **Domain** | `Func<>` delegates for external data | `Substitute.For<IRepo>()` — ever |
| **Application** | `Substitute.For<IRepo/IService>()` | Domain entities, value objects |
| **Infrastructure** | `HttpMessageHandler` via `.Protected()` only | Real repositories, EF Core |
| **Desktop** | `Substitute.For<IHandler>()` via factory methods | ViewModels, converters |
| **Scenarios** | `FakeCredentialStore` + `IMailboxSyncService` only | All repositories |

If you are reaching for a mock in a domain test, stop. That class has an external
dependency it should not have — fix the design before writing the test.

---

## Domain: Func<> Delegates, Not Interface Mocks

Domain services accept `Func<>` delegates for external data lookups (e.g., exchange rates).
In tests, pass a lambda. This keeps the domain pure with zero framework dependencies.

```csharp
// ── Fixed rate ────────────────────────────────────────────────────────────
Func<DateOnly, string, Task<ExchangeRate>> fixedRate =
    (date, currency) => Task.FromResult(new ExchangeRate(date, currency, 117.21m));

// ── Capturing delegate (verify what currency was requested) ───────────────
string? capturedCurrency = null;
Func<DateOnly, string, Task<ExchangeRate>> capturingRate = (date, currency) => {
    capturedCurrency = currency;
    return Task.FromResult(new ExchangeRate(date, currency, 117.21m));
};

var result = await TaxCalculationService.CalculateAsync(
    IncomeType.Dividend, "AAPL", date,
    100m, "USD", 15m, "USD",
    capturingRate,
    TestContext.Current.CancellationToken);

capturedCurrency.Should().Be("USD");

// ── Throwing delegate (simulate a fetch failure) ──────────────────────────
Func<DateOnly, string, Task<ExchangeRate>> throwingRate =
    (date, currency) => throw new InvalidOperationException("NBS unavailable");
```

---

## Application: Substitute.For<IRepo>()

Initialise substitutes in the **constructor** — xUnit creates a fresh class instance per test,
so every test automatically gets clean substitutes with no shared state.

```csharp
public class CreateManualFilingCommandHandlerTests
{
    private readonly IFilingRepository          _filingRepo;
    private readonly IExchangeRateCacheRepository _rateRepo;
    private readonly CreateManualFilingCommandHandler _sut;

    public CreateManualFilingCommandHandlerTests()
    {
        _filingRepo = Substitute.For<IFilingRepository>();
        _rateRepo   = Substitute.For<IExchangeRateCacheRepository>();
        _sut = new CreateManualFilingCommandHandler(_filingRepo, _rateRepo);
    }
}
```

### Configuring Return Values

```csharp
// ── Return a value (NSubstitute wraps in Task automatically) ──────────────
_repo.GetByIdAsync(id, Arg.Any<CancellationToken>())
     .Returns(filing);

// ── Return null (not-found) ───────────────────────────────────────────────
_repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
     .Returns((Filing?)null);

// ── Return a Result failure ───────────────────────────────────────────────
_repo.GetAsync(id, Arg.Any<CancellationToken>())
     .Returns(Result<Filing, Error>.Failure(
         new Error("NOT_FOUND", "Filing not found")));

// ── Return different values on successive calls ───────────────────────────
_repo.GetByIdAsync(id, Arg.Any<CancellationToken>())
     .Returns(filingV1, filingV2);     // first call → v1, second → v2

// ── Throw (simulating a transient DB failure) ─────────────────────────────
_repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
     .Returns<Filing?>(_ => throw new DbUpdateException("connection refused"));
```

### Argument Matchers — Always Match CancellationToken with Arg.Any<>

```csharp
Arg.Any<Guid>()              // any Guid
Arg.Any<CancellationToken>() // ALWAYS use for CT — never the literal token
Arg.Is<Filing>(f => f.Status == FilingStatus.Init)   // predicate match
Arg.Is<Guid>(id => id == specificId)                  // value match
```

Tests pass `TestContext.Current.CancellationToken` into the SUT; matchers must use
`Arg.Any<CancellationToken>()` to match it correctly.

### Verifying Calls

```csharp
// ── Called exactly once ────────────────────────────────────────────────────
await _repo.Received(1).DeleteAsync(expectedId, Arg.Any<CancellationToken>());

// ── Called with specific argument predicate ───────────────────────────────
await _repo.Received(1).SaveAsync(
    Arg.Is<Filing>(f => f.Status == FilingStatus.Filed && f.Id == filing.Id),
    Arg.Any<CancellationToken>());

// ── Never called ──────────────────────────────────────────────────────────
await _repo.DidNotReceive().SaveAsync(Arg.Any<Filing>(), Arg.Any<CancellationToken>());

// ── Called at least N times ───────────────────────────────────────────────
await _repo.Received(atLeast: 2)
           .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
```

### When to Verify vs. When to Assert Result

- **`Received(1)`** — when a side-effect (save, delete, send) is the test's point
- **`result.IsSuccess`** — when the handler's return value is the test's point
- **Both** — when the handler must produce output AND trigger a side-effect

```csharp
[Fact]
public async Task HandleAsync_ValidCommand_SavesFilingAndReturnsId()
{
    _profileRepo.GetByIdAsync(profileId, Arg.Any<CancellationToken>()).Returns(profile);

    var result = await _sut.HandleAsync(command, TestContext.Current.CancellationToken);

    // Assert return value
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().Be(expectedId);

    // Assert side-effect
    await _filingRepo.Received(1).AddAsync(
        Arg.Is<Filing>(f => f.TaxpayerProfileId == profileId),
        Arg.Any<CancellationToken>());
}
```

---

## Infrastructure: Mock HttpMessageHandler Only

Real repositories use real SQLite. The only thing to mock in `Rentier.Infrastructure.Tests`
is `HttpMessageHandler` for NBS / external HTTP scrapers.

```csharp
using NSubstitute.Extensions;

var handler = Substitute.For<HttpMessageHandler>();
handler.Protected()
    .Setup<Task<HttpResponseMessage>>("SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
    .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(FakeNbsResponseHtml, Encoding.UTF8, "text/html")
    });

var client  = new HttpClient(handler) { BaseAddress = new Uri("https://nbs.rs") };
var fetcher = new NbsExchangeRateFetcher(client);
```

For error scenarios:
```csharp
// ── HTTP 503
.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))

// ── Network failure
.ThrowsAsync(new HttpRequestException("DNS resolution failed"))
```

---

## Desktop: Handler Factories for ViewModels

Never configure a substitute inline inside a test body. Extract `MockXxx()` and `CreateVm()`:

```csharp
// ── Success handler ──────────────────────────────────────────────────────
private static IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>
    MockGetFilings(FilingsPageResult? page = null)
{
    var h = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
    h.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
        .Returns(Result<FilingsPageResult, Error>.Success(
            page ?? new FilingsPageResult([], 0, 1)));
    return h;
}

// ── Failure handler ──────────────────────────────────────────────────────
private static IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>
    MockGetFilingsFailing(string code = "LOAD_ERROR", string msg = "Load failed")
{
    var h = Substitute.For<IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>>();
    h.HandleAsync(Arg.Any<GetFilingsQuery>(), Arg.Any<CancellationToken>())
        .Returns(Result<FilingsPageResult, Error>.Failure(new Error(code, msg)));
    return h;
}

// ── ViewModel factory ────────────────────────────────────────────────────
private static FilingsViewModel CreateVm(
    IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>>? q = null)
    => new(q ?? MockGetFilings(), ImmediateScheduler.Instance);
```

---

## Scenarios: Minimal Mocking

`ScenarioFixture` pre-registers a `FakeCredentialStore` and a `Substitute.For<IMailboxSyncService>()`.
All repositories are real. Do not add further mocks.

```csharp
// ✅ Configure specific scenario behaviour on the pre-registered substitute
var syncService = _fixture.GetService<IMailboxSyncService>();
syncService.SyncAsync(Arg.Any<Mailbox>(), Arg.Any<CancellationToken>())
    .Returns(new SyncResult(added: 3, skipped: 0, errors: []));

// ❌ WRONG — don't substitute a real repository in a scenario test
var fakeRepo = Substitute.For<IFilingRepository>();    // ❌ defeats the purpose
```

---

## Anti-Patterns

| Anti-pattern | Fix |
|---|---|
| `Substitute.For<IRepo>()` in a domain test | Domain is pure — use `Func<>` delegates |
| `CancellationToken.None` in argument matchers | `Arg.Any<CancellationToken>()` |
| `_repo.Returns(x)` without specifying method | Always: `_repo.Method(...).Returns(x)` |
| Forgetting `.Returns()` (NSubstitute returns `default`) | Configure all paths the SUT will hit |
| `Arg.Any<>()` when the exact value matters | `Arg.Is<T>(x => x.Id == expectedId)` |
| Mocking `HttpClient` directly | Mock `HttpMessageHandler.SendAsync` via `.Protected()` |
| Inline `Substitute.For<>()` in each `[Fact]` body | Extract to constructor or `static` factory |
| Adding more mocks in scenario tests | Use the two pre-registered mocks only |
