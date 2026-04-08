---
description: "Task list for Feature 006: NBS Exchange Rate Fetcher"
---

# Tasks: NBS Exchange Rate Fetcher (Feature 006)

**Input**: Design documents from `.specify/specs/006-nbs-exchange-rate-fetcher/`  
**Branch**: `feature/006-nbs-exchange-rate-fetcher`  
**Prerequisites**: plan.md ✅ data-model.md ✅ contracts/ ✅ research.md ✅ quickstart.md ✅

**Tests**: Tests are included for Infrastructure layer (repository unit + fetcher unit + integration)
in line with constitution quality gates. Integration tests are tagged `[Trait("Category","Integration")]`
and excluded from CI with `--filter "Category!=Integration"`.

**Organization**: This feature has no CQRS handlers or Desktop surface — it is a pure
Infrastructure/Application service. Tasks are ordered by dependency: Application interface first,
then Infrastructure EF config + DbContext (parallel), then EF migration, then repository
implementation, then fetcher implementation, then tests, then DI wiring.

## Format: `[ID] [P?] [Badge] Description`

- **[P]**: Can run in parallel (different files, no blocking dependency on an incomplete task)
- **Badge**: Layer/role label — `[APPLICATION]`, `[INFRASTRUCTURE]`, `[TEST]`, `[DI]`
- All tasks include exact file paths and complete implementation details

---

## Pre-Flight: Critical Existing State

> ⚠️ Before starting, confirm the following files **already exist** and must **NOT** be recreated:
>
> - `src/Rentier.Domain/ValueObjects/ExchangeRate.cs` — `record` with `init` setters; `DateOnly Date`, `string Currency`, `decimal RateToRsd`; constructor throws `DomainException` if `rateToRsd <= 0`. **DO NOT MODIFY.**
> - `src/Rentier.Application/Repositories/IExchangeRateCacheRepository.cs` — interface with `GetAsync`, `GetByDateRangeAsync`, `SaveAsync`, `SaveBatchAsync`. **DO NOT MODIFY.**
> - `src/Rentier.Application/Common/Result.cs` — `Result<TValue, TError>` with `.Success(value)` and `.Failure(error)` static factory methods. **DO NOT USE `.Ok()` — it does not exist.**
> - `src/Rentier.Infrastructure/Persistence/AppDbContext.cs` — 5 existing `DbSet<T>` properties. **MODIFY in T003 only.**
> - `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs` — 6 existing DI registrations. **MODIFY in T009 only.**
> - Migrations `0001`–`0005` exist under `src/Rentier.Infrastructure/Persistence/Migrations/`. The next migration is **`0006_ExchangeRateCache`**.

---

## Phase 1: Application Interface (T001)

**Purpose**: Define the `IExchangeRateFetcher` contract in the Application layer. This is the
only Application-layer change in this feature. It has no dependencies on any other task.

> **⚠️ CRITICAL**: T002–T006 all reference types from this interface or its co-located namespace.
> Confirm this file compiles cleanly before proceeding.

- [X] T001 [APPLICATION] CREATE `src/Rentier.Application/Interfaces/IExchangeRateFetcher.cs`:
  Namespace: `Rentier.Application.Interfaces`.
  Usings: `using Rentier.Application.Common;` `using Rentier.Domain.ValueObjects;`
  Full interface body:
  ```csharp
  /// <summary>
  /// Fetches the NBS official middle exchange rate for a currency on a given date.
  /// Checks the local SQLite cache first; falls back to the NBS XML web service on miss.
  /// </summary>
  public interface IExchangeRateFetcher
  {
      Task<Result<ExchangeRate, Error>> FetchRateAsync(
          DateOnly date, string currency, CancellationToken ct = default);
  }
  ```
  Error codes returned by implementations:
  `UNSUPPORTED_CURRENCY` — currency not in the 15-currency NBS set;
  `RATE_NOT_FOUND` — NBS published no rate for the date (weekend/holiday) or currency absent;
  `NBS_HTTP_ERROR` — non-2xx HTTP response from NBS;
  `NBS_PARSE_ERROR` — XML response could not be parsed.

**Checkpoint**: `dotnet build src/Rentier.Application` produces zero errors.

---

## Phase 2: Infrastructure EF Config + AppDbContext (T002–T003)

**Purpose**: Register `ExchangeRate` as an EF Core entity with the correct SQLite table name,
composite primary key, and column constraints. T002 and T003 touch different files and can
proceed in parallel; both must complete before T004 (EF migration) can run.

