# Research: Feature 005 — Importer Configuration

**Phase**: 0 — Research & Decision Log  
**Date**: 2026-04-06  
**Status**: Complete — all decisions resolved

---

## RES-01: Shadow FK vs Navigation Properties on `Importer` entity

### Decision
Use **explicit scalar FK properties** (`Guid? TaxpayerProfileId`, `Guid? MailboxId`) with **no navigation properties**. Configure EF via `HasOne<T>().WithMany().HasForeignKey(i => i.Prop)` — no nav prop needed for FK to work.

The term "shadow FK" in the clarify doc was slightly imprecise: a true EF shadow property is one that is **not** on the CLR class at all. Since the FK Guids are real properties on `Importer`, the correct term is "no-navigation FK with explicit scalar property". The EF call pattern is identical to the shadow-prop pattern, minus the string name.

### Rationale
- `Importer` is **not** an aggregate root of `TaxpayerProfile` or `Mailbox`. Eager-loading them through navigation would couple aggregates at the ORM level.
- The Application layer resolves each aggregate independently (via `GetTaxpayerProfileQuery`, `GetMailboxesQuery`). The `Importer` entity needs only the FK Guid to identify the association.
- `DeleteBehavior.SetNull` on an optional FK is straightforward with EF and SQLite (SQLite supports `ON DELETE SET NULL` since version 3.25 — EF generates the correct DDL).
- This pattern was already established in Feature 004 and is consistent with the project's cross-aggregate boundary discipline.

### Alternatives Considered
| Alternative | Rejected Because |
|---|---|
| Navigation properties (`Mailbox? Mailbox`, `TaxpayerProfile? Profile`) | Couples aggregates at ORM level; forces eager-load decisions into repository layer; violates aggregate-boundary discipline |
| True shadow properties (no CLR prop, only `"MailboxId"` string in fluent config) | Adds complexity with no benefit — Application layer needs the Guid values anyway, so scalar properties are cleaner |
| No FK constraints at all (only `Guid?` columns) | Loses referential integrity; `DeleteBehavior.SetNull` DDL never generated; orphan records could silently point to non-existent entities |

---

## RES-02: `ReportType` Enum — EF Storage Strategy

### Decision
Store `ReportType` as **`int`** in SQLite (default EF behaviour for enums). No custom value converter needed. Only one value (`IbkrCsv = 0`) in this feature.

### Rationale
- EF Core 8 maps .NET enums to `INTEGER` by default. This is correct for an enum that will grow incrementally (future: `IbkrXml`, `DeGiroCsv`, etc.).
- Integer storage is compact, indexed efficiently, and requires no migration changes when new enum values are added — only when the domain adds a new variant.
- String storage (`HasConversion<string>()`) is human-readable in the DB but requires a column migration or a new member addition to the conversion table when enum variants are added.
- The `ReportType` display string for the UI is handled separately by `ReportTypeExtensions.ToDisplayString()` in the Desktop layer — the DB storage form has no user-visible impact.

### Alternatives Considered
| Alternative | Rejected Because |
|---|---|
| Store as `string` | Requires conversion config; no benefit for internal enum; slightly larger column |
| Custom check constraint | Over-engineering; EF handles new variants gracefully without it |

---

## RES-03: Regex Validation in Handler vs Domain Entity

### Decision
**Regex validation lives in the Application command handler**, not in the `Importer` domain entity. `AttachmentRegex` validity is checked via `try { _ = new Regex(value); } catch (ArgumentException) { ... }` inside `AddImporterCommandHandler` and `UpdateImporterCommandHandler`.

### Rationale
- The `Rentier.Domain` project MUST NOT reference I/O or platform packages. `System.Text.RegularExpressions` is technically a BCL namespace, but `Regex` compilation can have side effects (cache, JIT) and its error semantics (throwing `ArgumentException`) are a runtime concern rather than a pure domain invariant.
- The domain's job is to enforce business rules (e.g., "DisplayName is required"). Whether a string is a valid .NET regex pattern is a _syntactic compatibility_ check against a .NET API — exactly the kind of infrastructure concern the handler layer should own.
- This pattern also makes the domain entity simple and fast (no try/catch in constructor or `UpdateDetails`).
- Consistent with the clarify decision AD-1 and Q4.

### Alternatives Considered
| Alternative | Rejected Because |
|---|---|
| Validate in `Importer.Create(...)` or `UpdateDetails(...)` | Imports BCL Regex into Domain; throws from domain constructor which complicates handler error model |
| Validate in ViewModel before dispatch | Validation would be duplicated across VM and handler; handler must be independently correct regardless of VM |
| Validate via a separate `ValidateRegexQuery` dispatched from VM | Over-engineering for a single field check |

---

## RES-04: ComboBox Binding Patterns in Avalonia for Optional FK Dropdowns

### Decision
Bind `ComboBox` to `ObservableCollection<TDto>` on the ViewModel. Use `SelectedItem` bound to a nullable DTO property (`TaxpayerProfileDto?`, `MailboxDto?`). Provide an explicit `null` / "None" option using a **nullable sentinel** pattern: prepend `null` to the collection is **not** directly supported in Avalonia without a wrapper — instead use a dedicated `NullableItem<T>` wrapper or rely on the fact that Avalonia's `ComboBox` returns `null` from `SelectedItem` when no item is selected.

**Chosen approach**: Keep collections as `ObservableCollection<TaxpayerProfileDto>` and `ObservableCollection<MailboxDto>` (no null entries). When no profile/mailbox exists (empty collection), the ComboBox is simply empty and `SelectedItem` stays null. The VM property `SelectedTaxpayerProfile` and `SelectedMailbox` are typed as `TaxpayerProfileDto?` and `MailboxDto?`. On save, extract `.Id` when non-null, else pass `null` to the command.

Use `x:CompileBindings="False"` to avoid compiled binding issues with nullable DTO types and generic `ObservableCollection`.

### Rationale
- Feature 004's `MailboxSettingsViewModel` uses the same observable-collection + nullable-DTO pattern for the single profile; this feature extends that pattern to two dropdowns.
- Avalonia `ComboBox.SelectedItem` naturally returns `null` when nothing is selected, so a "None" sentinel is not required — just leave the combo unselected.
- `x:CompileBindings="False"` is already established across Settings views and avoids x:DataType annotation noise on complex generic types.

### Loading Pattern (on `WhenActivated`)
```
1. Dispatch GetImportersQuery → ImporterItems (ObservableCollection<ImporterItemViewModel>)
2. Dispatch GetTaxpayerProfileQuery → if dto != null: AvailableProfiles = [dto] else AvailableProfiles.Clear()
3. Dispatch GetMailboxesQuery → AvailableMailboxes = result list
```
Reload order: importers first (populates list), then profiles+mailboxes in parallel (fills dropdowns before user selects).

### Post-Save State (same as Feature 004)
- After **Add** success: reload importers → find by returned `Guid` → set as `SelectedImporter` → `IsEditMode = true`.
- After **Update** success: reload importers → restore selection by saved `Id`.
- After **Delete** success: reload importers → clear form → `SelectedImporter = null`.

### Alternatives Considered
| Alternative | Rejected Because |
|---|---|
| Null sentinel object in collection | Requires custom equals logic and display template; more complex than nullable SelectedItem |
| `CompiledBinding` for performance | Causes compilation errors with nullable generics; unnecessary for settings screens |
| Load dropdowns lazily (on ComboBox open) | Complicates activation logic; no performance benefit for small lists |
