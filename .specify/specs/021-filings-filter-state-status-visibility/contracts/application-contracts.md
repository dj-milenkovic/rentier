# Application Contracts: Filings Page Filter State & Status Visibility

**Feature**: `001-filings-filter-status`
**Date**: 2025-07-15

## Overview

This feature is entirely within the Desktop (presentation) layer and introduces **no new external interfaces**. No Application-layer commands, queries, or repository contracts are added or modified. The contracts below document the internal presentation-layer interfaces for clarity and traceability.

## Existing Contracts (Unchanged)

### Query: `GetFilingsQuery`

The existing query remains the sole data source for the Filings page. No changes to its signature or behaviour.

```
Input:  GetFilingsQuery(FilingFilterMode filter, int page, int pageSize, Guid? reportIdFilter)
Output: Result<FilingsPageResult, Error>
```

### Command: `UpdateFilingStatusCommand`

The existing command remains the sole mechanism for advancing filing status. No changes.

```
Input:  UpdateFilingStatusCommand(Guid filingId, FilingStatus newStatus)
Output: Result<VoidResult, Error>
```

## New Presentation-Layer Contracts

### `FilingRowViewModel.StatusDisplayText` (Property)

**Contract**: Given a `FilingRowViewModel` with status `S`, `StatusDisplayText` MUST return the same string as `FilingStatusExtensions.ToDisplayString(S)`.

| Status | Expected Output (en) |
|--------|---------------------|
| `Init` | `"Init"` |
| `Filed` | `"Filed"` |
| `Paid` | `"Paid"` |

Localised — actual values depend on the current resource culture.

### `FilingStatusToBadgeBrushConverter` (IValueConverter)

**Contract**: `Convert(value, targetType, parameter, culture)` where `value` is `FilingStatus` and `parameter` is a string.

| value | parameter | Returns |
|-------|-----------|---------|
| `FilingStatus.Init` | `"Background"` | `SolidColorBrush(Color.Parse("#D4A017"))` |
| `FilingStatus.Filed` | `"Background"` | `SolidColorBrush(Color.Parse("#0063B1"))` |
| `FilingStatus.Paid` | `"Background"` | `SolidColorBrush(Color.Parse("#107C10"))` |
| Any `FilingStatus` | `"Foreground"` | `SolidColorBrush(Colors.White)` |
| `null` or non-`FilingStatus` | Any | `Brushes.Transparent` |

`ConvertBack` is not supported and throws `NotSupportedException`.

### View Contract: Filter Toggle Buttons

**Visual Contract**:
- Exactly two ToggleButtons in the filter bar: "Unpaid" and "All"
- At any time, exactly one button MUST be in the `:checked` visual state
- The checked button uses `AccentButtonBackground` / `AccentButtonForeground` resources
- The unchecked button uses default FluentTheme ToggleButton styling
- Toggling updates `FilingsViewModel.ShowAll` which triggers `LoadPageCommand`

### View Contract: Status Badge Column

**Visual Contract**:
- Each DataGrid row in the Filings list contains a status badge cell
- The badge is rendered as a `Border` (pill shape, `CornerRadius="10"`) containing a `TextBlock`
- Background colour is bound to `Status` via `FilingStatusToBadgeBrushConverter` with parameter `"Background"`
- Text content is bound to `StatusDisplayText`
- Foreground colour is bound to `Status` via `FilingStatusToBadgeBrushConverter` with parameter `"Foreground"`
- The badge is `IsHitTestVisible="False"` — it cannot receive pointer events
- The badge column appears before the existing Status ComboBox column