> **⚠️ CRITICAL**: T004 (EF migration) cannot run until T002 AND T003 are both complete and
> `dotnet build` succeeds on `Rentier.Infrastructure`.

- [X] T002 [P] [INFRASTRUCTURE] CREATE `src/Rentier.Infrastructure/Persistence/Configurations/ExchangeRateCacheConfiguration.cs`:
  Namespace: `Rentier.Infrastructure.Persistence.Configurations`.
  Usings: `using Microsoft.EntityFrameworkCore;` `using Microsoft.EntityFrameworkCore.Metadata.Builders;` `using Rentier.Domain.ValueObjects;`
  Full class body:
  ```csharp
  public sealed class ExchangeRateCacheConfiguration : IEntityTypeConfiguration<ExchangeRate>
  {
      public void Configure(EntityTypeBuilder<ExchangeRate> builder)
      {
          builder.ToTable("ExchangeRateCache");
          builder.HasKey(e => new { e.Date, e.Currency });

          builder.Property(e => e.Date)
              .IsRequired();

          builder.Property(e => e.Currency)
              .HasMaxLength(10)
              .IsRequired();

          builder.Property(e => e.RateToRsd)
              .HasPrecision(18, 6)
              .IsRequired();
      }
  }
  ```
  `ApplyConfigurationsFromAssembly` in `AppDbContext.OnModelCreating` will pick this up
  automatically — no explicit call required.
  Note: EF Core's SQLite provider stores `decimal` columns with `HasPrecision` as TEXT (ISO
  formatted), preserving full precision — this is correct and expected for financial values.

- [X] T003 [P] [INFRASTRUCTURE] MODIFY `src/Rentier.Infrastructure/Persistence/AppDbContext.cs`:
  Add using at top of file: `using Rentier.Domain.ValueObjects;`
  Add one new `DbSet` property after the existing five, maintaining alphabetical alignment:
  ```csharp
  public DbSet<ExchangeRate> ExchangeRateCache => Set<ExchangeRate>();
  ```
  The final property list should be:
  `TaxpayerProfiles`, `PublicHolidays`, `HolidayYearRange`, `Mailboxes`, `Importers`, `ExchangeRateCache`.
  Do NOT alter `OnModelCreating` — `ApplyConfigurationsFromAssembly` handles the new config.

**Checkpoint**: `dotnet build src/Rentier.Infrastructure` produces zero errors before migration.

---

## Phase 3: EF Migration (T004)

**Purpose**: Generate the EF Core migration that creates the `ExchangeRateCache` SQLite table.
Depends on T002 (EF config) and T003 (DbSet) both being complete and building cleanly.

- [X] T004 [INFRASTRUCTURE] RUN EF migration (depends on T002 + T003):
  From the repository root, run:
  ```shell
  dotnet ef migrations add 0006_ExchangeRateCache --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop
  ```
  Verify that the migration file was created at:
  `src/Rentier.Infrastructure/Persistence/Migrations/` with a filename containing `0006_ExchangeRateCache`.
  The generated `Up` method should contain a `CreateTable` call for `"ExchangeRateCache"` with
  columns `Date` (TEXT), `Currency` (TEXT, maxLength 10), `RateToRsd` (TEXT, precision 18/6),
  and primary key constraint `PK_ExchangeRateCache` over `(Date, Currency)`.
  The `Down` method should contain `DropTable(name: "ExchangeRateCache")`.
  After generation, confirm `dotnet build src/Rentier.Infrastructure` still produces zero errors.

**Checkpoint**: Migration file exists; `dotnet build` succeeds; `dotnet ef database update` can
be run against a local SQLite file to confirm schema creation.

---

## Phase 4: Infrastructure Repository (T005)

**Purpose**: Implement `IExchangeRateCacheRepository` backed by EF Core + SQLite.
Depends on T004 (migration must exist so that `ExchangeRateCache` DbSet is fully wired).

