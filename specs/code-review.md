# Rentier — Full Codebase Code Review

> **Reviewer:** C# Specialist (AI-assisted, Claude Opus 4.6)  
> **Date:** 2026-04-08  
> **Scope:** All 5 layers — Domain · Application · Infrastructure · Desktop · Tests  
> **Method:** Parallel layer-by-layer deep review of every source file  

---

## Severity Legend

| Icon | Meaning |
|------|---------|
| 🔴 **Critical** | Bug, security vulnerability, or correctness issue that must be fixed |
| 🟡 **Warning** | Design issue, reliability gap, or meaningful technical debt |
| 🟢 **Suggestion** | Improvement opportunity — low risk, worth considering |
| ℹ️ **Info** | Observation, no action required |

---

## Executive Summary

| Layer | 🔴 Critical | 🟡 Warning | 🟢 Suggestion | Grade |
|-------|------------|-----------|--------------|-------|
| Domain | 3 | 15 | 7 | B+ |
| Application | 2 | 9 | 8 | A- |
| Infrastructure | 2 | 18 | 10 | B+ |
| Desktop | 2 | 18 | 7 | B+ |
| Tests | 2 | 12 | 9 | A- |
| **Total** | **11** | **72** | **41** | — |

**Overall verdict:** The codebase is well-structured, follows Clean Architecture principles faithfully, and demonstrates strong engineering discipline. The Domain is hermetically sealed, CQRS is properly implemented, the Result pattern is applied consistently, and the test suite is remarkably thorough. The critical issues are concentrated in correctness/security gaps rather than fundamental architectural problems.

---

---

# Layer 1 — Domain (`Rentier.Domain`)

**Scope:** 22 C# files · 6 enums · 6 entities · 6 value objects · 3 services · 1 exception  
**TFM:** net8.0 | **LangVersion:** 12 | **Nullable:** enabled | **NuGet packages:** ZERO ✅

---

## 1.1 Architectural Purity

| Check | Status |
|-------|--------|
| No `<PackageReference>` in `Rentier.Domain.csproj` | ✅ Zero NuGet dependencies |
| No `<ProjectReference>` to other layers | ✅ |
| All `using` statements reference only `Rentier.Domain.*` or BCL | ✅ |
| No `System.Net`, `System.IO`, EF Core, or MailKit references | ✅ |

ℹ️ The domain is hermetically sealed. No I/O leakage. Full constitution Principle I compliance.

---

## 1.2 Type Safety

### ✅ All monetary values use `decimal`

Every money-related property across `Filing`, `FilingInfo`, `ExchangeRate`, `Money`, and `TaxCalculationService` uses `decimal`. No `double` or `float` anywhere in the domain.

### ✅ All dates use `DateOnly`

`Filing.TaxPeriod`, `Filing.IncomeDate`, `Filing.FilingDeadline`, `ExchangeRate.Date`, `MailboxCursor.LastSyncDate`, `Report.ImportDate`, `PublicHoliday.Date`, `SyncParameters.ReplayFromDate` — all `DateOnly`.

### 🟡 `DateTime.UtcNow` used inside domain entities

| File | Usage |
|------|-------|
| `Report.cs` line 40 | `DateOnly.FromDateTime(DateTime.UtcNow)` |
| `Report.cs` line 58 | `DateTime.UtcNow:yyyyMMddHHmmss` (revision suffix) |
| `Report.cs` line 68 | `DateOnly.FromDateTime(DateTime.UtcNow)` |
| `Mailbox.cs` line 38 | `DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-90)` |
| `SyncParameters.cs` line 28 | `DateOnly.FromDateTime(DateTime.UtcNow)` |

Direct clock access couples domain logic to system time, making it non-deterministic and harder to test. Accept a `DateOnly today` (or inject `TimeProvider`) parameter in `Create` and `CreateRevision` factory methods.

---

## 1.3 Domain Modeling

### Value Objects

| Value Object | Immutable? | Invariants? | Notes |
|---|---|---|---|
| `Money` | ✅ record | ❌ None | See 🔴 below |
| `MailboxCursor` | ✅ record | ❌ None | See 🟡 below |
| `ExchangeRate` | ✅ record | ✅ `RateToRsd > 0` | See 🟡 below |
| `FilingInfo` | ✅ sealed record | ❌ None | Positional record, no validation |
| `HolidayConf` | ✅ record | ✅ null check | Minimal |
| `SyncParameters` | ✅ sealed record | ✅ Rich | Well done |

#### 🔴 `Money` has no invariants and is never used

```csharp
public record Money(decimal Amount, string Currency);
```

1. `Currency` can be `null`, empty, or arbitrary string — no validation
2. `Amount` allows negative values — no semantic constraint
3. `Money` is defined but **never referenced anywhere** in the solution — every monetary field uses raw `decimal` + separate `string`

**Recommendation:** Either adopt `Money` across `FilingInfo`, `TaxCalculationService` parameters, etc. (and add validation: `Currency` must be a 3-letter ISO 4217 code), or delete it to remove dead code.

#### 🟡 `MailboxCursor` is not a discriminated union

The constitution states: *"`MailboxCursor` should be a discriminated union via `abstract record`."*

Currently it is a flat record with two nullable fields. There is no type-level distinction between "Never synced", "Date-based cursor", and "UID-based cursor". A proper discriminated union would make these states explicit and eliminate the `null!` default in `Mailbox.cs:15`:

