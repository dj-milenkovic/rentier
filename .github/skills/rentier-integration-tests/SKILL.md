---
name: rentier-integration-tests
description: >
  Write integration tests for Rentier.Infrastructure — EF Core repositories, CSV/XML parsers,
  NBS web scrapers, serializers, and external service adapters. Use this skill whenever a
  repository (FilingRepository, ReportRepository, ExchangeRateCacheRepository, etc.), parser
  (IbkrCsvParser), serializer (PpOpoXmlSerializer), or HTTP adapter is added or changed.
  Also use it when fixing a persistence bug, when asked "how do I test this repository?", or
  when verifying EF Core queries, value converters, or migrations work correctly end-to-end
  with a real database. If infrastructure code changes without tests, suggest this skill.
---

# Rentier Integration Tests

Integration tests verify that the code talking to SQLite, CSV files, XML, and external HTTP
services works correctly with real I/O rather than in-memory mocks. They catch FK violations,
value converter bugs, and query logic that EF Core's in-memory provider silently ignores.

## Test Project

All infrastructure integration tests live in `Rentier.Infrastructure.Tests`. Key packages:
- `Microsoft.EntityFrameworkCore.Sqlite` — real SQLite in-memory (the default for all DB tests)
- `Microsoft.EntityFrameworkCore.InMemory` — available but rarely needed (use SQLite instead)
- `NSubstitute` — for mocking `HttpMessageHandler` and external services only

## Category Trait — Mandatory

Every test class in this project must be tagged so CI can separate it from fast unit tests:

```csharp
[Trait("Category", "Integration")]
public class FilingRepositoryTests { ... }
```

CI runs these with: `dotnet test --filter "Category=Integration"`

---

## Database Setup — The Golden Pattern

Each test gets its **own isolated** `AppDbContext` backed by a fresh in-memory SQLite database.
Never share DB state between tests.

```csharp
private static AppDbContext CreateContext()
{
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite("Data Source=:memory:")   // ← NOT UseInMemoryDatabase
        .Options;
    var ctx = new AppDbContext(options);
    ctx.Database.EnsureCreated();            // applies schema; no migrations needed
    return ctx;
}
```

Use `await using var ctx = CreateContext();` (or `using var` for sync tests) to ensure
disposal. Each call creates a fully independent database.

### Why SQLite, Not EF InMemory?

| Feature | SQLite `:memory:` | EF InMemory |
|---|---|---|
| FK constraints | ✅ enforced | ❌ skipped |
| Unique indexes | ✅ enforced | ❌ skipped |
| Value converters (`DateOnly`↔`TEXT`, `decimal`↔`TEXT`) | ✅ runs | ❌ bypassed |
| LINQ-to-SQL translation bugs | ✅ caught | ❌ hidden |

Use EF InMemory only when testing pure change-tracking logic (no SQL semantics needed).

### In-Memory Connection Keep-Alive (Alternative Pattern)

When a test class has many tests and benefits from `IAsyncLifetime`, keep the connection
open to prevent the database from being garbage-collected between test methods:

```csharp
public class FilingRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _conn = null!;
    private AppDbContext _ctx     = null!;
    private FilingRepository _repo = null!;

    public async Task InitializeAsync()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conn).Options;
        _ctx  = new AppDbContext(options);
        await _ctx.Database.EnsureCreatedAsync();
        _repo = new FilingRepository(_ctx);
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
        await _conn.DisposeAsync();
    }
}
```

Both patterns (per-test `CreateContext()` or class-level `IAsyncLifetime`) are acceptable.
Prefer `CreateContext()` per test for simplicity. Use `IAsyncLifetime` when setup cost matters.

---

## Repository Test Coverage

For every public repository method, write three categories:

```csharp
// 1. Found / success case
[Fact]
public async Task GetByIdAsync_ExistingFiling_ReturnsFiling()
{
    await using var ctx = CreateContext();
    var repo = new FilingRepository(ctx);
    ctx.Filings.Add(FilingFactory.Create());
    await ctx.SaveChangesAsync();

    var result = await repo.GetByIdAsync(filing.Id, CancellationToken.None);

    result.Should().NotBeNull();
    result!.Id.Should().Be(filing.Id);
}

// 2. Not-found / empty case
[Fact]
public async Task GetByIdAsync_NonExistentId_ReturnsNull()
{
    await using var ctx = CreateContext();
    var repo = new FilingRepository(ctx);

    var result = await repo.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

    result.Should().BeNull();
}

// 3. Constraint / duplicate case (where applicable)
[Fact]
public async Task AddAsync_DuplicateTicker_ThrowsOrReturnsFailure() { ... }
```

