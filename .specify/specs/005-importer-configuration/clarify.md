# Feature 005 — Importer Configuration: Clarifications

**Status**: Resolved  
**Date**: 2026-04-06  
**Feature**: Settings → Importers — CRUD for IBKR statement importer configs  
**Method**: Autonomous resolution (all questions answered before specifying)

---

## Resolved Questions

### Q1 — What happens to the existing `FilterExpression` field?

**Decision**: Replace entirely. The `Importer` entity is completely redesigned. `FilterExpression` is removed and replaced by three dedicated filter fields: `FromFilter`, `SubjectFilter`, `AttachmentRegex`. The Importers table has never appeared in any EF migration (migrations 0001–0004 only cover TaxpayerProfiles, PublicHolidays, HolidayYearRange, Mailboxes), so migration 0005 creates the table from scratch — no column rename needed.

---

### Q2 — Are FK references to TaxpayerProfile and Mailbox required or optional?

**Decision**: **Both are optional** (`Guid?` FK, no `NOT NULL` constraint). The user may create an importer before configuring a profile or mailbox. FKs are stored as bare `Guid?` columns — no EF navigation properties (avoids cross-aggregate coupling). EF FK constraint is configured with `DeleteBehavior.SetNull` (on delete of related entity, set FK to null rather than cascade delete).

---

### Q3 — How are TaxpayerProfile and Mailbox loaded for the dropdowns?

**Decision**: The `MailboxSettingsViewModel` pattern is reused:
- Load available profiles via existing `GetTaxpayerProfileQuery` (returns single nullable DTO → wrap in a 0-or-1 collection for ComboBox)
- Load available mailboxes via existing `GetMailboxesQuery` (returns list)
- Both loaded in `WhenActivated` and stored as `ObservableCollection` on the ViewModel
- Selected profile/mailbox tracked as `TaxpayerProfileDto?` and `MailboxDto?` VM properties
- On save: extract `Id` from selected DTO and pass to command

---

### Q4 — Where does regex validation live?

**Decision**: **In the command handler** (Application layer), not in the Domain entity. Regex validity is an infrastructure concern (depends on `System.Text.RegularExpressions`), not a business invariant. Handler tries `new Regex(pattern)` inside try/catch `ArgumentException`; on failure returns `Result.Failure(new Error("INVALID_REGEX", message))`. An empty `AttachmentRegex` is valid (means "accept all attachments").

---

### Q5 — ReportType enum placement

**Decision**: New file `src/Rentier.Domain/Enums/ReportType.cs` in a new `Enums` folder. Only value: `IbkrCsv = 0`. EF stores as `int`. The `Importer` entity has `public ReportType ReportType { get; private set; }` defaulting to `ReportType.IbkrCsv`.

---

### Q6 — Should the Importer have navigation properties to Mailbox and TaxpayerProfile?

**Decision**: **No navigation properties**. Store only `Guid? MailboxId` and `Guid? TaxpayerProfileId` as scalar FK columns. The application layer resolves related entities independently. This prevents circular dependencies between aggregates. EF FK configuration uses the **expression overload** (real CLR property, not shadow property): `HasOne<Mailbox>().WithMany().HasForeignKey(i => i.MailboxId).IsRequired(false).OnDelete(DeleteBehavior.SetNull)` (no nav prop).

---

### Q7 — PaymentNotes constraints

**Decision**: Optional free text, max 4000 characters. Stored as TEXT. Can contain multiline content (newlines preserved). Included verbatim in XML filing generation (future feature).

---

### Q8 — Importer entity: all fields and their types

**Final Importer entity fields:**
| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `Id` | `Guid` | ✅ | Auto-generated |
| `DisplayName` | `string` | ✅ | Non-empty, max 200 |
| `ReportType` | `ReportType` | ✅ | Enum value (default IbkrCsv) |
| `TaxpayerProfileId` | `Guid?` | ❌ | Valid Guid if present |
| `MailboxId` | `Guid?` | ❌ | Valid Guid if present |
| `FromFilter` | `string` | ❌ | Empty = no filter |
| `SubjectFilter` | `string` | ❌ | Empty = no filter |
| `AttachmentRegex` | `string` | ❌ | Valid regex if non-empty (validated in handler) |
| `PaymentNotes` | `string` | ❌ | Max 4000 chars |

All string fields: empty string (not null) as default. Simplifies binding.

---

## Architecture Decisions

### AD-1: Importer entity revision
- Replace existing public constructor (with just `Id`, `DisplayName`, `FilterExpression`) 
- Add `private Importer() { }` for EF hydration
- Add `private set` on all properties
- Add all new fields with defaults
- Static factory `public static Importer Create(string displayName, ReportType reportType = ReportType.IbkrCsv)`
- Mutation method `public void UpdateDetails(...)` with all editable fields

