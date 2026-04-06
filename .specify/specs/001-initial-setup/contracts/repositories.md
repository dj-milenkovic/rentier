# Contract: Repository Interfaces

**Feature**: `001-initial-setup`  
**Layer**: `Rentier.Application/Repositories/`  
**Date**: 2026-04-06

---

## Overview

All six repository interfaces are declared in `Rentier.Application`. They are empty stubs in this
feature — method signatures document the intended contract; implementations are deferred to the
persistence feature for each entity. `Rentier.Infrastructure` provides concrete EF Core
implementations. `Rentier.Domain` has no knowledge of these interfaces.

All method signatures:
- Are `async Task<T>` (constitution Principle IV — no `.Result`/`.Wait()`).
- Accept `CancellationToken ct = default` as the final parameter.
- Return `Task<T>` where `T` is a domain type, a collection of domain types, or `bool`/`void`.

---

## IFilingRepository

**File**: `Rentier.Application/Repositories/IFilingRepository.cs`

**Purpose**: Persistence contract for `Filing` aggregate roots.

**Methods**:

| Signature | Description |
|-----------|-------------|
| `Task<Filing?> GetByIdAsync(Guid id, CancellationToken ct = default)` | Retrieve a single `Filing` by its surrogate ID. Returns `null` if not found. |
| `Task<IReadOnlyList<Filing>> GetAllAsync(CancellationToken ct = default)` | Retrieve all `Filing` records for the current taxpayer profile. |
| `Task<Filing?> GetByTaxPeriodAsync(Guid taxpayerProfileId, DateOnly taxPeriod, CancellationToken ct = default)` | Retrieve a `Filing` for a specific taxpayer and tax period. Returns `null` if not found. |
| `Task AddAsync(Filing filing, CancellationToken ct = default)` | Persist a newly created `Filing`. |
| `Task UpdateAsync(Filing filing, CancellationToken ct = default)` | Persist changes to an existing `Filing` (e.g., status transition). |
| `Task DeleteAsync(Guid id, CancellationToken ct = default)` | Remove a `Filing` by ID. |

---

## IReportRepository

**File**: `Rentier.Application/Repositories/IReportRepository.cs`

**Purpose**: Persistence contract for `Report` entities (parsed activity statements).

**Methods**:

| Signature | Description |
|-----------|-------------|
| `Task<Report?> GetByIdAsync(Guid id, CancellationToken ct = default)` | Retrieve a `Report` by ID. Returns `null` if not found. |
| `Task<IReadOnlyList<Report>> GetAllAsync(CancellationToken ct = default)` | Retrieve all `Report` records. |
| `Task<IReadOnlyList<Report>> GetByImporterAsync(Guid importerId, CancellationToken ct = default)` | Retrieve all `Report` records produced by a specific `Importer`. |
| `Task AddAsync(Report report, CancellationToken ct = default)` | Persist a newly imported `Report`. |
| `Task DeleteAsync(Guid id, CancellationToken ct = default)` | Remove a `Report` by ID. |

---

## IMailboxRepository

**File**: `Rentier.Application/Repositories/IMailboxRepository.cs`

**Purpose**: Persistence contract for `Mailbox` entities (IMAP connection configurations and sync
cursors).

**Methods**:

| Signature | Description |
|-----------|-------------|
| `Task<Mailbox?> GetByIdAsync(Guid id, CancellationToken ct = default)` | Retrieve a `Mailbox` configuration by ID. Returns `null` if not found. |
| `Task<IReadOnlyList<Mailbox>> GetAllAsync(CancellationToken ct = default)` | Retrieve all configured `Mailbox` records. |
| `Task AddAsync(Mailbox mailbox, CancellationToken ct = default)` | Persist a new `Mailbox` configuration. |
| `Task UpdateAsync(Mailbox mailbox, CancellationToken ct = default)` | Persist changes to an existing `Mailbox` (e.g., cursor advancement after sync). |
| `Task DeleteAsync(Guid id, CancellationToken ct = default)` | Remove a `Mailbox` configuration by ID. |

---

## IImporterRepository

**File**: `Rentier.Application/Repositories/IImporterRepository.cs`

**Purpose**: Persistence contract for `Importer` entities (IBKR CSV import filter configurations).

**Methods**:

| Signature | Description |
|-----------|-------------|
| `Task<Importer?> GetByIdAsync(Guid id, CancellationToken ct = default)` | Retrieve an `Importer` configuration by ID. Returns `null` if not found. |
| `Task<IReadOnlyList<Importer>> GetAllAsync(CancellationToken ct = default)` | Retrieve all `Importer` configurations. |
| `Task AddAsync(Importer importer, CancellationToken ct = default)` | Persist a new `Importer` configuration. |
| `Task UpdateAsync(Importer importer, CancellationToken ct = default)` | Persist changes to an existing `Importer` configuration. |
| `Task DeleteAsync(Guid id, CancellationToken ct = default)` | Remove an `Importer` configuration by ID. |

---

## ITaxpayerProfileRepository

**File**: `Rentier.Application/Repositories/ITaxpayerProfileRepository.cs`

**Purpose**: Persistence contract for the `TaxpayerProfile` entity. A single-user desktop
application is expected to have exactly one profile at runtime.

**Methods**:

| Signature | Description |
|-----------|-------------|
| `Task<TaxpayerProfile?> GetAsync(CancellationToken ct = default)` | Retrieve the single `TaxpayerProfile`. Returns `null` if no profile has been created yet. |
| `Task SaveAsync(TaxpayerProfile profile, CancellationToken ct = default)` | Create or update the taxpayer profile (upsert semantics). |
| `Task DeleteAsync(CancellationToken ct = default)` | Remove the taxpayer profile (e.g., factory reset). |

---

## IExchangeRateCacheRepository

**File**: `Rentier.Application/Repositories/IExchangeRateCacheRepository.cs`

**Purpose**: Cache persistence contract for `ExchangeRate` value objects fetched from the NBS
exchange-rate API. Avoids repeated HTTP calls for the same date/currency pair.

**Methods**:

| Signature | Description |
|-----------|-------------|
| `Task<ExchangeRate?> GetAsync(DateOnly date, string currency, CancellationToken ct = default)` | Retrieve a cached `ExchangeRate` for the given date and currency. Returns `null` if not cached. |
| `Task<IReadOnlyList<ExchangeRate>> GetByDateRangeAsync(DateOnly from, DateOnly to, string currency, CancellationToken ct = default)` | Retrieve all cached rates for a currency between two dates (inclusive). |
| `Task SaveAsync(ExchangeRate rate, CancellationToken ct = default)` | Persist (upsert) a fetched `ExchangeRate` into the cache. |
| `Task SaveBatchAsync(IReadOnlyList<ExchangeRate> rates, CancellationToken ct = default)` | Persist a batch of rates (e.g., after scraping a date range from NBS). |

---

## Notes

- All methods are async (`Task`/`Task<T>`) per constitution Principle IV.
- All return types that could be absent return nullable (`T?`) or empty collections — no
  exceptions are thrown for "not found" cases; callers check for `null` or empty.
- Implementation classes in `Rentier.Infrastructure` will inherit from these interfaces and
  use `AppDbContext` for persistence.
- No EF Core types (e.g., `IQueryable<T>`) appear in these interface signatures; callers in
  `Rentier.Application` and `Rentier.Desktop` must not depend on EF Core types directly.