- [X] T005 [INFRASTRUCTURE] CREATE `src/Rentier.Infrastructure/Repositories/ExchangeRateCacheRepository.cs`:
  Namespace: `Rentier.Infrastructure.Repositories`.
  Usings: `using Microsoft.EntityFrameworkCore;` `using Rentier.Application.Repositories;` `using Rentier.Domain.ValueObjects;` `using Rentier.Infrastructure.Persistence;`
  Class declaration: `public sealed class ExchangeRateCacheRepository : IExchangeRateCacheRepository`
  Constructor: `public ExchangeRateCacheRepository(AppDbContext db) { _db = db; }`
  Private field: `private readonly AppDbContext _db;`

  **`GetAsync`** implementation:
  ```csharp
  public async Task<ExchangeRate?> GetAsync(DateOnly date, string currency, CancellationToken ct = default)
      => await _db.ExchangeRateCache
          .FindAsync(new object[] { date, currency.ToUpperInvariant() }, ct);
  ```
  Note: `FindAsync` key order must match `HasKey(e => new { e.Date, e.Currency })` — pass `Date`
  first, then `Currency`. `currency` is normalised to `ToUpperInvariant()` before lookup.

  **`GetByDateRangeAsync`** implementation:
  ```csharp
  public async Task<IReadOnlyList<ExchangeRate>> GetByDateRangeAsync(
      DateOnly from, DateOnly to, string currency, CancellationToken ct = default)
  {
      var upper = currency.ToUpperInvariant();
      return await _db.ExchangeRateCache.AsNoTracking()
          .Where(e => e.Currency == upper && e.Date >= from && e.Date <= to)
          .OrderBy(e => e.Date)
          .ToListAsync(ct);
  }
  ```

  **`SaveAsync`** implementation (single-row upsert):
  ```csharp
  public async Task SaveAsync(ExchangeRate rate, CancellationToken ct = default)
  {
      var upper = rate.Currency.ToUpperInvariant();
      var existing = await _db.ExchangeRateCache
          .FindAsync(new object[] { rate.Date, upper }, ct);
      if (existing is not null)
          _db.Entry(existing).CurrentValues.SetValues(
              new ExchangeRate(rate.Date, upper, rate.RateToRsd));
      else
          _db.ExchangeRateCache.Add(new ExchangeRate(rate.Date, upper, rate.RateToRsd));
      await _db.SaveChangesAsync(ct);
  }
  ```

  **`SaveBatchAsync`** implementation (batch upsert):
  ```csharp
  public async Task SaveBatchAsync(IReadOnlyList<ExchangeRate> rates, CancellationToken ct = default)
  {
      foreach (var rate in rates)
      {
          var upper = rate.Currency.ToUpperInvariant();
          var existing = await _db.ExchangeRateCache
              .FindAsync(new object[] { rate.Date, upper }, ct);
          if (existing is not null)
              _db.Entry(existing).CurrentValues.SetValues(
                  new ExchangeRate(rate.Date, upper, rate.RateToRsd));
          else
              _db.ExchangeRateCache.Add(new ExchangeRate(rate.Date, upper, rate.RateToRsd));
      }
      await _db.SaveChangesAsync(ct);
  }
  ```
  Note: `CurrentValues.SetValues` on a `record` with `init` setters is supported by EF Core 8.
  It matches by property name (`Date`, `Currency`, `RateToRsd`). The new `ExchangeRate` instance
  passed to `SetValues` ensures the `RateToRsd > 0` invariant is upheld. No `.Result`/`.Wait()`.

**Checkpoint**: `dotnet build src/Rentier.Infrastructure` produces zero errors.

---

## Phase 5: Infrastructure Fetcher (T006)

**Purpose**: Implement `IExchangeRateFetcher` — the NBS HTTP-calling, XML-parsing, cache-first
service. Depends on T001 (interface), T005 (repository), and the existing
`IExchangeRateCacheRepository` (already defined, unchanged from earlier features).

