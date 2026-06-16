---
name: rentier-test-data
description: >
  Guide creation and use of test data across all Rentier test layers. Covers three patterns:
  local MakeXxx() factories for unit tests, SeedDataBuilder for FK-correct entity graphs in
  integration and scenario tests, and FsCheck property-based generators for financial
  invariant testing. Use this skill when you need test data for any layer, or when deciding
  whether to inline values, extract a factory, or write a property test instead of [InlineData].
---

# Rentier Test Data Management

## Three Patterns, Three Contexts

| Pattern | Use in | When |
|---|---|---|
| Local `MakeXxx()` factory | Unit tests | One test class needs 1–3 entities with clean defaults |
| `SeedDataBuilder` | Integration & scenario tests | FK-correct multi-entity graphs, migration tests |
| FsCheck `[Property]` | Domain services, financial math | Invariants that must hold for **all** valid inputs |

Choose the simplest pattern that covers the test's needs.
Never use `SeedDataBuilder` in a unit test — it's for FK-correct graphs and adds unnecessary coupling.

---

## Pattern 1: Local MakeXxx() Factory

Define `private static` factory methods with sensible defaults and override-by-parameter for the
variations each test class needs. Keep them at the top of the test class.

```csharp
// ── Filing factory ───────────────────────────────────────────────────────
private static Filing MakeFiling(
    Guid?    profileId = null,
    string   entity    = "ACME Corp",
    DateOnly? date     = null,
    decimal  gross     = 10_000m,
    decimal  wht       = 1_500m)
{
    var d = date ?? new DateOnly(2024, 6, 15);
    return Filing.CreateFromIncome(
        profileId ?? Guid.NewGuid(),
        IncomeType.Dividend, entity, d,
        gross, wht,
        grossTaxPayableRsd: gross * 0.15m,
        taxPayableRsd: Math.Max(0m, gross * 0.15m - wht),
        filingDeadline: d.AddDays(30));
}

// ── TaxpayerProfile factory ───────────────────────────────────────────────
private static TaxpayerProfile MakeProfile(string jmbg = "1234567890123")
    => new(Guid.NewGuid(), jmbg, "Test User", "Test Address", "018");

// ── DTO factory (for ViewModel tests) ────────────────────────────────────
private static FilingRowDto MakeDto(
    FilingStatus status   = FilingStatus.Init,
    DateOnly?    deadline = null)
    => new(Guid.NewGuid(), status, IncomeType.Dividend, "ACME Corp",
           deadline ?? new DateOnly(2024, 4, 30), 100m, null);
```

**Rules for local factories:**
1. Default values must be **valid** domain values — they must not throw `DomainException`
2. All dates as `DateOnly` — never `DateTime`
3. All money as `decimal` with `m` suffix — never `double` or bare `int`
4. Use real domain factory methods (`Filing.CreateFromIncome`, `Importer.Create`) — never `new Filing(...)`
5. Name them `MakeXxx` — not `CreateXxx`, `BuildXxx`, or `GetXxx`

---

## Pattern 2: SeedDataBuilder (Integration & Scenario Tests)

`SeedDataBuilder` in `Rentier.Tests.Common` provides deterministic, FK-correct entity graphs
with amounts chosen to expose floating-point precision issues.

```csharp
// ── Seed a complete graph for a migration upgrade test ────────────────────
var primary   = SeedDataBuilder.PrimaryProfile();   // deterministic Guid
var secondary = SeedDataBuilder.SecondaryProfile();
var mailbox   = SeedDataBuilder.Mailbox();
var filings   = SeedDataBuilder.Filings(primary.Id, secondary.Id);
var rates     = SeedDataBuilder.ExchangeRates();    // 6-decimal-place values

ctx.TaxpayerProfiles.AddRange(primary, secondary);
ctx.Mailboxes.Add(mailbox);
ctx.Filings.AddRange(filings);
ctx.ExchangeRates.AddRange(rates);
await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

// ── Verify amounts survived the migration ─────────────────────────────────
var sortedAmounts = ctx.Filings
    .Select(f => f.GrossIncomeRsd)
    .OrderBy(a => a)
    .ToList();

sortedAmounts.Should().BeEquivalentTo(
    SeedDataBuilder.KnownGrossAmountsSorted(),
    o => o.WithStrictOrdering());
```

**When to use `SeedDataBuilder` vs. a local factory:**

| Need | Use |
|---|---|
| FK relationships (Filing → TaxpayerProfile) | `SeedDataBuilder` |
| Deterministic IDs shared across test files | `SeedDataBuilder.PrimaryProfileId` |
| Precision-probing amounts (6 d.p. rates) | `SeedDataBuilder.ExchangeRates()` |
| All status values covered in one graph | `SeedDataBuilder.Filings(...)` |
| A single entity in isolation | Local `MakeXxx()` |
| ViewModel handler stub data | Local `MakeDto()` |

---

## Pattern 3: FsCheck Property Tests (Financial Invariants)

Use FsCheck when a property must hold for **all valid inputs** — not just the edge cases
you thought of. Place property tests in `Domain/Properties/` within `Rentier.UnitTests`.

