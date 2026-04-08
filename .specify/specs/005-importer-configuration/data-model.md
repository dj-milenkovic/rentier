# Data Model: Feature 005 — Importer Configuration

**Phase**: 1 — Design & Contracts  
**Date**: 2026-04-06

---

## 1. Domain Entity: `Importer` (Before / After)

### Before (current `src/Rentier.Domain/Entities/Importer.cs`)

```csharp
public sealed class Importer
{
    public Guid Id { get; }
    public string DisplayName { get; }
    public string FilterExpression { get; }          // ← REMOVED

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

**Problems with the existing design**:
- Public constructor with Guid param prevents EF from materialising without custom factory.
- Single `FilterExpression` does not map to the three-filter model required (From, Subject, Attachment).
- No FK fields for TaxpayerProfile or Mailbox.
- No `ReportType` field.
- `{ get; }` (no setter) prevents EF `Entry().CurrentValues.SetValues(...)` pattern.

---

### After (redesigned `src/Rentier.Domain/Entities/Importer.cs`)

```csharp
namespace Rentier.Domain.Entities;

using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;

public sealed class Importer
{
    // ── EF materialisation constructor (private) ───────────────────────
    private Importer() { }

    // ── Properties ─────────────────────────────────────────────────────
    public Guid Id                    { get; private set; }
    public string DisplayName         { get; private set; } = string.Empty;
    public ReportType ReportType      { get; private set; } = ReportType.IbkrCsv;
    public Guid? TaxpayerProfileId    { get; private set; }
    public Guid? MailboxId            { get; private set; }
    public string FromFilter          { get; private set; } = string.Empty;
    public string SubjectFilter       { get; private set; } = string.Empty;
    public string AttachmentRegex     { get; private set; } = string.Empty;
    public string PaymentNotes        { get; private set; } = string.Empty;

    // ── Factory method ─────────────────────────────────────────────────
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
            Id          = Guid.NewGuid(),
            DisplayName = displayName.Trim(),
            ReportType  = reportType
        };
    }

    // ── Mutation method ────────────────────────────────────────────────
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

        DisplayName        = displayName.Trim();
        ReportType         = reportType;
        TaxpayerProfileId  = taxpayerProfileId;
        MailboxId          = mailboxId;
        FromFilter         = fromFilter        ?? string.Empty;
        SubjectFilter      = subjectFilter     ?? string.Empty;
        AttachmentRegex    = attachmentRegex   ?? string.Empty;
        PaymentNotes       = paymentNotes      ?? string.Empty;
    }
}
```

**Key changes**:
| Change | Reason |
|---|---|
| `private Importer()` added | EF Core requires a private or protected parameterless constructor for materialization |
| All props `{ get; private set; }` | Allows EF to set values; blocks external mutation |
| `FilterExpression` removed | Replaced by three dedicated fields |
| `TaxpayerProfileId`, `MailboxId` added as `Guid?` | FK scalars without navigation props |
| `ReportType`, `FromFilter`, `SubjectFilter`, `AttachmentRegex`, `PaymentNotes` added | Full field set per spec |
| `Importer.Create(...)` factory | Canonical creation path with domain validation |
| `UpdateDetails(...)` mutation | Single method for in-place updates (no separate setters) |

---

## 2. New Enum: `ReportType`

**File**: `src/Rentier.Domain/Enums/ReportType.cs` (new `Enums/` folder)

```csharp
namespace Rentier.Domain.Enums;

public enum ReportType
{
    IbkrCsv = 0
}
```

EF stores as `INTEGER` (default). No custom converter needed.

Desktop display name provided by `ReportTypeExtensions.ToDisplayString()`:
```csharp
// src/Rentier.Desktop/Extensions/ReportTypeExtensions.cs
public static string ToDisplayString(this ReportType reportType) =>
    reportType switch
    {
        ReportType.IbkrCsv => "IBKR CSV",
        _ => reportType.ToString()
    };
```

---

## 3. DTO: `ImporterDto`

**File**: `src/Rentier.Application/DTOs/ImporterDto.cs`

```csharp
namespace Rentier.Application.DTOs;

using Rentier.Domain.Enums;

public sealed record ImporterDto(
    Guid Id,
    string DisplayName,
    ReportType ReportType,
    Guid? TaxpayerProfileId,
    Guid? MailboxId,
    string FromFilter,
    string SubjectFilter,
    string AttachmentRegex,
    string PaymentNotes);
```

Projected from `Importer` entity in `GetImportersQueryHandler`. Contains no navigation objects — IDs only.

---

## 4. EF Core Configuration

**File**: `src/Rentier.Infrastructure/Persistence/Configurations/ImporterConfiguration.cs`

```csharp
namespace Rentier.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rentier.Domain.Entities;