- [X] T006 [INFRASTRUCTURE] CREATE `src/Rentier.Infrastructure/ExchangeRates/NbsExchangeRateFetcher.cs`:
  Namespace: `Rentier.Infrastructure.ExchangeRates`.
  Usings:
  ```csharp
  using System.Globalization;
  using System.Xml.Linq;
  using Rentier.Application.Common;
  using Rentier.Application.Interfaces;
  using Rentier.Application.Repositories;
  using Rentier.Domain.ValueObjects;
  ```
  Class declaration: `public sealed class NbsExchangeRateFetcher : IExchangeRateFetcher`

  **Supported currencies set** (static, used for both input validation and XML filtering):
  ```csharp
  public static readonly IReadOnlySet<string> SupportedCurrencies =
      new HashSet<string>(StringComparer.OrdinalIgnoreCase)
      { "EUR", "USD", "GBP", "CHF", "AUD", "CAD", "CZK", "DKK",
        "HUF", "JPY", "NOK", "PLN", "SEK", "TRY", "AED" };
  ```

  Constructor:
  ```csharp
  public NbsExchangeRateFetcher(HttpClient http, IExchangeRateCacheRepository cache)
  { _http = http; _cache = cache; }
  private readonly HttpClient _http;
  private readonly IExchangeRateCacheRepository _cache;
  ```

  **`FetchRateAsync`** full algorithm:
  ```csharp
  public async Task<Result<ExchangeRate, Error>> FetchRateAsync(
      DateOnly date, string currency, CancellationToken ct = default)
  {
      // Step 1: Validate currency
      var upperCurrency = currency.ToUpperInvariant();
      if (!SupportedCurrencies.Contains(upperCurrency))
          return Result<ExchangeRate, Error>.Failure(
              new Error("UNSUPPORTED_CURRENCY",
                  $"Currency '{upperCurrency}' is not supported by NBS."));

      // Step 2: Cache lookup
      var cached = await _cache.GetAsync(date, upperCurrency, ct);
      if (cached is not null)
          return Result<ExchangeRate, Error>.Success(cached);

      // Step 3: Build NBS URL — date format MUST be MM/dd/yyyy (US month-first).
      // IMPORTANT: use CultureInfo.InvariantCulture — the '/' in a format string is
      // the culture-specific date separator, which on Serbian/European locales becomes '.'
      // (producing "01.15.2024" instead of "01/15/2024", which NBS rejects).
      var url = $"https://webservices.nbs.rs/CommunicationOfficeService1_3/" +
                $"ExchangeRateXmlService.asmx/GetAllExchangeRates" +
                $"?InputDate={date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)}&CurrencyCodeCo=0";

      // Step 4: HTTP GET
      HttpResponseMessage response;
      try
      {
          response = await _http.GetAsync(url, ct);
      }
      catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
      {
          return Result<ExchangeRate, Error>.Failure(
              new Error("NBS_HTTP_ERROR", ex.Message));
      }

      if (!response.IsSuccessStatusCode)
          return Result<ExchangeRate, Error>.Failure(
              new Error("NBS_HTTP_ERROR",
                  $"NBS returned {(int)response.StatusCode} {response.StatusCode}."));

      // Step 5: Read body
      var xml = await response.Content.ReadAsStringAsync(ct);

      // Step 6: Parse XML — LocalName used to avoid namespace binding fragility
      XDocument doc;
      try { doc = XDocument.Parse(xml); }
      catch (Exception ex)
      {
          return Result<ExchangeRate, Error>.Failure(
              new Error("NBS_PARSE_ERROR",
                  $"Failed to parse NBS XML: {ex.Message}"));
      }

      // Step 7: Extract entries — empty = weekend / Serbian holiday
      var entries = doc.Descendants()
          .Where(e => e.Name.LocalName == "ExchangeRateXml")
          .ToList();

      if (entries.Count == 0)
          return Result<ExchangeRate, Error>.Failure(
              new Error("RATE_NOT_FOUND",
                  $"No NBS exchange rate published for {date:yyyy-MM-dd}."));

      // Step 8: Build batch — only SupportedCurrencies are retained
      var allRates = new List<ExchangeRate>();
      foreach (var entry in entries)
      {
          var code = entry.Elements()
              .FirstOrDefault(e => e.Name.LocalName == "CurrencyCodeCo")?.Value;
          if (code is null || !SupportedCurrencies.Contains(code)) continue;

          var unit = decimal.Parse(
              entry.Elements().First(e => e.Name.LocalName == "Unit").Value,
              CultureInfo.InvariantCulture);
          var middle = decimal.Parse(
              entry.Elements().First(e => e.Name.LocalName == "Middle_Rate").Value,
              CultureInfo.InvariantCulture);

          allRates.Add(new ExchangeRate(date, code.ToUpperInvariant(), middle / unit));
      }

      // Step 9: Batch cache all rates for this date
      await _cache.SaveBatchAsync(allRates, ct);

      // Step 10: Return requested rate from parsed batch (not re-queried from DB)
      var result = allRates.FirstOrDefault(r =>
          string.Equals(r.Currency, upperCurrency, StringComparison.OrdinalIgnoreCase));

      return result is not null
          ? Result<ExchangeRate, Error>.Success(result)
          : Result<ExchangeRate, Error>.Failure(
              new Error("RATE_NOT_FOUND",
                  $"Currency '{upperCurrency}' not found in NBS response for {date:yyyy-MM-dd}."));
  }
  ```
  Notes:
  - All `decimal.Parse` calls use `CultureInfo.InvariantCulture` — critical for correct parsing.
  - Rate formula: `RateToRsd = Middle_Rate / Unit`. Unit = 100 for JPY/HUF (e.g. JPY Unit=100,
    Middle_Rate=77.35 → RateToRsd=0.7735).
  - The requested rate is found in the parsed batch list, not re-queried from DB — one fewer
    async hop.
  - No retry logic in v1 — NBS call either succeeds or returns failure immediately.
  - No `.Result`/`.Wait()` anywhere.

