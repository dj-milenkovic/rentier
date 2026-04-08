# Data Model: IMAP Mailbox Configuration (Feature 004)

**Phase**: 1 — Design  
**Branch**: `feature/004-mailbox-configuration`  
**Date**: 2026-04-06

---

## 1. Domain Entities

### 1.1 `Mailbox` Entity — Before / After

**Before** (`src/Rentier.Domain/Entities/Mailbox.cs` — current state):
```csharp
public sealed class Mailbox
{
    public Guid Id { get; }
    public string Host { get; }
    public int Port { get; }
    public string Username { get; }
    public MailboxCursor Cursor { get; }

    public Mailbox(Guid id, string host, int port, string username, MailboxCursor cursor) { ... }
}
```

**After** (required modifications for this feature):
```csharp
public sealed class Mailbox
{
    // EF Core parameterless constructor (private — never used by application code)
    private Mailbox() { }

    public Guid Id { get; private set; }
    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public DateOnly InitialSyncDate { get; private set; }   // NEW: user-configured start date
    public MailboxCursor Cursor { get; private set; } = default!;

    // Public constructor (kept for test harness; factory preferred in production code)
    public Mailbox(Guid id, string host, int port, string username,
                   DateOnly initialSyncDate, MailboxCursor cursor)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new DomainException("Host must not be null or whitespace");
        if (port < 1 || port > 65535)
            throw new DomainException($"Port must be in range 1–65535, got {port}");
        if (string.IsNullOrWhiteSpace(username))
            throw new DomainException("Username must not be null or whitespace");
        ArgumentNullException.ThrowIfNull(cursor);

        Id = id;
        Host = host;
        Port = port;
        Username = username;
        InitialSyncDate = initialSyncDate;
        Cursor = cursor;
    }

    // Static factory (preferred in Application handlers)
    public static Mailbox Create(string host, int port, string username, DateOnly initialSyncDate)
        => new(Guid.NewGuid(), host, port, username, initialSyncDate,
               new MailboxCursor(LastSyncDate: initialSyncDate, LastUid: null));

    // Mutation method — called by sync feature (Feature 006+)
    public void UpdateCursor(MailboxCursor newCursor)
    {
        ArgumentNullException.ThrowIfNull(newCursor);
        Cursor = newCursor;
    }
}
```

**Summary of Changes**:
| Change | Reason |
|--------|--------|
| `private Mailbox()` added | EF Core parameterless constructor for materialization |
| All `{ get; }` → `{ get; private set; }` | Allows EF to set properties via reflection; preserves domain encapsulation |
| `InitialSyncDate` property added | Stores user-configured start date; not mutated by sync; displayed in UI |
| `Mailbox.Create(...)` factory | Encapsulates `Guid.NewGuid()` and initial cursor construction |
| `UpdateCursor(MailboxCursor)` method | Clean mutation for sync feature; avoids property setter leaking to Application |
| `DomainException` on `Cursor` null → `ArgumentNullException.ThrowIfNull` | Align with .NET 8 idiomatic null-check |

---

### 1.2 `MailboxCursor` Value Object — Unchanged

```csharp
// src/Rentier.Domain/ValueObjects/MailboxCursor.cs — NO CHANGES
public record MailboxCursor(DateOnly? LastSyncDate, long? LastUid);
```

Both fields are nullable: `null` = no sync has occurred.  
Stored via EF `OwnsOne` as inline columns on the `Mailboxes` table.

---

## 2. Application DTOs

### 2.1 `MailboxDto`

**File**: `src/Rentier.Application/DTOs/MailboxDto.cs`  
**Status**: NEW

```csharp
namespace Rentier.Application.DTOs;

/// <summary>
/// Read-only projection of a Mailbox entity. Password is intentionally excluded.
/// </summary>
public sealed record MailboxDto(
    Guid       Id,
    string     Host,
    int        Port,
    string     Username,
    DateOnly   InitialSyncDate,
    DateOnly?  LastSyncDate,
    long?      LastUid);
```

**Notes**:
- Password is never projected into a DTO (constitution Principle II — credentials stay in OS store).
- `LastSyncDate` and `LastUid` come from `MailboxCursor`; `null` when no sync has occurred.
- Used by `GetMailboxesQueryHandler` and consumed by `MailboxSettingsViewModel`.

---

## 3. Application Commands and Queries

### 3.1 `AddMailboxCommand`

```csharp
// src/Rentier.Application/Commands/AddMailboxCommand.cs — NEW
public sealed record AddMailboxCommand(
    string   Host,
    int      Port,
    string   Username,
    string?  Password,        // null/empty = no credential stored yet (unusual but allowed)
    DateOnly InitialSyncDate);
// Returns: Result<Guid, Error>  (Guid = new mailbox Id)
```

### 3.2 `UpdateMailboxCommand`

```csharp
// src/Rentier.Application/Commands/UpdateMailboxCommand.cs — NEW
public sealed record UpdateMailboxCommand(
    Guid     Id,
    string   Host,
    int      Port,
    string   Username,
    string?  Password,        // null/empty = keep existing credential
    DateOnly InitialSyncDate);
// Returns: Result<VoidResult, Error>
```

