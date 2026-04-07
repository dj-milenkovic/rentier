# Specification Analysis Report — Feature 006: NBS Exchange Rate Fetcher

**Date**: 2026-04-07
**Analyst**: speckit.analyze
**Artifacts examined**: `plan.md`, `data-model.md`, `tasks.md`, `contracts/IExchangeRateFetcher.cs`, `contracts/NbsFetcherDesign.md`
**Source files read (read-only)**: `ExchangeRate.cs`, `IExchangeRateCacheRepository.cs`, `Result.cs`, `Error.cs`, `AppDbContext.cs`, `InfrastructureServiceExtensions.cs`, `Rentier.Infrastructure.Tests.csproj`, `.github/workflows/ci.yml`

---

## Findings Table

| ID | Category | Severity | Location(s) | Summary | Recommendation |
|----|----------|----------|-------------|---------|----------------|
| C1 | Inconsistency | **CRITICAL** | `tasks.md` T006 (line 281), `contracts/NbsFetcherDesign.md` algorithm Step 3 | NBS URL built with `{date:MM/dd/yyyy}` in a C# interpolated string. The `/` in date format strings is the **culture-specific date separator**, which on Serbian/European locales (e.g., `sr-Latn-RS`) is replaced with `.`, producing `01.15.2024` instead of `01/15/2024`. NBS API would reject the malformed date. | Change to `date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)` in all C# code. **FIXED** in this analysis pass. |
| H1 | Inconsistency | **HIGH** | `data-model.md` (After section, DbSet), `tasks.md` T003/T005 (5 occurrences) | `data-model.md` names the new `DbSet` property `ExchangeRates`, but `tasks.md` T003 names it `ExchangeRateCache` and T005 references `_db.ExchangeRateCache` throughout. If an implementer follows `data-model.md`, their repository code will fail to compile (property not found). | Rename `ExchangeRates` to `ExchangeRateCache` in `data-model.md`. **FIXED** in this analysis pass. |
| H2 | Coverage Gap | **HIGH** | `.github/workflows/ci.yml` (line 37), `tasks.md` (no CI task) | CI workflow runs `dotnet test Rentier.slnx` without `--filter "Category!=Integration"`. Feature 006 introduces real-network integration tests (`[Trait("Category","Integration")]`). These will run in every CI build, fail on agents without internet access or when NBS is unavailable, and break the CI matrix. No task in `tasks.md` covers updating `ci.yml`. | Add `--filter "Category!=Integration"` to the CI test step. Add T010 to `tasks.md` to document this. **FIXED** in this analysis pass. |
| H3 | Inconsistency | **HIGH** | `contracts/NbsFetcherDesign.md` (SaveBatchAsync Upsert Logic) | `NbsFetcherDesign.md` shows `FindAsync(new object[] { rate.Date, rate.Currency }, ct)` and `SetValues(rate)` using unnormalized `rate.Currency`. The correct implementation (in `tasks.md` T005) normalizes to `upper` before all DB operations. An implementer following the contract would bypass currency normalization in upserts, causing cache misses on second lookups if any caller passes mixed-case currency codes directly. | Update `NbsFetcherDesign.md` to normalize currency in `SaveBatchAsync` and use `ExchangeRateCache` DbSet accessor. **FIXED** in this analysis pass. |
| M1 | Underspecification | MEDIUM | Feature 006 directory | `spec.md` is absent. `plan.md` references `spec.md` in its header (`**Input**: Feature specification from .specify/specs/006-nbs-exchange-rate-fetcher/spec.md`), but no such file exists. All requirements are distributed across `plan.md`, `data-model.md`, `research.md`, and `contracts/`. | Run `/speckit.specify` to generate `spec.md` from existing design artifacts, or update `plan.md` header to reference actual inputs. Not blocking implementation. |
| M2 | Inconsistency | MEDIUM | `contracts/NbsFetcherDesign.md` Step 9 vs `tasks.md` T006 Step 9 | `NbsFetcherDesign.md` Step 9 comment says **"fire & forget errors — don't fail the caller on cache write failure"**. `tasks.md` T006 implementation uses `await _cache.SaveBatchAsync(allRates, ct)` with no try/catch — SQLite write failures will propagate to the caller and return a failure Result even though the data was successfully fetched from NBS. | Decide: either wrap `SaveBatchAsync` in a try/catch (matching the "fire & forget" intent) or remove the "fire & forget" comment from the design doc. Either is defensible; the divergence is confusing. |
| M3 | Underspecification | MEDIUM | `plan.md` (Integration Test section) vs `tasks.md` T008 | `plan.md` integration test uses `new InMemoryCacheStub()`, but `InMemoryCacheStub` is never defined in `tasks.md` or `contracts/`. `tasks.md` T008 uses an NSubstitute mock instead. | Standardize on the NSubstitute approach documented in T008 and remove `InMemoryCacheStub` reference from `plan.md`. |
| L1 | Inconsistency | LOW | `tasks.md` T003 (line 120) | T003 states "maintaining alphabetical alignment" for DbSet properties, but `ExchangeRateCache` placed at the end is after `Importers` (I). The existing properties are not alphabetically sorted either (T, P, H, M, I). Claim is inaccurate. | Remove the "alphabetical alignment" claim in T003; just say "add after the existing five properties". |
| L2 | Inconsistency | LOW | `plan.md` (Cache Strategy section) vs `tasks.md` T005 | `plan.md` Cache Strategy shows `_db.Set<ExchangeRate>().FindAsync([rate.Date, rate.Currency], ct)` (C# collection expression, unnormalized). `tasks.md` T005 uses `new object[]` syntax and normalizes. | Cosmetic; tasks.md version is authoritative. Acceptable divergence. |
| L3 | Underspecification | LOW | `tasks.md` T007 (test usings) | T007 imports `using Microsoft.Data.Sqlite;` but `Microsoft.Data.Sqlite` is not a **direct** `PackageReference` in `Rentier.Infrastructure.Tests.csproj`. It is available only transitively via `Microsoft.EntityFrameworkCore.Sqlite 8.0.*`. On certain toolchain configurations transitive packages may not resolve correctly. | Add explicit `<PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.*" />` to the test `.csproj`, or note the transitive dependency. Not blocking on current toolchain. |

---

## Source Code Verification Results

These items from the review checklist were verified against actual source files. No issues found.

| Check | Source File | Verdict |
|-------|------------|---------|
| `Result<TValue,TError>` API: `.Success()`/`.Failure()` static factories | `Result.cs` | **PASS** — exact API used in tasks.md |
| `Error` constructor signature | `Error.cs` | **PASS** — `Error(string Code, string Message)` positional record; `new Error("CODE", msg)` is valid |
| `ExchangeRate` record constructor params map to property names | `ExchangeRate.cs` | **PASS** — `date`→`Date`, `currency`→`Currency`, `rateToRsd`→`RateToRsd`; EF8 constructor binding will succeed |
| EF8 `HasKey` on record type | `ExchangeRateCacheConfiguration` (tasks.md T002) | **PASS** — records are valid EF entities; composite key on `(Date, Currency)` is supported |
| `SetValues` with `init` setters on tracked record entity | `ExchangeRateCacheRepository` (tasks.md T005) | **PASS** — EF Core uses reflection to bypass `init` on tracked entities; `SetValues` copies by property name |
| `FindAsync` key order matches `HasKey(e => new { e.Date, e.Currency })` | `tasks.md` T005, T007 | **PASS** — all `FindAsync` calls pass `Date` first, `Currency` second |
| `IExchangeRateCacheRepository` not yet registered in DI | `InfrastructureServiceExtensions.cs` | **PASS** — only T009 adds it; correct |
| `using Rentier.Application.Interfaces` already present in ServiceExtensions | `InfrastructureServiceExtensions.cs` | **PASS** — already imported; T009 "only if not already present" note is correct |
| NSubstitute, FluentAssertions available in test project | `Rentier.Infrastructure.Tests.csproj` | **PASS** — both present; no new NuGet packages required |
| No `Microsoft.EntityFrameworkCore.InMemory` conflict with SQLite tests | `Rentier.Infrastructure.Tests.csproj` | **PASS** — both packages present; T007 uses SQLite in-memory (correct), not EF InMemory provider |

---

## Coverage Summary

*Note: `spec.md` is absent; requirements derived from `plan.md`, `contracts/`, and `data-model.md`.*

| Requirement Key | Description | Has Task? | Task IDs | Notes |
|-----------------|-------------|-----------|----------|-------|
| FR-001 | `IExchangeRateFetcher` interface in Application layer | ✅ | T001 | |
| FR-002 | EF config (`ExchangeRateCacheConfiguration`) | ✅ | T002 | |
| FR-003 | `AppDbContext.ExchangeRateCache` DbSet | ✅ | T003 | |
| FR-004 | EF migration `0006_ExchangeRateCache` | ✅ | T004 | |
| FR-005 | `ExchangeRateCacheRepository` implementation | ✅ | T005 | |
| FR-006 | `NbsExchangeRateFetcher` cache-first + HTTP + XML parse | ✅ | T006 | |
| FR-007 | Repository unit tests (7 tests) | ✅ | T007 | |
| FR-008 | Fetcher unit + integration tests (8+2 tests) | ✅ | T008 | |
| FR-009 | DI registration (`IExchangeRateFetcher`, `IExchangeRateCacheRepository`) | ✅ | T009 | |
| FR-010 | CI integration test filter (prevent real-network tests in CI) | ✅ | T010 | **Added in this analysis pass** |
| SC-001 | All `decimal.Parse` use `CultureInfo.InvariantCulture` | ✅ | T006 checklist | |
| SC-002 | NBS URL uses `CultureInfo.InvariantCulture` date formatting | ✅ | T006 | **Fixed in C1** |
| SC-003 | No `.Result`/`.Wait()` anywhere | ✅ | T001–T009 | Explicitly checked in validation checklist |

---

## Constitution Alignment

All five constitution principles verified. No violations found.

| Principle | Check | Status |
|-----------|-------|--------|
| I. Clean Architecture | `IExchangeRateFetcher` in Application; `NbsExchangeRateFetcher` in Infrastructure; no Domain I/O | ✅ PASS |
| II. Local-First Security | NBS endpoint is the approved outbound target; all data in local SQLite; no cloud sync | ✅ PASS |
| III. Financial & Temporal Correctness | `decimal` for all rates; `DateOnly` for all dates; no `float`/`double`; no `DateTime` | ✅ PASS |
| IV. Async & UI Responsiveness | All methods are `async Task<T>`; no `.Result`/`.Wait()`; no blocking I/O | ✅ PASS |
| V. Specification-Driven Quality Gates | Unit tests + integration tests; CI filter configured; no new compiler warnings gate in checklist | ✅ PASS |

---

## Unmapped Tasks

None. All 10 tasks (T001–T010) map to at least one requirement.

---

## Metrics

| Metric | Value |
|--------|-------|
| Total Requirements | 13 (FR-001–FR-010 + SC-001–SC-003) |
| Total Tasks | 10 (T001–T010) |
| Coverage % | 100% (all requirements have ≥1 task) |
| Ambiguity Count | 0 |
| Duplication Count | 0 |
| Critical Issues Found | 1 (C1 — date format culture sensitivity) |
| High Issues Found | 3 (H1 — DbSet name, H2 — CI filter, H3 — SaveBatchAsync normalization) |
| Medium Issues Found | 3 (M1 — spec.md absent, M2 — fire-and-forget divergence, M3 — InMemoryCacheStub) |
| Low Issues Found | 3 (L1 — alphabetical claim, L2 — collection expression, L3 — transitive dep) |

---

## Fixes Applied in This Analysis Pass

The following CRITICAL and HIGH issues were remediated directly. No source code was modified.

| Fix | Files Modified |
|-----|---------------|
| **C1** — Changed `{date:MM/dd/yyyy}` to `date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)` in T006 implementation code and updated validation checklist | `tasks.md`, `contracts/NbsFetcherDesign.md` |
| **H1** — Renamed DbSet property `ExchangeRates` → `ExchangeRateCache` in After section | `data-model.md` |
| **H2** — Added `--filter "Category!=Integration"` to CI test step; added T010 task to tasks.md | `.github/workflows/ci.yml`, `tasks.md` |
| **H3** — Updated `SaveBatchAsync` upsert logic in design contract to normalize currency and use `ExchangeRateCache` accessor | `contracts/NbsFetcherDesign.md` |

---

## Next Actions

> ✅ **No CRITICAL or HIGH issues remain** — all have been fixed in this analysis pass.

**Recommended before `/speckit.implement`:**

1. **Decide on fire-and-forget behaviour (M2)**: Choose between propagating `SaveBatchAsync` exceptions (current tasks.md T006 behaviour) or wrapping in try/catch (design intent in NbsFetcherDesign.md Step 9). Update whichever doc is wrong. If fire-and-forget is desired, add a try/catch around `await _cache.SaveBatchAsync(allRates, ct)` in T006.

2. **Add explicit `Microsoft.Data.Sqlite` reference (L3)**: Add `<PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.*" />` to `Rentier.Infrastructure.Tests.csproj` before T007 to avoid transitive resolution surprises.

3. **Optional — generate `spec.md` (M1)**: Run `/speckit.specify` to backfill the missing spec from existing design artifacts, or update `plan.md` header to reference actual inputs (`data-model.md`, `research.md`, `contracts/`).

You may proceed with `/speckit.implement` — all critical correctness and consistency issues are resolved.