**Checkpoint**: `dotnet build src/Rentier.Infrastructure` produces zero errors.

---

## Phase 6: Infrastructure Tests (T007–T008)

**Purpose**: Cover the repository upsert logic and the fetcher's cache-first / XML-parsing /
error-handling paths. T007 and T008 touch different test files and can proceed in parallel
once their subjects (T005 and T006) are complete.

### Repository Tests

- [X] T007 [P] [TEST] CREATE `tests/Rentier.Infrastructure.Tests/ExchangeRateCacheRepositoryTests.cs`:
  Namespace: `Rentier.Infrastructure.Tests`.
  Usings: `using FluentAssertions;` `using Microsoft.Data.Sqlite;` `using Microsoft.EntityFrameworkCore;`
  `using Rentier.Domain.ValueObjects;` `using Rentier.Infrastructure.Persistence;`
  `using Rentier.Infrastructure.Repositories;` `using Xunit;`

  **Test fixture / helper**: Each test method creates its own in-memory SQLite connection,
  builds an `AppDbContext` with `UseSqlite(connection)`, calls `await db.Database.EnsureCreatedAsync()`,
  and disposes both the context and connection when done. Pattern:
  ```csharp
  private static async Task<(AppDbContext db, SqliteConnection conn)> CreateDbAsync()
  {
      var conn = new SqliteConnection("Data Source=:memory:");
      await conn.OpenAsync();
      var options = new DbContextOptionsBuilder<AppDbContext>()
          .UseSqlite(conn).Options;
      var db = new AppDbContext(options);
      await db.Database.EnsureCreatedAsync();
      return (db, conn);
  }
  ```

  **Tests to implement** (all `[Fact]`, all async Task, all using `FluentAssertions`):

  `GetAsync_NoCachedRate_ReturnsNull`: Empty DB; call `GetAsync(new DateOnly(2024,1,15), "EUR")`;
  assert result is null.

  `SaveAsync_NewRate_PersistsToDb`: Save `new ExchangeRate(new DateOnly(2024,1,15), "EUR", 117.5952m)`;
  call `GetAsync` for same key; assert not null, `RateToRsd` equals `117.5952m`.

  `SaveAsync_ExistingRate_UpdatesRateToRsd`: Save EUR@100m, then call `SaveAsync` again with
  EUR@117m on same date; call `GetAsync`; assert `RateToRsd` is `117m` (updated, not duplicated).

  `SaveBatchAsync_NewRates_PersistsAll`: Batch of 3 rates for `2024-01-15` (EUR, USD, GBP);
  after `SaveBatchAsync`, assert `db.ExchangeRateCache.Count()` is 3.

  `SaveBatchAsync_DuplicateRates_Upserts`: Seed EUR@100m via `SaveAsync`; then call
  `SaveBatchAsync` with a batch containing EUR@117m on the same date; assert no exception is
  thrown, and `GetAsync` returns EUR with `RateToRsd == 117m`.

  `GetByDateRangeAsync_FiltersCorrectly`: Seed 5 rates: EUR on dates 2024-01-13, 2024-01-14,
  2024-01-15, 2024-01-16, 2024-01-17; query range `(2024-01-14, 2024-01-16, "EUR")`;
  assert result has 3 items, all with EUR, ordered by date ascending.

  `GetAsync_CurrencyNormalisedUppercase`: Save with `SaveAsync(new ExchangeRate(..., "EUR", ...))`;
  call `GetAsync(date, "eur")`; assert result is not null (lowercase lookup returns stored record).

### Fetcher Tests