```csharp
public abstract record MailboxCursor;
public sealed record NeverSynced : MailboxCursor;
public sealed record DateCursor(DateOnly LastSyncDate) : MailboxCursor;
public sealed record UidCursor(DateOnly LastSyncDate, long LastUid) : MailboxCursor;
```

*Note: this has EF Core mapping implications — the cursor is currently mapped via `OwnsOne` with flat columns.*

#### 🟡 `ExchangeRate` `init` properties bypassable with `with` expression

Because properties use `{ get; init; }`, the constructor validation can be bypassed:

```csharp
var rate = new ExchangeRate(someDate, "USD", 117m);
var broken = rate with { RateToRsd = -1m }; // No validation!
```

**Fix:** Change `{ get; init; }` to `{ get; }` to make the record truly immutable after construction. Also add a `null`/whitespace check on `Currency`.

### Entities

| Entity | Sealed? | Private ctor? | Factory? | Invariants? |
|---|---|---|---|---|
| `Filing` | ✅ | ✅ | ✅ `CreateFromIncome` | ✅ Rich |
| `Report` | ✅ | ✅ | ✅ `Create`, `CreateRevision` | ✅ Good |
| `Mailbox` | ✅ | ✅ | ✅ `Create` | ✅ Good |
| `TaxpayerProfile` | ✅ | ✅ | ❌ Public ctor | ✅ JMBG validation |
| `Importer` | ✅ | ✅ | ✅ `Create` | ✅ Good |
| `PublicHoliday` | ✅ | ✅ | ✅ `Create` | ✅ Name check |
| `HolidayYearRange` | ✅ | ✅ | ❌ Public ctor | ✅ Year validation |

---

## 1.4 Invariant Enforcement

### ✅ `Filing.AdvanceStatus` — state machine properly implemented

The `(Status, newStatus)` switch expression with a `DomainException` on invalid transitions is clean and exhaustive.

### 🔴 `Report.SetStatus` has NO transition guards

```csharp
public void SetStatus(ReportStatus status)
{
    Status = status;  // plain setter disguised as a method
}
```

Any status can transition to any other, including nonsensical transitions like `Processed → Init`. Compare to `Filing.AdvanceStatus`. The Application layer (`ProcessReportsCommandHandler.cs:87,96`) calls `SetStatus` freely, relying on caller discipline instead of domain enforcement.

### 🔴 `Filing` public constructor bypasses `CreateFromIncome` validation

```csharp
public Filing(Guid id, Guid taxpayerProfileId, DateOnly taxPeriod, FilingStatus status = FilingStatus.Init)
```

This allows creating a `Filing` with `status = FilingStatus.Paid` directly, setting none of the income fields. **Recommendation:** Make this constructor `internal` (for EF Core / test seeding) or add invariant checks.

### 🟡 `HolidayConf.Holidays` mutability leak

`IReadOnlyList<T>` is a read-only *interface*, but the underlying collection can be mutated by the caller after construction. **Fix:** Defensively copy: `Holidays = holidays?.ToArray() ?? throw ...;`

### 🟢 `Importer.UpdateDetails` doesn't trim `displayName`

`Importer.Create` trims `displayName`, but `UpdateDetails` does not — inconsistency that can introduce whitespace differences.

---

## 1.5 Exception Handling

### ✅ Consistently uses `DomainException`

All domain invariant violations correctly throw `DomainException`. No `ArgumentException` or `InvalidOperationException` used for domain rules.

### 🟡 Mixed use of `DomainException` and `ArgumentNullException`

`Report.CreateRevision` line 57 and `Mailbox` constructor line 28 use `ArgumentNullException.ThrowIfNull`, while the rest of the codebase uses `DomainException` for null checks. Pick one convention.

### 🟢 `DomainException` could carry a domain error code

A string-only exception makes programmatic handling harder. Consider:

```csharp
public sealed class DomainException : Exception
{
    public string Code { get; }
    public DomainException(string code, string message) : base(message) { Code = code; }
}
```

---

## 1.6 Code Quality

### 🟡 `FilingStatus` is in `Filing.cs`, not in `Enums/`

All other enums live in `Enums/`. `FilingStatus` is co-located with the `Filing` entity, forcing `using Rentier.Domain.Entities` in DTOs just to use an enum. Move to `Enums/FilingStatus.cs`.

### 🟡 `TaxCalculationService` is async in the domain layer

The domain service accepts a `Func<DateOnly, string, Task<ExchangeRate>>` delegate, making it async. Domain services should be pure and I/O-free. Consider moving rate resolution to an Application-layer orchestrator, and making `TaxCalculationService` accept pre-resolved `ExchangeRate` values (making it synchronous and pure).

### 🟡 `TaxCalculationService` fetches exchange rate twice when currencies match

When `whtAmount > 0` and `upperWht == upperIncome`, the same `(date, currency)` rate is fetched twice. **Fix:**
```csharp
var whtRate = (upperWht == upperIncome) ? incomeRate : await rateProvider(incomeDate, upperWht);
```

### 🟢 `BusinessDayResolver.FindPreviousBusinessDay` silently returns input on failure

If no business day is found within 10 days, it silently returns the non-business-day input. A `DomainException` would be safer.

---

## 1.7 Domain Layer — Top Priority Actions