```csharp
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Rentier.Domain.Enums;
using Rentier.Domain.Services;
using Rentier.Domain.ValueObjects;

public class TaxCalculationProperties
{
    private static Func<DateOnly, string, Task<ExchangeRate>> FixedRate(decimal r)
        => (d, c) => Task.FromResult(new ExchangeRate(d, c, r));

    // ── Invariant: TaxPayable ≥ 0 for any positive income and WHT ───────────
    [Property]
    public Property TaxPayable_IsNeverNegative(PositiveInt incomeInt, PositiveInt whtInt)
    {
        var income = incomeInt.Get / 100m;   // PositiveInt → positive decimal (2 d.p.)
        var wht    = whtInt.Get / 100m;

        var result = TaxCalculationService.CalculateAsync(
            IncomeType.Dividend, "TEST", new DateOnly(2024, 1, 1),
            income, "USD", wht, "USD",
            FixedRate(117.21m)).GetAwaiter().GetResult();

        return (result.TaxPayableRsd >= 0m).ToProperty();
    }

    // ── Invariant: TaxPayable ≤ GrossTax ─────────────────────────────────────
    [Property]
    public Property TaxPayable_NeverExceedsGrossTax(PositiveInt incomeInt, PositiveInt whtInt)
    {
        var income = incomeInt.Get / 100m;
        var wht    = whtInt.Get / 100m;

        var result = TaxCalculationService.CalculateAsync(
            IncomeType.Dividend, "TEST", new DateOnly(2024, 1, 1),
            income, "USD", wht, "USD",
            FixedRate(117.21m)).GetAwaiter().GetResult();

        return (result.TaxPayableRsd <= result.GrossTaxPayableRsd).ToProperty();
    }
}
```

**FsCheck generator patterns for Rentier:**

```csharp
// ✅ PositiveInt → positive decimal (2 d.p.)
var amount = positiveInt.Get / 100m;          // e.g. 4231 → 42.31m

// ✅ Bounded exchange rate (0.01 to ~100)
var rate = (rateInt.Get % 10_000) / 100m + 0.01m;

// ✅ Fixed-set generator (e.g. extreme rates)
Gen.Elements(new[] { 0.0001m, 1.0m, 117.21m, 99_999.99m }).ToArbitrary()

// ✅ Bounded collection — always cap size to avoid combinatorial explosion
var amounts = rawAmounts.Take(50).Select(n => n.Get / 100m).ToList();

// ✅ Enum coverage via Gen.Elements
Gen.Elements(ValidPairs).ToArbitrary()        // see FilingStatusTransitionProperties
```

**`[Property]` vs. `[Theory][InlineData]`:**

| Use | When |
|---|---|
| `[InlineData]` | Boundary values you know: `null`, `""`, `0m`, `-0.01m` |
| `[Property]` | Invariants that must hold for **all** valid inputs |
| `[Property]` | Financial capping / rounding rules |
| `[Property]` | State-machine exhaustive coverage (all N×N transition pairs) |

---

## Embedded Fixture Files (Parser Tests)

CSV and HTML fixtures live in `Parsers/Fixtures/` as embedded resources:

```xml
<!-- In Rentier.Infrastructure.Tests.csproj -->
<ItemGroup>
  <EmbeddedResource Include="Parsers\Fixtures\*.csv" />
  <EmbeddedResource Include="Parsers\Fixtures\*.html" />
</ItemGroup>
```

Loading in tests:
```csharp
private static Stream GetFixture(string fileName)
{
    var assembly = typeof(IbkrCsvParserTests).Assembly;
    var resourceName = $"Rentier.Infrastructure.Tests.Parsers.Fixtures.{fileName}";
    return assembly.GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException($"Fixture not found: {resourceName}");
}

[Fact]
public void Parse_ValidDividendsCsv_ReturnsTwoRows()
{
    using var stream = GetFixture("sample_dividends.csv");
    var result = new IbkrCsvParser().Parse(stream);

    result.IsSuccess.Should().BeTrue();
    result.Value.Should().HaveCount(2);
    result.Value[0].Amount.Should().Be(100.00m);
}
```

Never reference fixture files by filesystem path — paths are environment-dependent.

---

## Anti-Patterns

| Anti-pattern | Fix |
|---|---|
| `new DateTime(...)` in test data | `new DateOnly(y, m, d)` |
| Bare `int` or `double` for money | `m` suffix: `1_500m` not `(decimal)1500` |
| `new Filing(...)` bypassing factory | `Filing.CreateFromIncome(...)` |
| Sharing a mutable entity across tests | Each test calls `MakeFiling()` independently |
| `SeedDataBuilder` in a unit test | Local `MakeXxx()` — `SeedDataBuilder` is for FK graphs |
| `Guid.NewGuid()` in `[InlineData]` | Use `[MemberData]` with pre-built values |
| `double` literals in `[InlineData]` for money | `m` suffix: `[InlineData(100.00m, 15.00m)]` |
| FsCheck `.GetAwaiter().GetResult()` on hot tasks | Always construct the delegate fresh inside the property |