- [X] T008 [P] [TEST] CREATE `tests/Rentier.Infrastructure.Tests/NbsExchangeRateFetcherTests.cs`:
  Namespace: `Rentier.Infrastructure.Tests`.
  Usings: `using System.Net;` `using FluentAssertions;` `using NSubstitute;`
  `using Rentier.Application.Repositories;` `using Rentier.Domain.ValueObjects;`
  `using Rentier.Infrastructure.ExchangeRates;` `using Xunit;`

  **`FakeHttpMessageHandler`** inner class (no Moq — plain `HttpMessageHandler` subclass):
  ```csharp
  internal sealed class FakeHttpMessageHandler : HttpMessageHandler
  {
      private readonly string _response;
      private readonly HttpStatusCode _statusCode;
      public int CallCount { get; private set; }

      internal FakeHttpMessageHandler(string response,
          HttpStatusCode statusCode = HttpStatusCode.OK)
      { _response = response; _statusCode = statusCode; }

      protected override Task<HttpResponseMessage> SendAsync(
          HttpRequestMessage request, CancellationToken ct)
      {
          CallCount++;
          return Task.FromResult(new HttpResponseMessage(_statusCode)
              { Content = new StringContent(_response) });
      }
  }
  ```

  **Sample valid XML** constant (use for cache-miss / parse tests):
  ```csharp
  private const string ValidXml = """
      <?xml version="1.0" encoding="utf-8"?>
      <ArrayOfExchangeRateXml xmlns="http://www.nbs.rs/kursnaListaModul/schema">
        <ExchangeRateXml>
          <CurrencyCodeCo>EUR</CurrencyCodeCo>
          <Unit>1</Unit>
          <Middle_Rate>117.5952</Middle_Rate>
        </ExchangeRateXml>
        <ExchangeRateXml>
          <CurrencyCodeCo>USD</CurrencyCodeCo>
          <Unit>1</Unit>
          <Middle_Rate>108.4321</Middle_Rate>
        </ExchangeRateXml>
        <ExchangeRateXml>
          <CurrencyCodeCo>JPY</CurrencyCodeCo>
          <Unit>100</Unit>
          <Middle_Rate>77.3500</Middle_Rate>
        </ExchangeRateXml>
      </ArrayOfExchangeRateXml>
      """;
  ```

  **Unit tests** (all `[Fact]`, all async Task):

  `FetchRateAsync_UnsupportedCurrency_ReturnsFailureWithoutHttpCall`:
  Handler with `CallCount`; call `FetchRateAsync(date, "XYZ")`;
  assert `IsSuccess == false`, `Error.Code == "UNSUPPORTED_CURRENCY"`, `handler.CallCount == 0`.

  `FetchRateAsync_CacheHit_ReturnsCachedRateWithoutHttpCall`:
  Mock `IExchangeRateCacheRepository` with NSubstitute; `GetAsync` returns
  `new ExchangeRate(date, "EUR", 117m)`;
  create `FakeHttpMessageHandler` with any XML;
  call `FetchRateAsync(date, "EUR")`;
  assert `IsSuccess == true`, `Value.RateToRsd == 117m`, `handler.CallCount == 0`.

  `FetchRateAsync_CacheMiss_ParsesXmlAndCachesAllRates`:
  Mock repo where `GetAsync` returns `null`; handler returns `ValidXml` (3 currencies);
  call `FetchRateAsync(date, "EUR")`;
  assert `IsSuccess == true`, `Value.Currency == "EUR"`, `Value.RateToRsd == 117.5952m`;
  assert `SaveBatchAsync` was called once on the mock repo with a list of 3 rates
  (`Received(1)` call via NSubstitute).

  `FetchRateAsync_CurrencyNotInXml_ReturnsRateNotFoundError`:
  Handler returns `ValidXml` (EUR, USD, JPY only); request `"CHF"` (CHF is supported but not
  in this XML); assert `IsSuccess == false`, `Error.Code == "RATE_NOT_FOUND"`.

  `FetchRateAsync_HttpError_ReturnsNbsHttpErrorFailure`:
  Handler returns `HttpStatusCode.InternalServerError`;
  call `FetchRateAsync(date, "EUR")`;
  assert `IsSuccess == false`, `Error.Code == "NBS_HTTP_ERROR"`.

  `FetchRateAsync_MalformedXml_ReturnsParseError`:
  Handler returns `"this is not xml"`;
  call `FetchRateAsync(date, "EUR")`;
  assert `IsSuccess == false`, `Error.Code == "NBS_PARSE_ERROR"`.

  `FetchRateAsync_UnitIsHundred_ComputesCorrectRate`:
  Handler returns XML containing only JPY with `Unit=100` and `Middle_Rate=77.35`;
  call `FetchRateAsync(date, "JPY")`;
  assert `IsSuccess == true`, `Value.RateToRsd == 0.7735m`
  (i.e. `77.35m / 100m`, computed in `decimal` arithmetic via `CultureInfo.InvariantCulture`).

  `FetchRateAsync_CurrencyCodeCaseInsensitive`:
  Handler returns `ValidXml`; call `FetchRateAsync(date, "eur")` (lowercase);
  assert `IsSuccess == true`, `Value.Currency == "EUR"`.

  **Integration tests** (real HTTP, tagged to be filtered from CI):

  ```csharp
  [Trait("Category", "Integration")]
  public class NbsIntegrationTests
  ```

  `FetchRateAsync_RealNbs_ReturnsEurRate`:
  Use `new HttpClient()` and a stub `IExchangeRateCacheRepository` where `GetAsync` always
  returns `null` and `SaveBatchAsync` is a no-op;
  call `FetchRateAsync(new DateOnly(2024, 1, 15), "EUR")` (Monday — NBS publishes rates);
  assert `result.IsSuccess == true` and `result.Value.RateToRsd > 100m`
  (EUR/RSD was ~117 on that date).

  `FetchRateAsync_RealNbs_ReturnsUsdRate`:
  Same setup; call for `"USD"` on `2024-01-15`;
  assert `result.IsSuccess == true` and `result.Value.RateToRsd > 0m`.

  CI command to exclude integration tests:
  `dotnet test --filter "Category!=Integration"`