1. 🔴 **`Report.SetStatus`** — add state machine guards (same pattern as `Filing.AdvanceStatus`)
2. 🔴 **`Money`** — either adopt it across the domain or delete it (dead code)
3. 🔴 **`Filing` public constructor** — restrict to `internal` or add validation
4. 🟡 **`ExchangeRate`** — change `init` to `get` to prevent `with`-expression bypass
5. 🟡 **Inject clock** — accept `DateOnly today` in `Report.Create`, `Mailbox.Create` for testability
6. 🟡 **`TaxCalculationService`** — make pure by accepting pre-resolved rates

---

---

# Layer 2 — Application (`Rentier.Application`)

**Scope:** 80+ source files · 18 commands · 7 queries · 25 handlers  
**TFM:** net8.0 | **C#:** 12 | **Nullable:** enabled | **TreatWarningsAsErrors:** true  
**NuGet:** Only `Microsoft.Extensions.Logging.Abstractions` ✅

---

## 2.1 Dependency Direction

| Check | Result |
|-------|--------|
| `csproj` references only Domain | ✅ |
| No Infrastructure imports | ✅ Zero matches for `using Rentier.Infrastructure` |
| No Desktop imports | ✅ |
| No EF Core references | ✅ |

---

## 2.2 CQRS Correctness

Every command and query has a corresponding handler. No orphaned commands or queries.

### 🟡 `ISyncAllCommandHandler` breaks the uniform handler pattern

`ISyncAllCommandHandler` adds an `IProgress<SyncProgressEntry>` parameter to `HandleAsync`, making it non-generic. Consider supplying the progress callback via constructor injection rather than breaking the handler contract.

### 🟡 `IProgress<T>` embedded in `SyncMailboxCommand` record

```csharp
public sealed record SyncMailboxCommand(
    SyncParameters Parameters,
    IProgress<SyncProgress>? Progress = null);
```

Commands should be pure data (serializable intent). `IProgress<T>` is a behavioral callback that breaks serialization/replay scenarios. Remove from the command and inject into the handler or pass as a separate parameter.

---

## 2.3 Exception Handling

### 🔴 Broad `catch (Exception)` swallows `OperationCanceledException`

Seven `catch (Exception ex)` blocks across four handlers:

| File | Problem |
|------|---------|
| `DeleteReportCommandHandler.cs` line 40 | Catches **all** exceptions including `OperationCanceledException` |
| `GetDashboardQueryHandler.cs` line 54 | Catches all exceptions in a **read-only query** |
| `GetReportsQueryHandler.cs` line 47 | Silently converts any crash to `Result.Failure` |
| `ImportReportCommandHandler.cs` line 62 | Wraps everything including cancellation |
| `ProcessReportsCommandHandler.cs` lines 94, 167, 208 | Mixed — inner loops ok; outer loop catches cancellation |

**Fix at minimum:**
```csharp
catch (OperationCanceledException) { throw; }
catch (Exception ex) { /* handle */ }
```
Better: remove broad catches from pure query handlers entirely.

### 🔴 Pipe-delimited exception channel in `ProcessReportsCommandHandler`

`BuildRateProvider` (line 242) throws to encode business errors:
```csharp
throw new InvalidOperationException($"{result.Error.Code}|{result.Error.Message}");
```
Then `ParseErrorFromException` (line 246) manually parses the pipe-delimited message back out. **This is fragile string tunneling through exception messages** — contradicts the Result pattern used everywhere else. Make `BuildRateProvider` return `Result<RateResolution, Error>` instead.

---

## 2.4 Result Pattern

### 🟡 Error codes are inconsistent

| Handler | Error Codes Used |
|---------|-----------------|
| `AddImporterCommandHandler` | `INVALID_REGEX`, `DOMAIN_ERROR` |
| `AddMailboxCommandHandler` | `DOMAIN_VALIDATION` |
| `UpdateMailboxCommandHandler` | `NOT_FOUND`, `DOMAIN_VALIDATION` |
| `UpdateImporterCommandHandler` | `IMPORTER_NOT_FOUND`, `INVALID_REGEX`, `DOMAIN_ERROR` |

`NOT_FOUND` vs `IMPORTER_NOT_FOUND` vs `Error.NotFound(...)` — three patterns for the same concept. **Recommendation:** Define error codes as `const` fields in `Error.cs` or a dedicated `ErrorCodes` static class and use factory methods consistently.

### 🟢 Verbose `Result` construction

Add `Map`, `Bind`, and implicit conversion operators to reduce ceremony:
```csharp
public static implicit operator Result<TValue, TError>(TValue value) => Success(value);
```

---

## 2.5 Testability Issues

### 🟡 `DateTime.Today` is not injectable

- `GetDashboardQueryHandler.cs` line 27: `DateOnly.FromDateTime(DateTime.Today)`
- `GetHolidayConfQueryHandler.cs` line 26: `DateOnly.FromDateTime(DateTime.Today).Year`

**Fix:** Inject `TimeProvider` (built-in in .NET 8):
```csharp
public GetDashboardQueryHandler(IFilingRepository filings, TimeProvider time)
```

### 🟡 `ExchangeRateResolver` is a concrete dependency

`ProcessReportsCommandHandler` depends on `ExchangeRateResolver` (a concrete `sealed class`), not an interface. It cannot be mocked in unit tests. **Fix:** Extract `IExchangeRateResolver`.

---

## 2.6 Design Issues

### 🟡 `GetHolidayConfQueryHandler` (a **query**) writes seed data

```csharp
if (yearRange is null)
    await _repository.SaveHolidaysAsync(seededHolidays, seedRange, ct);
```

**Violates CQRS** — a query handler must not mutate state. Move the seed logic to application startup or a dedicated `EnsureHolidaysSeededCommand`.