public sealed class ImporterConfiguration : IEntityTypeConfiguration<Importer>
{
    public void Configure(EntityTypeBuilder<Importer> builder)
    {
        builder.ToTable("Importers");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.ReportType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(i => i.FromFilter)
            .HasMaxLength(500)
            .HasDefaultValue(string.Empty);

        builder.Property(i => i.SubjectFilter)
            .HasMaxLength(500)
            .HasDefaultValue(string.Empty);

        builder.Property(i => i.AttachmentRegex)
            .HasMaxLength(1000)
            .HasDefaultValue(string.Empty);

        builder.Property(i => i.PaymentNotes)
            .HasMaxLength(4000)
            .HasDefaultValue(string.Empty);

        // FK to TaxpayerProfile — optional, no navigation property
        builder.HasOne<TaxpayerProfile>()
            .WithMany()
            .HasForeignKey(i => i.TaxpayerProfileId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // FK to Mailbox — optional, no navigation property
        builder.HasOne<Mailbox>()
            .WithMany()
            .HasForeignKey(i => i.MailboxId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
```

**FK design rationale**:
- `TaxpayerProfileId` and `MailboxId` are real CLR properties (not shadow properties), so `HasForeignKey(i => i.Prop)` is the correct overload.
- `IsRequired(false)` → SQLite column is nullable (`NULL` allowed).
- `OnDelete(DeleteBehavior.SetNull)` → if the referenced profile or mailbox row is deleted, EF sets the FK column to `NULL` rather than cascading the delete to the importer row.
- No navigation properties on either side — the configuration is one-way (`HasOne<T>().WithMany()` with no lambda for the collection).

---

## 5. Database Schema (after migration 0005)

### Table: `Importers`

| Column | Type | Nullable | Constraint |
|---|---|---|---|
| `Id` | TEXT (GUID) | NOT NULL | PK |
| `DisplayName` | TEXT | NOT NULL | max 200 |
| `ReportType` | INTEGER | NOT NULL | default 0 |
| `TaxpayerProfileId` | TEXT (GUID) | NULL | FK → TaxpayerProfiles(Id) ON DELETE SET NULL |
| `MailboxId` | TEXT (GUID) | NULL | FK → Mailboxes(Id) ON DELETE SET NULL |
| `FromFilter` | TEXT | NULL | max 500, default '' |
| `SubjectFilter` | TEXT | NULL | max 500, default '' |
| `AttachmentRegex` | TEXT | NULL | max 1000, default '' |
| `PaymentNotes` | TEXT | NULL | max 4000, default '' |

> Note: SQLite stores GUIDs as TEXT (lower-hyphen format). EF Core 8 handles the conversion automatically.

---

## 6. CQRS Commands and Queries

### Commands

```csharp
// AddImporterCommand.cs
public sealed record AddImporterCommand(
    string DisplayName,
    ReportType ReportType,
    Guid? TaxpayerProfileId,
    Guid? MailboxId,
    string FromFilter,
    string SubjectFilter,
    string AttachmentRegex,
    string PaymentNotes);
// → Result<Guid, Error>

// UpdateImporterCommand.cs
public sealed record UpdateImporterCommand(
    Guid Id,
    string DisplayName,
    ReportType ReportType,
    Guid? TaxpayerProfileId,
    Guid? MailboxId,
    string FromFilter,
    string SubjectFilter,
    string AttachmentRegex,
    string PaymentNotes);
// → Result<VoidResult, Error>

// DeleteImporterCommand.cs
public sealed record DeleteImporterCommand(Guid Id);
// → Result<VoidResult, Error>
```

### Queries

```csharp
// GetImportersQuery.cs
public sealed record GetImportersQuery();
// → Result<IReadOnlyList<ImporterDto>, Error>
```

### Error Codes

| Code | Trigger |
|---|---|
| `IMPORTER_NOT_FOUND` | UpdateImporterCommand / DeleteImporterCommand — Guid not in DB |
| `INVALID_REGEX` | AddImporterCommand / UpdateImporterCommand — AttachmentRegex fails `new Regex(...)` |
| `DOMAIN_ERROR` | Domain validation failure caught and wrapped |

---

## 7. ViewModel Structure

```
ImporterSettingsViewModel (ReactiveObject)
├── ObservableCollection<ImporterItemViewModel> ImporterItems
├── ImporterItemViewModel? SelectedImporter       ← RaiseAndSetIfChanged; drives form population
│
├── ─── Form fields (all RaiseAndSetIfChanged) ───────────────────────────────
├── string DisplayName
├── ReportType SelectedReportType
├── TaxpayerProfileDto? SelectedTaxpayerProfile
├── MailboxDto? SelectedMailbox
├── string FromFilter
├── string SubjectFilter
├── string AttachmentRegex
├── string PaymentNotes
│
├── ─── Dropdowns ────────────────────────────────────────────────────────────
├── ObservableCollection<ReportType> AvailableReportTypes   ← populated from enum values
├── ObservableCollection<TaxpayerProfileDto> AvailableProfiles
├── ObservableCollection<MailboxDto> AvailableMailboxes
│
├── ─── State ────────────────────────────────────────────────────────────────
├── bool IsEditMode      ← false = "new importer mode"
├── bool IsLoading
├── string? ErrorMessage
├── string? SuccessMessage
│
└── ─── Commands ─────────────────────────────────────────────────────────────
    ├── ReactiveCommand AddNewCommand   ← always enabled; clears form; adds unsaved item
    ├── ReactiveCommand SaveCommand     ← canExecute: DisplayName non-empty
    └── ReactiveCommand DeleteCommand  ← canExecute: SelectedImporter != null && IsEditMode

ImporterItemViewModel (ReactiveObject)
├── Guid Id
├── string DisplayName    ← primary label in ListBox
├── string SubTitle       ← ReportType.ToDisplayString()
└── bool IsNew            ← true for unsaved placeholder
```
