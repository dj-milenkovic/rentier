# Contract: Importer Entity Design — Before / After

**Feature**: 005 — Importer Configuration  
**File**: `src/Rentier.Domain/Entities/Importer.cs`  
**Date**: 2026-04-06

---

## Before (existing stub — to be replaced)

```csharp
using Rentier.Domain.Exceptions;

namespace Rentier.Domain.Entities;

/// <summary>
/// Represents a named import configuration for filtering and reading IBKR CSV activity statements.
/// </summary>
public sealed class Importer
{
    public Guid Id { get; }
    public string DisplayName { get; }
    public string FilterExpression { get; }

    public Importer(Guid id, string displayName, string filterExpression = "")
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("DisplayName must not be null or whitespace");

        Id = id;
        DisplayName = displayName;
        FilterExpression = filterExpression ?? string.Empty;
    }
}
```

### Issues with the existing design

| Issue | Impact |
|---|---|
| Public constructor with `Guid id` param | Callers must generate the ID; EF cannot materialise without a custom factory; no parameterless private constructor |
| `{ get; }` (init-only) on all props | EF cannot set property values after construction without complex configuration |
| `FilterExpression` single field | Does not map to the three-filter model (`FromFilter`, `SubjectFilter`, `AttachmentRegex`) |
| No `TaxpayerProfileId` or `MailboxId` | Cannot store FK references to related aggregates |
| No `ReportType` | Cannot distinguish importer strategies |
| No `PaymentNotes` | Missing required field from spec |
| No static factory method | No canonical creation path with domain validation |
| No mutation method | No way to update fields without reconstructing the entity |

---

## After (redesigned — Feature 005)

```csharp
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;

namespace Rentier.Domain.Entities;

public sealed class Importer
{
    // ── EF materialization constructor ──────────────────────────────────
    private Importer() { }

    // ── Properties ──────────────────────────────────────────────────────
    public Guid Id                 { get; private set; }
    public string DisplayName      { get; private set; } = string.Empty;
    public ReportType ReportType   { get; private set; } = ReportType.IbkrCsv;
    public Guid? TaxpayerProfileId { get; private set; }
    public Guid? MailboxId         { get; private set; }
    public string FromFilter       { get; private set; } = string.Empty;
    public string SubjectFilter    { get; private set; } = string.Empty;
    public string AttachmentRegex  { get; private set; } = string.Empty;
    public string PaymentNotes     { get; private set; } = string.Empty;

    // ── Factory method ───────────────────────────────────────────────────
    /// <summary>
    /// Creates a new <see cref="Importer"/> with a generated <see cref="Id"/>.
    /// All optional fields default to empty string / null / default enum value.
    /// </summary>
    /// <exception cref="DomainException">Thrown when <paramref name="displayName"/> is null, empty, or exceeds 200 characters.</exception>
    public static Importer Create(
        string displayName,
        ReportType reportType = ReportType.IbkrCsv)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("Importer.DisplayName must not be empty.");
        if (displayName.Length > 200)
            throw new DomainException("Importer.DisplayName must not exceed 200 characters.");

        return new Importer
        {
            Id         = Guid.NewGuid(),
            DisplayName = displayName.Trim(),
            ReportType  = reportType
        };
    }

    // ── Mutation method ──────────────────────────────────────────────────
    /// <summary>
    /// Updates all mutable fields on the importer in a single atomic call.
    /// Null strings are coerced to empty string.
    /// </summary>
    /// <exception cref="DomainException">Thrown when <paramref name="displayName"/> is empty or exceeds 200 characters, or when <paramref name="paymentNotes"/> exceeds 4000 characters.</exception>
    public void UpdateDetails(
        string displayName,
        ReportType reportType,
        Guid? taxpayerProfileId,
        Guid? mailboxId,
        string fromFilter,
        string subjectFilter,
        string attachmentRegex,
        string paymentNotes)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("Importer.DisplayName must not be empty.");
        if (displayName.Length > 200)
            throw new DomainException("Importer.DisplayName must not exceed 200 characters.");
        if (paymentNotes?.Length > 4000)
            throw new DomainException("Importer.PaymentNotes must not exceed 4000 characters.");

        DisplayName       = displayName.Trim();
        ReportType        = reportType;
        TaxpayerProfileId = taxpayerProfileId;
        MailboxId         = mailboxId;
        FromFilter        = fromFilter        ?? string.Empty;
        SubjectFilter     = subjectFilter     ?? string.Empty;
        AttachmentRegex   = attachmentRegex   ?? string.Empty;
        PaymentNotes      = paymentNotes      ?? string.Empty;
    }
}
```