### 🟡 N+1 query in `GetReportsQueryHandler`

```csharp
foreach (var r in reports)
{
    var count = await _filings.GetFilingCountByReportIdAsync(r.Id, ct);
```

For N reports, this makes N+1 database roundtrips. **Fix:** Add a batch method:
```csharp
Task<IReadOnlyDictionary<Guid, int>> GetFilingCountsByReportIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);
```

### 🟡 `DeleteMailboxCommandHandler` discards credential deletion result

```csharp
await _credentials.DeleteCredentialAsync(CredentialKeys.MailboxPassword(command.Id), ct);
// result discarded — what if this fails?
```

### 🟡 `UpdateMailboxCommandHandler` — wrong order of operations

The handler updates the mailbox in the DB *before* saving the new credential. If credential save fails, the DB has new connection details but no matching password.

### 🟡 `FilingStatus` in entity namespace leaks into DTOs

`FilingRowDto`, `UpcomingDeadlineDto`, `OverdueFilingDto` all need `using Rentier.Domain.Entities` just for a `FilingStatus` enum. Fix: move `FilingStatus` to `Rentier.Domain.Enums` (tracked in Domain findings).

### 🟡 `IHolidayRepository` in `Interfaces/` instead of `Repositories/`

All other repository interfaces are in `Repositories/`. Move `IHolidayRepository` there for consistency.

### 🟢 Regex validation duplicated between `AddImporterCommandHandler` and `UpdateImporterCommandHandler`

Extract to a shared static helper: `ImporterValidation.ValidateRegex(string pattern)`.

---

## 2.7 Application Layer — Top Priority Actions

1. 🔴 **Fix `catch (Exception)`** — add `catch (OperationCanceledException) { throw; }` before every broad catch; remove catches from query handlers
2. 🔴 **Eliminate pipe-delimited exception channel** — make `BuildRateProvider` return `Result<RateResolution, Error>`
3. 🟡 **Inject `TimeProvider`** — replace `DateTime.Today` in query handlers
4. 🟡 **Move state-seeding out of `GetHolidayConfQueryHandler`**
5. 🟡 **Centralize error codes** — define as constants and use `Error` factory methods consistently

---

---

# Layer 3 — Infrastructure (`Rentier.Infrastructure`)

**Scope:** 50+ source files  
**TFM:** net8.0 | **C#:** 12 | **Nullable:** enabled | **TreatWarningsAsErrors:** true  
**Build:** ✅ 0 warnings, 0 errors

---

## 3.1 DI Registration

### 🟡 `async` extension method — non-standard pattern

`AddInfrastructureServicesAsync` returns `Task`, forcing callers to `await` during startup. `IServiceCollection` extension methods in .NET are universally synchronous. Consider deferring async work (D-Bus credential store connection) to first-access via a `Lazy<Task<ICredentialStore>>` factory.

### 🟡 `ProviderInfo` silently discarded

The credential store provider info is created but never logged or registered in DI. In production, operators cannot know which credential store backend was selected.

### 🟢 Application-layer handlers registered in Infrastructure

Two handlers (`SyncMailboxCommandHandler`, `ProcessReportsCommandHandler`) are registered in `InfrastructureServiceExtensions`. They are Application-layer concerns and should be registered in a dedicated Application extension method or the composition root.

---

## 3.2 EF Core / Persistence

### 🔴 `GetFilingStatsAsync` materializes all filings into memory

```csharp
var filings = await _db.Filings.AsNoTracking().ToListAsync(ct);
var initCount = filings.Count(f => f.Status == FilingStatus.Init);
```

For users with hundreds of filings, this is an unbounded in-memory load. **Fix:** Use server-side aggregation:
```csharp
var stats = await _db.Filings.AsNoTracking()
    .GroupBy(f => f.Status)
    .Select(g => new { Status = g.Key, Count = g.Count(), Unpaid = g.Sum(f => f.TaxPayableRsd) })
    .ToListAsync(ct);
```

### 🟡 Missing explicit enum conversions in `FilingConfiguration`

`Filing.IncomeType` and `Filing.Status` have no explicit `.HasConversion<int>()` in `FilingConfiguration`, unlike `ReportConfiguration` and `ImporterConfiguration` which configure theirs explicitly. Inconsistent — add explicit conversions and a default value for `FilingStatus`.

### 🟡 Repeated detach-then-update boilerplate in every `UpdateAsync`

The same ~5-line detach pattern appears in `TaxpayerProfileRepository`, `ReportRepository`, `MailboxRepository`, `ImporterRepository`, and `FilingRepository`. Extract a `DetachStale<T>(Guid id)` helper method.

### 🟡 `ImporterRepository.GetAllAsync` missing `.AsReadOnly()`

All sibling repositories call `.AsReadOnly()` to prevent callers from casting back to `List<T>`. `ImporterRepository` doesn't. Trivial fix.

### 🟡 `ExchangeRateCacheRepository.SaveBatchAsync` — N+1 `FindAsync` calls

15 `FindAsync` round-trips for a typical NBS response. **Fix:** Fetch all existing rates for the date in a single query first, then upsert from a dictionary.

### ℹ️ Migration numbering conflict

Two migrations share the `0010` sequence number:
- `20260408180904_0010_SyncReplayControls`
- `20260717000001_0010_FilingRateProvenance`

The second should be renamed `0011`.

---

## 3.3 Security

