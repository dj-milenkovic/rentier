# Rentier Testing Guide

Financial correctness is the highest priority. A rounding bug is a legal problem, not a
UI glitch. This guide describes the five test types the project uses, their frameworks,
rules, trait/CI contract, and the "when you add X, you add Y" checklist.

---

## Test types and frameworks

| Type | Project | Framework | Trait | CI job |
|------|---------|-----------|-------|--------|
| Unit — Domain | `Rentier.UnitTests` | xUnit v3 + FluentAssertions + FsCheck | _(none)_ | `build` matrix |
| Unit — Application | `Rentier.UnitTests` | xUnit v3 + FluentAssertions + NSubstitute | _(none)_ | `build` matrix |
| UI — ViewModel | `Rentier.UnitTests` | xUnit v3 + FluentAssertions + `ImmediateScheduler` | _(none)_ | `build` matrix |
| UI — Headless view | `Rentier.UnitTests` | Avalonia.Headless.XUnit (`[AvaloniaFact]`) | `Category=UI` | `build` matrix |
| Integration — adapters | `Rentier.Infrastructure.Tests` | xUnit v3 + SQLite `:memory:` + Verify | `Category=Integration` | `integration-tests` |
| E2E — scenarios | `Rentier.Scenarios.Tests` | xUnit v3 + production DI + fakes | `Category=Integration` | `integration-tests` |
| Mutation | _(Stryker.NET)_ | dotnet-stryker | _(scheduled)_ | `stryker` (weekly) |

**Speed contract:** no-trait tests < 5 s total, zero I/O, zero threads blocked.

---

## 2.1 Unit tests — Domain & Application

**Domain rules (non-negotiable):**
- No mocking framework — ever. External data enters as values or `Func<>` delegates.
- Exact `decimal` assertions: `.Should().Be(1234.56m)` — never tolerance-based, never double.
- Every financial rule gets *both* an example test (`[Theory]` with hand-computed values,
  ideally cross-checked against Poreska uprava worked examples) *and* a property test for the invariant.
- Filing status machines: enumerate the **full transition matrix** — every valid and every
  invalid pair, not just the happy chain.

```csharp
// Example-based: values hand-computed from a worked Poreska uprava example
[Theory]
[InlineData(100.00, 117.5432, 15.00, 1763.15)]
public void ComputeTax_KnownCase_MatchesHandCalculation(
    decimal grossUsd, decimal nbsRate, decimal whtPct, decimal expectedRsdTax) { … }

// Property-based: the invariant no example test can prove
[Property]
public Property WithholdingCredit_NeverExceedsSerbianTax(PositiveDecimal gross, Rate rate)
    => (TaxCalc.Credit(gross, rate) <= TaxCalc.SerbianTax(gross, rate)).ToProperty();
```

**Application handler rules:**
- One test class per handler.
- Mandatory case set: happy / not-found / validation-failure / `Received(1)` interaction / error-propagation.
- Assert **both** `Result` branches.
- Every new handler registered in `DiRegistrationSmokeTests`.

---

## 2.2 Integration tests — Infrastructure adapters

**Rules:**
- Real SQLite `:memory:` (kept-open connection) — never EF InMemory (it lies about FK/unique/converter semantics).
- New migration → new schema smoke test + upgrade-from-baseline test.
- Repository minimum: found / not-found / constraint violation / `DateOnly` + `decimal` round-trip.
- **No real network.** NBS fetcher tested against canned HTML/XML via fake `HttpMessageHandler`.
  Keep one canned fixture per known NBS format change as a regression corpus.
- Parser bugs: add the offending IBKR CSV as an embedded-resource fixture before fixing.
- Serializer: structural asserts for logic, Verify snapshots for full-document regression.

```csharp
public sealed class FilingRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");

    public async ValueTask InitializeAsync()
    {
        await _conn.OpenAsync();
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync() => await _conn.DisposeAsync();

    private AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
}
```

---

## 2.3 UI tests — ViewModels + headless views