### 3.3 `DeleteMailboxCommand`

```csharp
// src/Rentier.Application/Commands/DeleteMailboxCommand.cs — NEW
public sealed record DeleteMailboxCommand(Guid Id);
// Returns: Result<VoidResult, Error>
```

### 3.4 `GetMailboxesQuery`

```csharp
// src/Rentier.Application/Queries/GetMailboxesQuery.cs — NEW
public sealed record GetMailboxesQuery();
// Returns: Result<IReadOnlyList<MailboxDto>, Error>
```

---

## 4. Repository Contract

### 4.1 `IMailboxRepository` — Already Exists (No Changes Required)

```csharp
// src/Rentier.Application/Repositories/IMailboxRepository.cs — EXISTS, UNCHANGED
public interface IMailboxRepository
{
    Task<Mailbox?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Mailbox>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Mailbox mailbox, CancellationToken ct = default);
    Task UpdateAsync(Mailbox mailbox, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

The stub already defines the full needed surface. No amendments required.

---

## 5. EF Core Configuration

### 5.1 `MailboxConfiguration`

**File**: `src/Rentier.Infrastructure/Persistence/Configurations/MailboxConfiguration.cs`  
**Status**: NEW

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rentier.Domain.Entities;

namespace Rentier.Infrastructure.Persistence.Configurations;

public sealed class MailboxConfiguration : IEntityTypeConfiguration<Mailbox>
{
    public void Configure(EntityTypeBuilder<Mailbox> builder)
    {
        builder.ToTable("Mailboxes");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Host)
               .IsRequired()
               .HasMaxLength(253);           // max hostname length per RFC 1035

        builder.Property(m => m.Port)
               .IsRequired();

        builder.Property(m => m.Username)
               .IsRequired()
               .HasMaxLength(320);           // max email address length per RFC 5321

        builder.Property(m => m.InitialSyncDate)
               .IsRequired();               // EF 8 maps DateOnly → TEXT "YYYY-MM-DD" natively

        // OwnsOne maps MailboxCursor inline on the Mailboxes table
        builder.OwnsOne(m => m.Cursor, cursor =>
        {
            cursor.Property(c => c.LastSyncDate)
                  .HasColumnName("Cursor_LastSyncDate")
                  .IsRequired(false);

            cursor.Property(c => c.LastUid)
                  .HasColumnName("Cursor_LastUid")
                  .IsRequired(false);
        });
    }
}
```

### 5.2 `AppDbContext` Changes

**File**: `src/Rentier.Infrastructure/Persistence/AppDbContext.cs`  
**Status**: MODIFIED — add `DbSet<Mailbox>`

```csharp
// Add to existing AppDbContext:
public DbSet<Mailbox> Mailboxes => Set<Mailbox>();
```

`OnModelCreating` already calls `ApplyConfigurationsFromAssembly(...)` — `MailboxConfiguration` is discovered automatically. No additional changes needed.

---

## 6. Migration

### 6.1 `0004_MailboxConfiguration` Migration

**File**: `src/Rentier.Infrastructure/Persistence/Migrations/YYYYMMDDHHMMSS_0004_MailboxConfiguration.cs`  
**Status**: NEW (generated by `dotnet ef migrations add 0004_MailboxConfiguration`)

**Expected DDL** (SQLite):
```sql
CREATE TABLE "Mailboxes" (
    "Id"                   TEXT NOT NULL CONSTRAINT "PK_Mailboxes" PRIMARY KEY,
    "Host"                 TEXT NOT NULL,
    "Port"                 INTEGER NOT NULL,
    "Username"             TEXT NOT NULL,
    "InitialSyncDate"      TEXT NOT NULL,
    "Cursor_LastSyncDate"  TEXT,
    "Cursor_LastUid"       INTEGER
);
```

No foreign keys. No indexes beyond PK (query-by-Id and GetAll cover the use cases).

---

## 7. Entity Relationship Diagram

```
┌─────────────────────────────────────────────────────────────┐
│  Mailboxes (EF table)                                       │
│                                                             │
│  Id                    TEXT (GUID)   PK                     │
│  Host                  TEXT          NOT NULL               │
│  Port                  INTEGER       NOT NULL               │
│  Username              TEXT          NOT NULL               │
│  InitialSyncDate       TEXT          NOT NULL  (DateOnly)   │
│  Cursor_LastSyncDate   TEXT          NULLABLE (DateOnly)    │
│  Cursor_LastUid        INTEGER       NULLABLE (long)        │
└─────────────────────────────────────────────────────────────┘

Domain model:
  Mailbox (Entity)
    └── MailboxCursor (Value Object — OwnsOne inline)
```

Windows Credential Manager (external, not EF):
```
Key: "Rentier/Mailbox/{Id}"
Value: UTF-8 encoded IMAP password
Type: CRED_TYPE_GENERIC
Persist: CRED_PERSIST_LOCAL_MACHINE
```