### 🔴 macOS `security` CLI — shell injection vulnerability

```csharp
var (exitCode, _, stderr) = RunSecurity(
    $"add-generic-password -a \"{AccountName}\" -s \"{key}\" -w \"{secret}\" -U");
```

`secret` is the user-provided password, interpolated directly into a shell command string. **Fix:** Use `ProcessStartInfo.ArgumentList` (auto-escapes arguments) instead of building `Arguments` as a string.

### 🟡 `RunSecurity` — potential deadlock from sequential stream reads

```csharp
var stdout = process.StandardOutput.ReadToEnd();   // blocks
var stderr = process.StandardError.ReadToEnd();    // may deadlock if stderr buffer fills
process.WaitForExit();
```

.NET docs explicitly warn: reading stdout then stderr sequentially can deadlock. **Fix:** Read both streams concurrently with async tasks.

### 🟡 `WindowsCredentialStore.GetCredentialAsync` uses wrong error code

When `CredReadW` fails for non-`ERROR_NOT_FOUND` reasons, the error is reported as `CredentialWriteFailed`. Should be `CredentialReadFailed`.

### 🟡 Sensitive data not zeroed in Windows credential store

```csharp
byte[] blob = Encoding.UTF8.GetBytes(secret);
IntPtr ptr = Marshal.AllocHGlobal(blob.Length);
```

The managed `blob` array is not zeroed after use. For a credential store, zero it in `finally`:
```csharp
finally { Array.Clear(blob); Marshal.FreeHGlobal(ptr); }
```

### 🟢 Linux `LinuxCredentialStore` doesn't pass `CancellationToken` to D-Bus calls

All three methods accept `CancellationToken` but never forward it to the Secret Service API.

---

## 3.4 Exchange Rates

### ✅ Excellent NBS ASMX integration

- Correct date format with InvariantCulture commentary
- `XDocument` + `LocalName` to avoid namespace issues
- Defensive `decimal.TryParse` with InvariantCulture
- Batch caching of all rates per date
- `CompositeExchangeRateFetcher` fallback logic well-designed

### 🟡 Silent bare `catch { }` in cache writes

```csharp
try { await _cache.SaveBatchAsync(allRates, ct); } catch { /* non-fatal */ }
```

This swallows even `OutOfMemoryException`. **Fix:** Filter to expected types and log:
```csharp
catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
{
    _logger.LogWarning(ex, "Cache write failed — non-fatal");
}
```

### 🟡 `CompositeExchangeRateFetcher` injects concrete types, not interfaces

```csharp
public CompositeExchangeRateFetcher(NbsExchangeRateFetcher primary, NbsWebScraper secondary)
```

Both implement `IExchangeRateFetcher`. Inject by interface for testability. Use keyed services or a factory pattern.

---

## 3.5 Parsing

### 🟢 `StripIsin` method is fragile

```csharp
internal static string StripIsin(string description) =>
    description.Split('(')[0].Trim();
```

Fails for entity names containing parentheses (e.g., `"COMPANY (UK) LTD (GB1234567890)"`). A regex matching the ISIN pattern `\([A-Z]{2}[A-Z0-9]{9}\d\)` would be more precise.

---

## 3.6 IMAP Sync

### 🟡 Regex recompiled on every attachment

```csharp
if (!Regex.IsMatch(filename, importer.AttachmentRegex))
```

`Regex.IsMatch` with a string pattern recompiles the regex on every call. For a mailbox with many messages, precompile outside the inner loop using `new Regex(importer.AttachmentRegex, RegexOptions.Compiled)`.

### 🟡 Cursor advances even on partial failure

After processing all importers, the cursor always advances to `DateTime.UtcNow`, even when some importers failed. Failed messages won't be re-fetched on the next incremental sync.

### 🟢 `BuildReportName` is defined but never called

An internal method that duplicates inline code at lines 112–115. Either use it or remove it.

---

## 3.7 Cross-Cutting

### 🟡 No `ConfigureAwait(false)` in any infrastructure `await`

Throughout all async Infrastructure code, `await` is used without `.ConfigureAwait(false)`. In an Avalonia application, the UI has a synchronization context. Infrastructure/library code should use `ConfigureAwait(false)` to avoid unnecessary context captures. This is a **systematic gap** affecting every file in the layer.

---

## 3.8 Infrastructure Layer — Top Priority Actions

1. 🔴 **`GetFilingStatsAsync`** — use server-side `GroupBy` aggregation
2. 🔴 **macOS shell injection** — use `ProcessStartInfo.ArgumentList`
3. 🟡 **Add `ConfigureAwait(false)`** to all `await` calls in Infrastructure
4. 🟡 **Fix `RunSecurity` deadlock risk** — read stdout/stderr in parallel
5. 🟡 **Replace bare `catch { }`** with filtered exception catches + logging

---

---

# Layer 4 — Desktop (`Rentier.Desktop`)

**Scope:** Avalonia 11 + ReactiveUI MVVM — all ViewModels, Views, Dialogs, Composition Root

---

## 4.1 Architecture & Dependencies

### 🔴 Direct Infrastructure reference in `App.axaml.cs`

```csharp
using Rentier.Infrastructure;
using Rentier.Infrastructure.Persistence;
```

`App.axaml.cs` directly references `AppDbContext` to run EF migrations. While the composition root is the appropriate place for this coupling, the migration call should be extracted into an Infrastructure extension method (e.g., `provider.MigrateAsync()`) to keep `App.axaml.cs` free of Infrastructure type knowledge.