**Checkpoint**: `dotnet test tests/Rentier.Infrastructure.Tests --filter "Category!=Integration"`
passes all unit tests. Integration tests pass when run manually with network access.

---

## Phase 7: DI Wiring (T009)

**Purpose**: Register the new repository and typed HTTP client in the DI container.
Depends on T001 (interface), T005 (repository), T006 (fetcher) all being complete.

- [X] T009 [DI] MODIFY `src/Rentier.Infrastructure/InfrastructureServiceExtensions.cs`:
  Add three new `using` directives (only if not already present):
  ```csharp
  using Rentier.Application.Interfaces;
  using Rentier.Infrastructure.ExchangeRates;
  using Rentier.Infrastructure.Repositories;
  ```
  Locate the existing tail of `AddInfrastructureServices` — the block ending with the
  `#pragma warning restore CA1416` / `return services;` lines. Insert two new registrations
  **before** `return services;`:
  ```csharp
  services.AddTransient<IExchangeRateCacheRepository, ExchangeRateCacheRepository>();
  services.AddHttpClient<IExchangeRateFetcher, NbsExchangeRateFetcher>();
  return services;
  ```
  Notes:
  - `AddTransient` is consistent with all existing repository registrations in this file.
  - `AddHttpClient<IExchangeRateFetcher, NbsExchangeRateFetcher>()` registers
    `NbsExchangeRateFetcher` as a typed client; `IHttpClientFactory` manages socket lifecycle.
  - The `IExchangeRateCacheRepository` registration is required by `NbsExchangeRateFetcher`'s
    constructor — it must be registered before the fetcher is first resolved.

**Checkpoint**: `dotnet build` on the full solution produces zero errors and zero warnings.
`dotnet test tests/Rentier.Infrastructure.Tests --filter "Category!=Integration"` remains green.

---

## Phase 8: CI Configuration (T010)

**Purpose**: Ensure the CI workflow excludes real-network integration tests from automated runs.
Integration tests tagged `[Trait("Category","Integration")]` make live HTTP calls to `webservices.nbs.rs`
and must not run on CI agents that may lack network access or when NBS is temporarily unavailable.

> **⚠️ NOTE**: This change was pre-applied during `speckit.analyze`. Verify the filter is present
> before raising a PR.

- [X] T010 [INFRASTRUCTURE] VERIFY/APPLY `--filter "Category!=Integration"` in `.github/workflows/ci.yml`:
  Locate the `Test` step in the `build` job. Confirm (or add) the filter flag:
  ```yaml
  - name: Test
    run: dotnet test Rentier.slnx --no-build -c Release --filter "Category!=Integration" --collect:"XPlat Code Coverage" --results-directory ./coverage --logger "console;verbosity=normal"
  ```
  This ensures integration tests are skipped in CI. Integration tests can still be run locally:
  ```shell
  dotnet test tests/Rentier.Infrastructure.Tests --filter "Category=Integration"
  ```

