# Data Model: Filings Page Filter State & Status Visibility

**Feature**: `001-filings-filter-status`
**Date**: 2025-07-15

## Overview

This feature makes **no changes** to the Domain, Application, or Infrastructure layers. All data model changes are confined to the Desktop (presentation) layer — specifically computed display properties on an existing ViewModel and a new Avalonia value converter.

## Existing Entities (Unchanged)

### `FilingStatus` Enum — `Rentier.Domain.Entities.Filing.cs`

```csharp
public enum FilingStatus
{
    Init = 0,
    Filed = 1,
    Paid = 2
}
```

**No changes.** The enum remains the source of truth for filing lifecycle states. Used as input to both the badge label (via `ToDisplayString()`) and badge colour (via new converter).

### `Filing` Entity — `Rentier.Domain.Entities.Filing.cs`

**No changes.** The entity's `Status` property (type `FilingStatus`) is read by the Application layer query and projected into `FilingRowDto`.

### `FilingRowDto` — `Rentier.Application.DTOs.FilingRowDto.cs`

```csharp
public sealed record FilingRowDto(
    Guid Id,
    FilingStatus Status,
    IncomeType IncomeType,
    string PayingEntity,
    DateOnly FilingDeadline,
    decimal TaxPayable,
    string? PaymentReference);
```

**No changes.** The existing `Status` field provides all the data needed for the badge.

## Modified Entities

### `FilingRowViewModel` — `Rentier.Desktop.ViewModels.FilingRowViewModel.cs`

**Change**: Add one new computed property for testability.

| Property | Type | Source | Purpose |
|----------|------|--------|---------|
| `StatusDisplayText` | `string` | `Status.ToDisplayString()` | Localised label for the status badge. Testable without XAML. |

**New property**:
```csharp
/// <summary>Localised display text for the status badge.</summary>
public string StatusDisplayText => Status.ToDisplayString();
```

**Rationale**: While the existing `FilingStatusDisplayConverter` could provide this text directly in XAML, exposing it as a ViewModel property enables unit testing of the label mapping without requiring Avalonia runtime. This follows the project's existing pattern where `DeadlineDisplay` and `TaxPayableDisplay` are computed display properties on the ViewModel.

**Existing properties** (unchanged):

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `Guid` | Filing identifier |
| `Status` | `FilingStatus` | Filing lifecycle state |
| `IncomeType` | `IncomeType` | Dividend or Interest |
| `PayingEntity` | `string` | Name of paying entity |
| `FilingDeadline` | `DateOnly` | Deadline date |
| `TaxPayable` | `decimal` | Tax amount in RSD |
| `PaymentReference` | `string?` | Payment ref (nullable) |
| `DeadlineDisplay` | `string` | Formatted deadline |
| `TaxPayableDisplay` | `string` | Formatted tax amount |
| `IsPaymentReferenceEditable` | `bool` | True when Status == Filed |
| `AvailableNextStatuses` | `IReadOnlyList<FilingStatus>` | Valid next statuses |

## New Types

### `FilingStatusToBadgeBrushConverter` — `Rentier.Desktop.Converters.FilingStatusToBadgeBrushConverter.cs`

**Type**: Avalonia `IValueConverter` (static singleton)
**Purpose**: Maps `FilingStatus` → `IBrush` for badge background and foreground colours.

| Input | Parameter | Output |
|-------|-----------|--------|
| `FilingStatus.Init` | `"Background"` | `SolidColorBrush(#D4A017)` — Amber |
| `FilingStatus.Filed` | `"Background"` | `SolidColorBrush(#0063B1)` — Blue |
| `FilingStatus.Paid` | `"Background"` | `SolidColorBrush(#107C10)` — Green |
| Any `FilingStatus` | `"Foreground"` | `SolidColorBrush(#FFFFFF)` — White |
| Other/null | Any | `Brushes.Transparent` |

**Pattern**: Implements `IValueConverter` directly (not `FuncValueConverter`) because parameter support is needed. Follows the same static singleton pattern as `DateOnlyToStringConverter`.

## Relationships

```text
FilingStatus (Domain enum)
    |
    +-- Filing.Status (Domain entity property, unchanged)
    |
    +-- FilingRowDto.Status (Application DTO, unchanged)
    |
    +-- FilingRowViewModel.Status (Desktop VM property, unchanged)
    |   |
    |   +-- FilingRowViewModel.StatusDisplayText (NEW, computed from Status.ToDisplayString())
    |   |
    |   +-- FilingStatusToBadgeBrushConverter (NEW, converts Status to IBrush for badge)
    |   |
    |   +-- FilingStatusDisplayConverter (EXISTING, converts Status to display string for ComboBox)
    |
    +-- FilingsView.axaml badge column (NEW, renders Border+TextBlock using above)
```

## Validation Rules

No new validation rules. The `FilingStatus` enum constrains values to `Init`, `Filed`, `Paid` at the Domain level. The converter handles all three values and returns a sensible default (`Brushes.Transparent`) for any unexpected input.

## State Transitions

No new state transitions. The existing `Init → Filed → Paid` state machine (enforced in `Filing.AdvanceStatus()`) is unchanged. The badge is a read-only visual representation of the current state — it does not participate in state transitions.