### 🟡 `Microsoft.EntityFrameworkCore.Design` in Desktop project

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.*">
```

This design-time package belongs in the Infrastructure project only.

### 🟡 `CommunityToolkit.Mvvm` package unused

No source file uses `CommunityToolkit.Mvvm` — all ViewModels use `ReactiveObject`. Remove this dead dependency.

---

## 4.2 ReactiveUI Patterns

### 🔴 `ProfileSettingsViewModel` never loads its data

Unlike every other settings ViewModel (which implement `IActivatableViewModel` and auto-load via `WhenActivated`), `ProfileSettingsViewModel`:
- Does **not** implement `IActivatableViewModel`
- Does **not** have a `WhenActivated` block
- Has a `public async Task LoadAsync()` that is **never called from anywhere**

**Impact:** The profile form will always appear empty when navigated to, even if a profile was previously saved.

**Fix:** Add `IActivatableViewModel`, `ViewModelActivator`, and a `WhenActivated` block that triggers `LoadAsync`.

### 🔴 Missing `ThrownExceptions` subscriptions on most commands

In ReactiveUI, unhandled exceptions from `ReactiveCommand` route to `ThrownExceptions`. If nothing subscribes, the exception hits `RxApp.DefaultExceptionHandler` which by default **terminates the app**.

| ViewModel | Commands **without** `ThrownExceptions` |
|---|---|
| `FilingsViewModel` | `LoadPageCommand`, `AdvanceStatusCommand`, `SavePaymentRefCommand`, `DeleteCommand` |
| `ReportsViewModel` | All commands |
| `HolidaySettingsViewModel` | `SaveCommand`, `ImportCommand` |
| `MailboxSettingsViewModel` | All commands |
| `ImporterSettingsViewModel` | All commands |
| `SyncViewModel` | Swallowed silently |

**Fix:** Either subscribe to `ThrownExceptions` per command, or set a global `RxApp.DefaultExceptionHandler` that logs and shows an error toast.

### 🟡 Navigation logic split between ViewModel and code-behind

`MainWindow.axaml.cs` contains:
```csharp
this.WhenAnyValue(x => x.ViewModel!.SelectedEntry)
    .Subscribe(entry => { ViewModel.CurrentViewModel = entry.ViewModel; });
```

This is ViewModel logic that belongs in `MainWindowViewModel`. Move it there, leaving code-behind clean.

### 🟡 Fire-and-forget `Subscribe()` without disposal

`FilingsViewModel.cs` lines 59 and 71:
```csharp
LoadPageCommand.Execute().Subscribe();
```
And `FilingsView.axaml.cs` lines 26, 34, 42, 50:
```csharp
ViewModel?.AdvanceStatusCommand.Execute((row.Id, newStatus)).Subscribe();
```
No error handling if the command throws. If `ThrownExceptions` is not subscribed elsewhere, exceptions are silently swallowed.

---

## 4.3 Threading & Safety

### 🟡 `async void` in `App.OnFrameworkInitializationCompleted`

```csharp
public override async void OnFrameworkInitializationCompleted()
```

If startup throws, the exception will be unobserved and crash the app without a user-friendly error message. Wrap the body in try/catch with error logging or a dialog.

### 🟡 `SyncViewModel.ThrownExceptions` silently swallowed

```csharp
SyncCommand.ThrownExceptions
    .Subscribe(_ => { })  // completely silent
    .DisposeWith(disposables);