**Checkpoint**: CI passes without network access. Integration tests pass when run manually.

---

## Dependencies & Execution Order

### Phase Dependencies

```
T001  (Application Interface)
  └─▶ T002 [P] (EF Config)        ─┐
  └─▶ T003 [P] (AppDbContext)      ├─▶ T004 (EF Migration)
                                   ┘       └─▶ T005 (Repository)
                                                    └─▶ T006 (Fetcher)
                                                    └─▶ T007 [P] (Repo Tests)
                                                             └─▶ T008 [P] (Fetcher Tests)
                                                    └─▶ T009 (DI Wiring)
T010  (CI Config — no dependencies, verify at any point before PR)
```

- **T001**: No dependencies — start immediately
- **T002 + T003**: No inter-dependencies — run in parallel after T001
- **T004**: Requires T002 + T003 — blocks T005
- **T005**: Requires T004 — blocks T007 and T009
- **T006**: Requires T001 + T005 — blocks T008 and T009
- **T007**: Requires T005 — can run in parallel with T008
- **T008**: Requires T006 — can run in parallel with T007
- **T009**: Requires T001 + T005 + T006 — final wiring step
- **T010**: No dependencies — verify at any point; must be confirmed before PR

### Parallel Opportunities

```bash
# Phase 2 — run together (different files):
Task: "CREATE ExchangeRateCacheConfiguration.cs"   # T002
Task: "MODIFY AppDbContext.cs"                     # T003

# Phase 6 — run together after T005 and T006 complete:
Task: "CREATE ExchangeRateCacheRepositoryTests.cs" # T007
Task: "CREATE NbsExchangeRateFetcherTests.cs"      # T008
```

---

## Implementation Strategy

### Full Delivery (Single Story — this feature is a single deliverable)

1. Complete T001: Application interface — confirm compile
2. Complete T002 + T003 in parallel: EF config + DbContext — confirm compile
3. Complete T004: EF migration — confirm file generated and build clean
4. Complete T005: Repository implementation
5. Complete T006: Fetcher implementation
6. Complete T007 + T008 in parallel: Tests — confirm all unit tests green
7. Complete T009: DI wiring — confirm full solution build
8. Verify T010: CI integration test filter — confirm `--filter "Category!=Integration"` is in `ci.yml`

### Validation Checklist (run before PR)

- [ ] `dotnet build` — zero errors, zero warnings
- [ ] `dotnet test tests/Rentier.Infrastructure.Tests --filter "Category!=Integration"` — all pass
- [ ] `dotnet test tests/Rentier.Infrastructure.Tests --filter "Category=Integration"` — both pass
  (requires network access to `webservices.nbs.rs`)
- [ ] No `.Result`/`.Wait()` anywhere in new files
- [ ] All `decimal.Parse` calls use `CultureInfo.InvariantCulture`
- [ ] `FindAsync` key order matches `HasKey(e => new { e.Date, e.Currency })` (Date first)
- [ ] NBS URL date format is `MM/dd/yyyy` built with `CultureInfo.InvariantCulture` (not interpolated `{date:MM/dd/yyyy}` which is culture-sensitive)
- [ ] Error codes match: `UNSUPPORTED_CURRENCY`, `RATE_NOT_FOUND`, `NBS_HTTP_ERROR`, `NBS_PARSE_ERROR`
- [ ] Migration `0006_ExchangeRateCache` exists and is correctly numbered
- [ ] Constitution check: no `double`, no `DateTime` — only `decimal` and `DateOnly` for financial/temporal values

---

## Notes

- [P] tasks = different files, no blocking dependencies
- No CQRS handlers in this feature — `IExchangeRateFetcher` is a direct service, not a command/query handler
- No Desktop layer changes — this is a pure Infrastructure/Application service
- `ExchangeRate` domain value object is **unchanged** — T002 promotes it to an EF entity via configuration only
- `IExchangeRateCacheRepository` interface is **unchanged** — T005 implements it
- Integration tests are NOT skipped by default — they are filtered in CI via `--filter "Category!=Integration"`;
  run them locally when network is available to validate the real NBS endpoint
- Commit after each task or logical group to keep rollback scope small