---

## Diff Summary

| Element | Before | After |
|---|---|---|
| Constructors | `public Importer(Guid, string, string)` | `private Importer()` + `public static Create(string, ReportType)` |
| `Id` | `public Guid Id { get; }` | `public Guid Id { get; private set; }` |
| `DisplayName` | `public string DisplayName { get; }` | `public string DisplayName { get; private set; }` |
| `FilterExpression` | `public string FilterExpression { get; }` | **REMOVED** |
| `ReportType` | — (missing) | `public ReportType ReportType { get; private set; }` |
| `TaxpayerProfileId` | — (missing) | `public Guid? TaxpayerProfileId { get; private set; }` |
| `MailboxId` | — (missing) | `public Guid? MailboxId { get; private set; }` |
| `FromFilter` | — (missing) | `public string FromFilter { get; private set; }` |
| `SubjectFilter` | — (missing) | `public string SubjectFilter { get; private set; }` |
| `AttachmentRegex` | — (missing) | `public string AttachmentRegex { get; private set; }` |
| `PaymentNotes` | — (missing) | `public string PaymentNotes { get; private set; }` |
| Mutation | — (none) | `public void UpdateDetails(...)` |
| EF compatibility | ❌ no parameterless ctor | ✅ `private Importer()` |

---

## Factory Method Signatures

```csharp
// Primary creation path — used by AddImporterCommandHandler
public static Importer Create(
    string displayName,
    ReportType reportType = ReportType.IbkrCsv)
    → Importer

// In-place update path — used by Add and Update handlers after initial creation
public void UpdateDetails(
    string displayName,
    ReportType reportType,
    Guid? taxpayerProfileId,
    Guid? mailboxId,
    string fromFilter,
    string subjectFilter,
    string attachmentRegex,
    string paymentNotes)
    → void
```

### AddImporterCommandHandler usage pattern

```csharp
var importer = Importer.Create(command.DisplayName, command.ReportType);
importer.UpdateDetails(
    command.DisplayName,
    command.ReportType,
    command.TaxpayerProfileId,
    command.MailboxId,
    command.FromFilter,
    command.SubjectFilter,
    command.AttachmentRegex,
    command.PaymentNotes);
await _repository.AddAsync(importer, ct);
return Result<Guid, Error>.Success(importer.Id);
```

### UpdateImporterCommandHandler usage pattern

```csharp
var importer = await _repository.GetByIdAsync(command.Id, ct);
if (importer is null)
    return Result<VoidResult, Error>.Failure(new Error("IMPORTER_NOT_FOUND", $"No importer with ID {command.Id}."));

importer.UpdateDetails(
    command.DisplayName,
    command.ReportType,
    command.TaxpayerProfileId,
    command.MailboxId,
    command.FromFilter,
    command.SubjectFilter,
    command.AttachmentRegex,
    command.PaymentNotes);
await _repository.UpdateAsync(importer, ct);
return Result<VoidResult, Error>.Success(VoidResult.Value);
```

---

## Validation Rules Summary

| Rule | Enforced In | Error Code / Exception |
|---|---|---|
| `DisplayName` non-empty | `Importer.Create`, `UpdateDetails` | `DomainException` |
| `DisplayName` ≤ 200 chars | `Importer.Create`, `UpdateDetails` | `DomainException` |
| `PaymentNotes` ≤ 4000 chars | `UpdateDetails` | `DomainException` |
| `AttachmentRegex` is valid .NET regex | `AddImporterCommandHandler`, `UpdateImporterCommandHandler` | `Error("INVALID_REGEX", ...)` |
| `FromFilter`, `SubjectFilter` — no validation | — | Plain strings; no constraint |
| `TaxpayerProfileId`, `MailboxId` — optional | — | `null` = no association |