```

At minimum, log the exception.

---

## 4.4 Bugs

### 🟡 `DashboardView` overdue section always visible

```xml
IsVisible="{Binding OverdueFilings.Count, Converter={x:Static ObjectConverters.IsNotNull}}"
```

`OverdueFilings.Count` is an `int` (boxed) — `IsNotNull` is always true for a boxed value type. The overdue section will always be visible even when empty. **Fix:** Use a `> 0` comparison or a proper converter.

### 🟡 `PaymentRef_LostFocus` fires without change detection

Every time the TextBox loses focus, `SavePaymentRefCommand` is executed — even if the text hasn't changed. Adds unnecessary calls.

### 🟡 `MailboxSettingsViewModel.OnSaveAsync` rebuilds from stale DTO

After `ReloadAsync` line 168, `SelectedMailbox` is already `null` because `Mailboxes.Clear()` triggers ListBox deselection. The `UpdateFrom` call on line 170 is a null-conditional no-op operating on a stale reference.

---

## 4.5 XAML Quality

### 🟡 `x:CompileBindings="False"` on most views

Despite `AvaloniaUseCompiledBindingsByDefault=true` in the csproj, almost every view explicitly disables compiled bindings — forfeiting compile-time binding validation and performance. Enable incrementally per view.

### 🟡 Hardcoded strings not in `Strings.resx`

Key locations with missing resource strings:
- `SyncViewModel.cs` — all 9 impact summary strings
- `DashboardViewModel.cs` line 141 — `"Never"` (resource `Dashboard_LastSyncNever` exists but is not used!)
- `ImportDialogHelper.cs` — `"OK"`, `"Import"`, `"Cancel"`, `"Select importer:"`
- `HolidaySettingsView.axaml` — label strings
- `ReportTypeExtensions.cs` line 9 — `"IBKR CSV"`

---

## 4.6 Desktop Layer — Top Priority Actions

1. 🔴 **`ProfileSettingsViewModel`** — add `IActivatableViewModel` + `WhenActivated`
2. 🔴 **Global `RxApp.DefaultExceptionHandler`** or `ThrownExceptions` on all commands
3. 🟡 **Wrap `App.OnFrameworkInitializationCompleted` in try/catch**
4. 🟡 **Fix overdue section `IsVisible` binding** — always visible due to `IsNotNull` on boxed int
5. 🟡 **Move hardcoded strings to `Strings.resx`** (especially `SyncViewModel` impact summaries)

---

---

# Layer 5 — Tests

**Scope:** 5 test projects · 70+ test files · 350+ test methods  
**Stack:** xUnit + FluentAssertions + NSubstitute  
**Overall Grade: A-**

---

## 5.1 Project Structure

### 🟡 `NSubstitute` referenced in `Rentier.Domain.Tests` but unused

```xml
<!-- Remove from Rentier.Domain.Tests.csproj -->
<PackageReference Include="NSubstitute" Version="5.*" />
```

Domain tests are pure logic — no mocks. The unused reference contradicts that principle at the project level.

### 🟡 Floating version ranges (`*`) in all package references

All test projects use `Version="2.*"`, `Version="5.*"`, etc. Consider pinning to specific minor versions to prevent unexpected breakage.

### 🟢 `Rentier.Tests.Common` references unused test packages

This shared library only contains `FakeCredentialStore` but references xUnit, FluentAssertions, and NSubstitute without using any of them. Trim to just `Rentier.Application` project reference.

---

## 5.2 Test Naming Convention

The vast majority of tests follow `MethodName_StateUnderTest_ExpectedBehavior` correctly. Violations:

| File | Issue |
|------|-------|
| `HolidayYearRangeTests.cs` | Tests lack the method prefix (e.g., `ValidRange_NoThrow` → `Constructor_ValidRange_DoesNotThrow`) |
| `SaveHolidayConfCommandHandlerTests.cs` | Missing `HandleAsync_` prefix on all tests |
| `ImportHolidaysFromWebCommandHandlerTests.cs` | Missing `HandleAsync_` prefix |
| `GetHolidayConfQueryHandlerTests.cs` | Missing `HandleAsync_` prefix |
| `HolidayRepositoryTests.cs` | Uses old-style prefix, missing `Async` suffix |

---

## 5.3 Domain Tests ✅

All 13 domain test files are pure logic with zero mocking. Highlights:
- `FilingCreateFromIncomeTests` — 14 tests covering all validation paths
- `FilingStatusTransitionTests` — all valid + invalid state transitions
- `TaxCalculationServiceTests` — WHT clamping, rounding, rate provider call count

### 🟡 `static readonly Guid` fields are shared within test class

```csharp
private static readonly Guid ProfileId = Guid.NewGuid();
```

`static` means the value is shared across all tests in the class. Prefer `private readonly` (non-static) for clarity, or use a fixed GUID for deterministic assertions.

### 🟡 `FilingStatusTransitionTests` not using `[Theory]`

The 5 individual transition tests could be a single `[Theory]` with `[InlineData]` covering all valid/invalid transitions — easier to extend and audit for coverage.

### 🟢 `ReportTests.Create_SetsImportDateToToday` is fragile near midnight

The before/after `DateTime.UtcNow` sandwich will fail if the test crosses midnight UTC. This is a known minor risk from the lack of `IClock` injection in the domain.

---

## 5.4 Application Tests ✅

All 25+ handler test files follow consistent arrange/act/assert. Highlights:
- `ProcessReportsCommandHandlerTests` — 10 + 5 partial-success tests; exceptional coverage
- `DeleteReportCommandHandlerTests` — verifies call ordering with `callOrder.Should().Equal("filings", "report")`

### 🟡 `DiRegistrationSmokeTests` tests mock registration, not real DI

These tests register NSubstitute mocks and then resolve them. They don't test the real `services.AddApplication()` registration. Consider adding an integration test that exercises the actual extension method.

### 🟡 `SyncAllCommandHandlerTests` uses `Task.Delay(50)` for progress callbacks

`Progress<T>` posts callbacks to `SynchronizationContext`, making 50ms timing non-deterministic. This is a **flaky test** waiting to happen. Use synchronous `IProgress<T>` capture instead.

---

## 5.5 Infrastructure Tests

### 🔴 Most infrastructure tests missing `[Trait("Category", "Integration")]`

Per the testing standards, infrastructure tests must be marked so they can be excluded from fast unit test runs via `--filter "Category!=Integration"`. Currently only `ImapSyncIntegrationTests` and `NbsIntegrationTests` have this trait. All repository tests, parser tests, serializer tests, and security tests are **missing it**.

### ✅ `FakeHttpMessageHandler` is clean and focused

Hand-rolled, no Moq, tracks `CallCount`. Correct pattern.

### ✅ SQLite in-memory lifecycle is correct

All repository tests use `SqliteConnection("Data Source=:memory:")` + `IAsyncLifetime` with proper `await using DisposeAsync`. 

### 🟡 `NbsWebScraperTests` duplicates `FakeHttpMessageHandler`

Inline `FakeHandler` class duplicates the fake in `NbsExchangeRateFetcherTests`. Move to `Rentier.Tests.Common` for reuse.

### 🟡 Security tests use `if (!RuntimeInformation.IsOSPlatform(...)) return;` for wrong-platform skip

This causes tests to silently **pass** on the wrong platform. Use `[Fact(Skip = "Requires Windows")]` or `SkippableFact` to make skips visible in test output.

---

## 5.6 Desktop/ViewModel Tests ✅

All ViewModel tests correctly use `ImmediateScheduler.Instance`, `vm.Activator.Activate()` with `using`, and verify both `CanExecute` observables and command execution results.

### 🟡 Reflection used to set `IsLoading` in `HolidaySettingsViewModelTests`

```csharp
typeof(HolidaySettingsViewModel)
    .GetProperty(nameof(HolidaySettingsViewModel.IsLoading), ...)!
    .SetValue(vm, value);