---

## DateOnly and Decimal Round-Trip

Always assert that values survive the EF Core value converter (stored as TEXT in SQLite):

```csharp
// Seed
ctx.Filings.Add(new Filing { PaymentDate = new DateOnly(2024, 3, 15), TaxPayableRsd = 1758.15m });
await ctx.SaveChangesAsync();

// Re-query from DB
var loaded = await repo.GetByIdAsync(id, CancellationToken.None);

// Assert exact values preserved
loaded!.PaymentDate.Should().Be(new DateOnly(2024, 3, 15));   // DateOnly, not DateTime
loaded.TaxPayableRsd.Should().Be(1758.15m);                    // exact decimal, not approx
```

---

## Parser Tests — Embedded Fixtures

Load test input from embedded resources, not from filesystem paths. Fixture files live in
`Parsers/Fixtures/` and are marked `<EmbeddedResource>` in the `.csproj`:

```csharp
[Fact]
public void Parse_ValidIbkrCsv_ReturnsTwoIncomeRows()
{
    var assembly = typeof(IbkrCsvParserTests).Assembly;
    using var stream = assembly.GetManifestResourceStream(
        "Rentier.Infrastructure.Tests.Parsers.Fixtures.sample_dividends.csv")!;

    var result = new IbkrCsvParser().Parse(stream);

    result.IsSuccess.Should().BeTrue();
    result.Value.Should().HaveCount(2);
    result.Value[0].PayingEntity.Should().Be("AAPL");
    result.Value[0].Amount.Should().Be(100.00m);
}

[Fact]
public void Parse_MalformedCsv_ReturnsFailure()
{
    var result = new IbkrCsvParser().Parse(MalformedStream());

    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().StartWith("PARSE_");
}
```

---

## Serializer Tests

For `PpOpoXmlSerializer`, verify structure and key tax amounts:

```csharp
[Fact]
public void Serialize_ValidFiling_ContainsRequiredElements()
{
    var xml = PpOpoXmlSerializer.Serialize(FilingFactory.Create());
    var doc = XDocument.Parse(xml);

    doc.Root!.Name.LocalName.Should().Be("PpOpo");
    doc.Descendants("Prihod").Should().HaveCount(1);
    doc.Descendants("PorezNaTeret").First().Value.Should().Be("1758.15");
}
```

For full snapshot testing, add `Verify.Xunit` (see `docs/testing-strategy.md` §10).

---

## External HTTP Services — No Real Network

Mock `HttpMessageHandler`, not `HttpClient` directly:

```csharp
var handler = Substitute.For<HttpMessageHandler>();
handler.Protected()
    .Setup<Task<HttpResponseMessage>>("SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
    .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(FakeNbsResponseHtml)
    });

var fetcher = new NbsExchangeRateFetcher(new HttpClient(handler));
```

Or use a `TestHttpMessageHandler` from `Rentier.Tests.Common` if one exists.
**Never** hit real HTTP endpoints in automated tests.

---

## Credential Store Tests

Use `FakeCredentialStore` from `Rentier.Tests.Common`:

```csharp
var store = new FakeCredentialStore();
store.Save("key", "secret");

store.Load("key").Should().Be("secret");
store.Load("missing").Should().BeNull();
```

---

## Schema Smoke Test

Add or update this whenever a new entity or migration lands:

```csharp
[Fact]
public void AppDbContext_NewSchema_DoesNotThrow()
{
    var act = () => { using var ctx = CreateContext(); };
    act.Should().NotThrow();
}
```

---

## Naming Convention

```
MethodName_StateUnderTest_ExpectedBehavior
```

Examples:
- `GetByIdAsync_ExistingFiling_ReturnsFiling`
- `SaveAsync_DecimalRateToRsd_PreservesExactValue`
- `Parse_MissingDividendsSection_ReturnsParseError`
- `GetRangeAsync_NoRatesForCurrency_ReturnsEmpty`

---

## Anti-Patterns to Avoid

| Anti-pattern | Fix |
|---|---|
| `UseInMemoryDatabase` for repository tests | `UseSqlite("Data Source=:memory:")` |
| Shared DB context between tests | Fresh `CreateContext()` per test (or `IAsyncLifetime`) |
| `DateTime` in date assertions | `new DateOnly(y, m, d)` |
| Missing `[Trait("Category", "Integration")]` | Add it to every class |
| Real HTTP in CI | Mock `HttpMessageHandler` |
| Filesystem paths for fixture files | Embedded resources only |