**ViewModel tests (no Avalonia needed):**
- Inject `ImmediateScheduler.Instance` — a Reactive test with the real scheduler is a false green.
- Activate via `using var _ = vm.Activator.Activate()`.
- Per ViewModel: initial state / loaded state / failure state / command `CanExecute` gating /
  navigation via captured delegate.

**Headless view tests (`[AvaloniaFact]`):**
- Smoke only: construct the real View + real ViewModel, verify it renders and key bindings resolve.
- Do not script click-flows here — flow logic belongs in ViewModel tests.

---

## 2.4 E2E — scenario tests

`Rentier.Scenarios.Tests` is the real E2E suite. It builds the service provider from the
**production `CompositionRoot`/`InfrastructureRegistrar`**, overriding only true externals:

| External | Test override |
|----------|--------------|
| HTTP handler | canned NBS responses |
| Credential store | `FakeCredentialStore` |
| SQLite file | temp file (deleted after test) |
| Mail | fake / no-op |

**Required scenarios:**
1. Golden path: real IBKR CSV → import → rate fetch (canned) → tax computed → PP-OPO XML exported.
   Assert Verify snapshot of XML and exact `decimal` amounts on every tax field.
2. Deadline on a Serbian holiday → shifted correctly end-to-end.
3. Foreign WHT > 15% → credit capped in the exported XML.
4. Same CSV imported twice → idempotent (no duplicate filings).
5. Filing lifecycle Init→Filed→Paid through handlers; invalid transition rejected.
6. App-boot smoke: `DatabaseInitializer` against a temp file — fresh install and
   upgrade-from-previous-schema both complete without error.

Plus **one** `[AvaloniaFact]` full-app smoke test: app starts headless, main window renders,
navigation to each page doesn't throw. That's the entire pixels-level E2E budget.

---

## 2.5 Mutation testing (Stryker.NET)

Run **weekly + on demand** (not per-PR — too slow for the inner loop).

```bash
# Install once:
dotnet tool install -g dotnet-stryker

# Run against Domain:
dotnet stryker --config-file stryker-config.json
```

Config lives in `stryker-config.json` at the repo root. Break threshold: **85**.
HTML report uploaded as a CI artifact from the weekly `stryker.yml` workflow.

---

## CI pipeline

```
lint (format + vulnerable-pkg gate)
  ├─ build   matrix: win/mac/linux   --filter "Category!=Integration"
  ├─ integration  ubuntu             --filter "Category=Integration"
  │               Infrastructure.Tests + Scenarios.Tests
  │               (migration tests fold in — no separate migration job)
  └─ (both upload coverage-* artifacts)
coverage merge (ReportGenerator)   includes integration coverage + ratchet step
sonar + quality gate
weekly: stryker.yml                 mutation testing on Domain, break<85
```

**Trait contract — a test in the wrong bucket is a CI bug:**

| Trait | Meaning | CI job |
|-------|---------|--------|
| _(no trait)_ | Fast unit / ViewModel test | `build` matrix |
| `[Trait("Category", "Integration")]` | Needs SQLite / HTTP / file I/O | `integration-tests` |
| `[Trait("Category", "UI")]` | Needs Avalonia headless app context | `build` matrix |

---

## When you add X, you add Y

| When you add… | Also add… |
|---------------|-----------|
| Domain entity or value object | Example `[Theory]` + `[Property]` tests |
| Filing status transition | Full transition matrix (all valid + all invalid pairs) |
| Application handler | Handler test class + `DiRegistrationSmokeTests` entry |
| ViewModel or View | ViewModel tests + headless smoke |
| Repository | Integration tests (found / not-found / constraint / round-trips) |
| Migration | Schema smoke test + upgrade-from-baseline test |
| Parser bug fix | Offending IBKR CSV as embedded-resource fixture |
| Money-path change | Update the golden-path scenario + Verify snapshot |
| New NBS format | Canned fixture in the HTTP mock corpus |

---

## Naming and structure

- `MethodName_StateUnderTest_ExpectedBehavior`
- Arrange-Act-Assert; one behavior per test.
- Tests must be runnable in any order, in parallel (except `[AvaloniaFact]` which requires serialized context).
- Bug fix → failing regression test first, then fix.