```

Brittle — breaks silently at runtime if `IsLoading` becomes read-only. Test `CanExecute` indirectly by triggering a long-running command instead.

### 🟡 `SyncViewModelTests` uses multiple `Task.Delay(50)` for timing

Same flaky test risk as `SyncAllCommandHandlerTests`.

---

## 5.7 Missing Tests & Coverage Gaps

| Gap | Severity |
|-----|---------|
| No concurrency tests for `ExchangeRateCacheRepository.SaveBatchAsync` | 🔴 |
| `SyncMailboxCommandHandlerTests.MakeMailbox` ignores its `id` parameter | 🟡 |
| No negative-port test for `Mailbox.Create` | 🟡 |
| `TaxpayerProfileTests` — no test for `null` JMBG | 🟡 |
| No `CancellationToken` propagation tests for `ProcessReportsCommandHandler` loops | 🟡 |
| `ExportFilingCommandHandlerTests` — no test for `WithReportId` importer lookup path | 🟢 |
| `GetTaxpayerProfileQueryHandlerTests` — only 2 tests | 🟢 |
| No cross-year deadline test in `FilingDeadlineCalculatorTests` | 🟢 |
| `ImapSyncIntegrationTests` are placeholder stubs (`[Fact(Skip = ...)]`) | 🟢 |

---

## 5.8 Tests Layer — Top Priority Actions

1. 🔴 **Add `[Trait("Category", "Integration")]`** to all infrastructure tests (repository, parser, serializer, security)
2. 🟡 **Fix naming** in `HolidayYearRangeTests`, `SaveHolidayConfCommandHandlerTests`, etc.
3. 🟡 **Remove `NSubstitute`** from `Rentier.Domain.Tests.csproj`
4. 🟡 **Replace `Task.Delay(50)` patterns** with synchronous progress capture
5. 🟢 **Create shared `TestData` builder** in `Rentier.Tests.Common` for `Filing`, `Report`, `TaxpayerProfile`, `Importer`

---

---

# Master Priority List — All Layers

## 🔴 Critical Issues (11 total) — Fix Now

| # | Layer | Issue | File(s) |
|---|-------|-------|---------|
| 1 | Domain | `Money` value object unused and has no invariants | `ValueObjects/Money.cs` |
| 2 | Domain | `Report.SetStatus` has no state machine guards | `Entities/Report.cs` |
| 3 | Domain | `Filing` public ctor bypasses `CreateFromIncome` invariants | `Entities/Filing.cs` |
| 4 | Application | `catch (Exception)` swallows `OperationCanceledException` in 4 handlers | Multiple handlers |
| 5 | Application | Pipe-delimited exception channel in `ProcessReportsCommandHandler` | `Handlers/ProcessReportsCommandHandler.cs` |
| 6 | Infrastructure | `GetFilingStatsAsync` materializes all filings into memory | `Repositories/FilingRepository.cs` |
| 7 | Infrastructure | macOS `security` CLI shell injection vulnerability | `Security/MacOsCredentialStore.cs` |
| 8 | Desktop | `ProfileSettingsViewModel` never loads its data | `ViewModels/ProfileSettingsViewModel.cs` |
| 9 | Desktop | Missing `ThrownExceptions` subscriptions — unhandled exceptions crash app | Multiple ViewModels |
| 10 | Tests | Most infrastructure tests missing `[Trait("Category", "Integration")]` | All infra test files |
| 11 | Tests | No concurrency tests for `ExchangeRateCacheRepository.SaveBatchAsync` | Infrastructure.Tests |

## 🟡 Warnings — High Value Fixes

| Priority | Layer | Issue |
|----------|-------|-------|
| High | Infrastructure | `ConfigureAwait(false)` missing on all `await` calls in Infrastructure |
| High | Infrastructure | `RunSecurity` stdout/stderr deadlock risk on macOS |
| High | Domain | `DateTime.UtcNow` used in domain entities (testability) |
| High | Domain | `ExchangeRate` `init` props bypassable via `with` expression |
| High | Application | `DateTime.Today` not injectable in two query handlers |
| High | Application | `GetHolidayConfQueryHandler` (query) mutates state |
| High | Application | N+1 query in `GetReportsQueryHandler` |
| High | Desktop | Navigation logic in code-behind instead of ViewModel |
| High | Desktop | Overdue section `IsVisible` always true (boxed int with `IsNotNull`) |
| High | Desktop | `async void OnFrameworkInitializationCompleted` has no error handling |
| Medium | Infrastructure | Silent `catch { }` in NBS cache writes |
| Medium | Infrastructure | Cursor advances on partial IMAP sync failure |
| Medium | Application | Error codes inconsistent across handlers |
| Medium | Application | `IProgress<T>` embedded in `SyncMailboxCommand` record |
| Medium | Tests | `Task.Delay(50)` flaky timing in sync/progress tests |
| Medium | Tests | Security tests silently pass on wrong platform |

---

*Review generated from parallel analysis of all source files across all 5 layers.*
