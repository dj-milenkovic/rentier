# Quickstart: NBS Exchange Rate Fetcher (Feature 006)

**Date**: 2026-04-07

---

## Prerequisites

- .NET 8 SDK
- EF Core CLI tools: `dotnet tool restore` (installs `dotnet-ef` from `.config/dotnet-tools.json`)
- Working directory: repository root `F:\Projects\Rentier\rentier`

---

## 1. Build

```shell
dotnet build Rentier.slnx
```

Expected: 0 errors, 0 warnings.

---

## 2. Generate the EF Migration

```shell
dotnet ef migrations add 0006_ExchangeRateCache \
    --project src/Rentier.Infrastructure \
    --startup-project src/Rentier.Desktop \
    --output-dir Persistence/Migrations
```

Verify a new file `20xxxxxx_0006_ExchangeRateCache.cs` appears under  
`src/Rentier.Infrastructure/Persistence/Migrations/`.

---

## 3. Apply the Migration

```shell
dotnet ef database update \
    --project src/Rentier.Infrastructure \
    --startup-project src/Rentier.Desktop
```

This creates (or updates) the local SQLite file and adds the `ExchangeRateCache` table.

---

## 4. Run Unit Tests

```shell
dotnet test tests/Rentier.Infrastructure.Tests \
    --filter "Category!=Integration" \
    --logger "console;verbosity=normal"
```

Expected output includes:
- `ExchangeRateCacheRepositoryTests` — all 6 tests pass
- `NbsExchangeRateFetcherTests` — all 7 unit tests pass (integration tests skipped by filter)

---

## 5. Run Integration Tests (requires internet access)

Integration tests make a real HTTP request to the NBS web service. Run them explicitly:

```shell
dotnet test tests/Rentier.Infrastructure.Tests \
    --filter "Category=Integration" \
    --logger "console;verbosity=normal"
```

Expected: `NbsIntegrationTests.FetchRateAsync_RealNbs_ReturnsPositiveEurRate` passes with `RateToRsd > 0` for `EUR` on `2024-01-15`.

> **Note**: These tests require outbound HTTPS access to `webservices.nbs.rs`. They will fail in network-restricted CI environments. The default CI pipeline excludes them — see below.

---

## 6. CI Pipeline Configuration

In `.github/workflows/ci.yml`, the test step should use:

```yaml
- name: Run tests (unit only)
  run: |
    dotnet test Rentier.slnx \
      --filter "Category!=Integration" \
      --configuration Release \
      --no-build \
      --logger "trx;LogFileName=test-results.trx"
```

Add a separate optional job for integration tests if desired:

```yaml
integration-tests:
  if: github.event_name == 'workflow_dispatch'
  steps:
    - name: Run integration tests
      run: |
        dotnet test tests/Rentier.Infrastructure.Tests \
          --filter "Category=Integration"
```

---

## 7. Verify the Cache Table

After running the integration test (or any test that populates the DB), inspect the SQLite database:

```shell
# Using sqlite3 CLI
sqlite3 rentier.db "SELECT Date, Currency, RateToRsd FROM ExchangeRateCache LIMIT 20;"
```

Expected: rows for `2024-01-15` with 15 currencies and positive `RateToRsd` values.

---

## 8. Using `IExchangeRateFetcher` in Application Code

Inject the service in any Application handler:

```csharp
public sealed class MyHandler(IExchangeRateFetcher fetcher)
{
    public async Task<Result<decimal, Error>> GetEurRateAsync(DateOnly date, CancellationToken ct)
    {
        var result = await fetcher.FetchRateAsync(date, "EUR", ct);
        return result.IsSuccess
            ? Result.Success(result.Value.RateToRsd)
            : Result.Failure<decimal, Error>(result.Error);
    }
}
```

The `IExchangeRateFetcher` is registered in DI — no `new` required.

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| `No migrations applied` | Forgot `database update` | Run step 3 |
| Integration test `HttpRequestException` | No internet / NBS down | Check connectivity; test is optional |
| `DomainException: RateToRsd must be positive` | NBS returned `0` for a rate | Inspect raw XML; likely a data issue for that date |
| Migration conflict with snapshot | Another feature branch also added migration | Rebase, regenerate migration |
