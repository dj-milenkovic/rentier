# Data Model: Rentier Initial Project Setup

**Feature**: `001-initial-setup`  
**Phase**: 1 — Design & Contracts  
**Date**: 2026-04-06  
**Status**: Complete

---

## Overview

This document describes all domain entities and value objects introduced in the initial scaffold.
All types live in `Rentier.Domain`. These are stub declarations only — no persistence mappings,
no business logic beyond the `Filing` status machine invariant, and no I/O dependencies.

---

## Domain Entities

Entities have identity (a unique ID), may change state over time, and carry domain behaviour.

---

### TaxpayerProfile

**Kind**: Entity  
**Project**: `Rentier.Domain/Entities/TaxpayerProfile.cs`

**Purpose**: Represents the Serbian taxpayer whose income is being reported and whose PP-OPO
filings are managed by the application.

**Fields**:

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| `Id` | `Guid` | Surrogate identity | Required; never null |
| `Jmbg` | `string` | Serbian personal ID number (13 digits) | Required; 13 characters; digits only |
| `FullName` | `string` | First + last name | Required; not empty |
| `Address` | `string` | Street address | Required; not empty |
| `OpstinaCode` | `string` | Serbian municipality code | Required; not empty |

**Invariants**:
- JMBG must be exactly 13 characters and contain only digits (enforced in Domain constructor).
- FullName, Address, and OpstinaCode must not be null or whitespace.

**Relationships**:
- `TaxpayerProfile` is referenced by `Filing` (a filing belongs to one taxpayer).
- Only one `TaxpayerProfile` is expected per application instance (single-user desktop tool).

---

### Mailbox

**Kind**: Entity  
**Project**: `Rentier.Domain/Entities/Mailbox.cs`

**Purpose**: Represents an IMAP mailbox connection configuration. Used to sync emails containing
dividend and interest notifications from a user's inbox.

**Fields**:

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| `Id` | `Guid` | Surrogate identity | Required |
| `Host` | `string` | IMAP server hostname | Required; not empty |
| `Port` | `int` | IMAP port (e.g., 993) | Required; 1–65535 |
| `Username` | `string` | IMAP login username (email address) | Required; not empty |
| `Cursor` | `MailboxCursor` | Current sync position | Required; non-null |

**Invariants**:
- `Port` must be in range 1–65535.
- `Username` and `Host` must not be null or whitespace.
- IMAP password is NOT stored in this entity; it is retrieved at runtime from `ICredentialStore`.

**Relationships**:
- Contains exactly one `MailboxCursor` value object (owned; updated on each sync).

---

### Importer

**Kind**: Entity  
**Project**: `Rentier.Domain/Entities/Importer.cs`

**Purpose**: Represents a named import configuration for filtering and reading IBKR CSV activity
statements. Different importers may target different account types or CSV column mappings.

**Fields**:

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| `Id` | `Guid` | Surrogate identity | Required |
| `DisplayName` | `string` | Human-readable name shown in UI | Required; not empty |
| `FilterExpression` | `string` | Stub expression for row filtering | Optional; empty string if no filter |

**Invariants**:
- `DisplayName` must not be null or whitespace.

**Relationships**:
- Referenced by `Report` (a report was produced by a specific importer run).

---

### Report

**Kind**: Entity  
**Project**: `Rentier.Domain/Entities/Report.cs`

**Purpose**: Represents a parsed activity statement produced from an imported IBKR CSV file.
Each import run produces one `Report` containing the extracted income events.

**Fields**:

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| `Id` | `Guid` | Surrogate identity | Required |
| `ImportDate` | `DateOnly` | Date the CSV was imported | Required; not future |
| `ImporterId` | `Guid` | Reference to the `Importer` that produced this report | Required |

**Invariants**:
- `ImportDate` must not be in the future at time of creation.

**Relationships**:
- References one `Importer` (by `ImporterId`).
- Referenced by `Filing` (a filing is based on the income events in one or more reports).

---

### Filing (Aggregate Root)

**Kind**: Aggregate Root (Entity)  
**Project**: `Rentier.Domain/Entities/Filing.cs`

**Purpose**: Represents a PP-OPO tax filing. Enforces the three-state lifecycle. All state
transitions are validated in the domain; invalid transitions throw `DomainException`.

**Fields**:

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| `Id` | `Guid` | Surrogate identity | Required |
| `TaxpayerProfileId` | `Guid` | Reference to the taxpayer | Required |
| `TaxPeriod` | `DateOnly` | The income period this filing covers (typically the dividend payment date) | Required |
| `Status` | `FilingStatus` | Current lifecycle status | Required; see status machine |

**Status Machine** (`FilingStatus` enum):

```text
Init ──(SubmitXml)──► Filed ──(ConfirmPayment)──► Paid
```

| Transition | From | To | Trigger |
|------------|------|-----|---------|
| Submit XML | `Init` | `Filed` | XML file sent to ePorezi |
| Confirm payment | `Filed` | `Paid` | Payment confirmed in ePorezi |

**Invalid transitions** (any other transition) throw `DomainException` with a descriptive message,
for example:
- `Filed → Init` (rollback prohibited)
- `Paid → Init` (rollback prohibited)
- `Paid → Filed` (rollback prohibited)
- `Init → Paid` (must go through `Filed` first)

**Invariants**:
- `Status` may only advance along the defined path; backward transitions are prohibited.
- `TaxPeriod` must not be null.
- Only one `Filing` per `TaxpayerProfileId` + `TaxPeriod` combination (enforced at repository level in future features).