### AD-2: ImporterDto
```csharp
public record ImporterDto(
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

### AD-3: Commands
| Command | Result |
|---------|--------|
| `AddImporterCommand` | `Result<Guid, Error>` |
| `UpdateImporterCommand` | `Result<VoidResult, Error>` |
| `DeleteImporterCommand` | `Result<VoidResult, Error>` |
| `GetImportersQuery` | `Result<IReadOnlyList<ImporterDto>, Error>` |

### AD-4: EF Configuration
- Table: `Importers`
- No navigation properties
- **Real-property FK (no navigation property)** — `TaxpayerProfileId` and `MailboxId` are real `Guid?` CLR
  properties on the entity, so the **expression overload** is used (not the string/shadow-property overload):
  `HasOne<Mailbox>().WithMany().HasForeignKey(i => i.MailboxId).IsRequired(false).OnDelete(DeleteBehavior.SetNull)`
  `HasOne<TaxpayerProfile>().WithMany().HasForeignKey(i => i.TaxpayerProfileId).IsRequired(false).OnDelete(DeleteBehavior.SetNull)`
- `ReportType` stored as `int`
- `AttachmentRegex` max 1000 chars

### AD-5: UI Layout
- Two-panel layout: `ListBox` (left, 250px) + form (right)  
- Form: TextBox for DisplayName, ComboBox for ReportType (shows display name), ComboBox for TaxpayerProfile, ComboBox for Mailbox, TextBox for FromFilter, TextBox for SubjectFilter, TextBox for AttachmentRegex, multi-line TextBox for PaymentNotes
- Toolbar: Add New, Save, Delete (CanExecute = item selected)
- `SettingsViewModel` gets `ImportersTab` as 4th constructor param

### AD-6: IImporterRepository namespace
`Rentier.Application.Repositories` — already exists, use as-is.

### AD-7: DesktopVM dependencies
`ImporterSettingsViewModel` injects:
- `IQueryHandler<GetImportersQuery, ...>` 
- `IQueryHandler<GetTaxpayerProfileQuery, ...>` (for profile dropdown)
- `IQueryHandler<GetMailboxesQuery, ...>` (for mailbox dropdown)
- `ICommandHandler<AddImporterCommand, ...>`
- `ICommandHandler<UpdateImporterCommand, ...>`
- `ICommandHandler<DeleteImporterCommand, ...>`

### AD-8: ReportType display names
Implement a simple extension method `ReportTypeExtensions.ToDisplayString()` in Desktop layer that returns human-readable names (`IbkrCsv` → "IBKR CSV").

---

## Assumptions
1. **No Importers migration yet** — migration 0005 creates the table from scratch.
2. **Migration 0004 creates Mailboxes table** — 0005 depends on it for FK. Both must be applied sequentially. The implement agent must ensure `dotnet ef migrations add 0005_ImporterConfiguration` runs after feature 004's migration is applied (i.e., run `dotnet ef database update` first, or just add the migration — EF will snapshot from current model).
3. **`GetTaxpayerProfileQuery`** returns `Result<TaxpayerProfileDto?, Error>` (single nullable). ViewModel wraps in a `IEnumerable<TaxpayerProfileDto>` of 0 or 1 items.
4. **GetMailboxesQuery** returns `Result<IReadOnlyList<MailboxDto>, Error>` — from feature 004.
5. **VoidResult** (not Unit) for void command results.
6. **RaiseAndSetIfChanged** for all VM properties — no Fody, no CommunityToolkit.
7. **AddTransient** for all services — root ServiceProvider pattern.
8. **`x:CompileBindings="False"`** on view — avoids compiled binding issues with complex types.
9. **Regex validation**: only `AttachmentRegex`; `FromFilter` and `SubjectFilter` are plain strings (not validated as regex).
10. **No importer execution** — this feature configures importers only; no email processing.
11. **SettingsViewModel current signature** after feature 003: `(ProfileSettingsViewModel, HolidaySettingsViewModel)`. After feature 004 will add `MailboxSettingsViewModel`. Feature 005 adds `ImporterSettingsViewModel` as 4th param.
12. **`ReportType` enum** — placed in `Rentier.Domain/Enums/ReportType.cs`.
13. **Feature 004 may still be running** when this spec is written. Implement agent must wait for 004 to complete before running the EF migration (or accept that AppDbContext will have Mailboxes table in model snapshot after 004).
14. **`MailboxDto`** from feature 004 — importer VM depends on it. Implementation must occur after 004 completes.