**Relationships**:
- References one `TaxpayerProfile` (by `TaxpayerProfileId`).
- Associated with one or more `Report` objects (tracked in future persistence feature).

---

## Value Objects

Value objects have no identity; equality is determined by their field values. All value objects
are declared as C# `record` types to obtain structural equality automatically.

---

### MailboxCursor

**Kind**: Value Object (`record`)  
**Project**: `Rentier.Domain/ValueObjects/MailboxCursor.cs`

**Purpose**: Represents the last-synced position in a mailbox. Used by the IMAP sync process to
resume from where it left off rather than re-fetching all messages.

**Fields**:

| Field | Type | Description | Notes |
|-------|------|-------------|-------|
| `LastSyncDate` | `DateOnly?` | Date of the last successfully synced message | Null if no sync has occurred |
| `LastUid` | `long?` | UID of the last successfully synced message | Null if no sync has occurred |

**Invariants**:
- At least one of `LastSyncDate` or `LastUid` should be set after first sync; both may be null
  for a freshly created `Mailbox`.
- Both cannot simultaneously represent conflicting state (resolution deferred to sync feature).

**Owned by**: `Mailbox` entity.

---

### Money

**Kind**: Value Object (`record`)  
**Project**: `Rentier.Domain/ValueObjects/Money.cs`

**Purpose**: Represents a monetary amount with its currency. Used for dividend amounts,
withholding tax, and filing totals.

**Fields**:

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| `Amount` | `decimal` | Monetary value | Required; **`decimal` — not `double` or `float`** |
| `Currency` | `string` | ISO 4217 currency code (e.g., `"USD"`, `"RSD"`) | Required; 3 uppercase characters |

**Invariants**:
- `Amount` must be `decimal` (constitution Principle III — no `double` or `float`).
- `Currency` must not be null or empty; expected to be a valid ISO 4217 code (validated in future features).
- `Amount` may be negative only for adjustments or corrections (business rule deferred).

---

### ExchangeRate

**Kind**: Value Object (`record`)  
**Project**: `Rentier.Domain/ValueObjects/ExchangeRate.cs`

**Purpose**: Represents a daily NBS (Narodna Banka Srbije) official exchange rate from a foreign
currency to RSD. Used to convert foreign dividend amounts to RSD for PP-OPO calculation.

**Fields**:

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| `Date` | `DateOnly` | The date this rate applies | Required; **`DateOnly` — not `DateTime`** |
| `Currency` | `string` | ISO 4217 source currency code (e.g., `"USD"`, `"EUR"`) | Required; 3 characters |
| `RateToRsd` | `decimal` | Rate: 1 unit of `Currency` = `RateToRsd` RSD | Required; **`decimal`**; must be positive |

**Invariants**:
- `Date` must be `DateOnly` (constitution Principle III).
- `RateToRsd` must be `decimal` (no `double`/`float`).
- `RateToRsd` must be positive.
- Two `ExchangeRate` records with the same `Date` and `Currency` are considered equal by value.

---

### HolidayConf

**Kind**: Value Object (`record`)  
**Project**: `Rentier.Domain/ValueObjects/HolidayConf.cs`

**Purpose**: Represents a configured list of public holidays (Serbian national holidays + weekends
are excluded from filing deadline calculations). Used to advance a deadline to the next working
day when it falls on a weekend or public holiday.

**Fields**:

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| `Holidays` | `IReadOnlyList<DateOnly>` | List of public holiday dates | Required; non-null; may be empty |

**Invariants**:
- `Holidays` must not be null (empty list is valid for a jurisdiction with no public holidays).
- All dates in `Holidays` must be `DateOnly` (not `DateTime`).

---

## Supporting Types

### FilingStatus (Enum)

**Project**: `Rentier.Domain/Entities/FilingStatus.cs` (or nested in `Filing.cs`)

```text
Init   = 0   // Filing created; XML not yet submitted
Filed  = 1   // XML submitted to ePorezi
Paid   = 2   // Payment confirmed in ePorezi
```

### DomainException

**Project**: `Rentier.Domain/Exceptions/DomainException.cs`

A `sealed` exception class inheriting from `Exception`. Thrown by domain entities and value
objects to signal invariant violations (e.g., invalid `Filing` status transitions).

---

## Dependency and Relationship Diagram

```text
Filing (Aggregate Root)
 ├── references TaxpayerProfile (by TaxpayerProfileId)
 └── references Report (by ReportId — future feature)

Mailbox (Entity)
 └── owns MailboxCursor (Value Object — embedded)

Report (Entity)
 └── references Importer (by ImporterId)

Money (Value Object — used by Filing, Report, ExchangeRate contexts in future features)
ExchangeRate (Value Object — used by tax calculation in future features)
HolidayConf (Value Object — used by deadline calculation in future features)
```

---

## Type Constraint Summary

| Field | Required Type | Reason |
|-------|--------------|--------|
| `Money.Amount` | `decimal` | Constitution Principle III — no `double`/`float` |
| `ExchangeRate.RateToRsd` | `decimal` | Constitution Principle III |
| `ExchangeRate.Date` | `DateOnly` | Constitution Principle III — no `DateTime` |
| `Filing.TaxPeriod` | `DateOnly` | Constitution Principle III |
| `Report.ImportDate` | `DateOnly` | Constitution Principle III |
| `MailboxCursor.LastSyncDate` | `DateOnly?` | Constitution Principle III |
| `HolidayConf.Holidays` | `IReadOnlyList<DateOnly>` | Constitution Principle III |
