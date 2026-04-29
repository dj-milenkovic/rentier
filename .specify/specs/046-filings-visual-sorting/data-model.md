# Data Model: Filings Visual Sorting

This feature is UI-only. No new entities, value objects, or database changes are required. The data model impact is limited to ViewModel property changes.

## ViewModel State Changes

### FilingsViewModel — Modified Properties

| Property | Current Type | New Type | Change Reason |
|---|---|---|---|
| `SortColumn` | `FilingSortColumn` | `FilingSortColumn?` | Nullable to represent "unsorted" (no active sort column) |
| `_sortColumn` | `FilingSortColumn` (default: `FilingDeadline`) | `FilingSortColumn?` (default: `FilingSortColumn.FilingDeadline`) | Initial sort remains FilingDeadline descending per spec assumption |
| `_showAll` | `bool` (default: `false`) | `bool` (default: `true`) | FR-008: Show all filings by default |
| `SortIndicatorDisplay` | `string` (computed) | **REMOVED** | FR-009: Replaced by column header arrows |

### FilingsViewModel — New Properties

None. The sort arrow state is derived in the View from existing `SortColumn` and `SortDescending` properties via bindings and converters.

### ApplySortCommand — Modified Behavior

Current cycle (same column): `asc → desc → asc → desc → ...`
New cycle (same column): `asc → desc → unsorted → asc → desc → unsorted → ...`

When `SortColumn` is `null` (unsorted), the query omits the sort parameter and the database returns default ordering.

## Converters Needed

### SortArrowVisibilityConverter (new, IMultiValueConverter)

Determines whether a sort arrow should be visible for a given column.

**Inputs**: `SortColumn` (FilingSortColumn?), column tag (string)
**Output**: `bool` — true if `SortColumn` matches the column tag

### SortArrowDirectionConverter (new, IMultiValueConverter)

Determines which arrow to show (up or down).

**Inputs**: `SortDescending` (bool), `SortColumn` (FilingSortColumn?), column tag (string)
**Output**: `StreamGeometry` — ascending or descending arrow path

*Alternative*: A single converter returning the appropriate `StreamGeometry` (ascending arrow, descending arrow, or null) could combine both responsibilities. This is the preferred approach to reduce binding complexity.

### SortArrowDataConverter (preferred single converter)

**Input**: `SortColumn` (FilingSortColumn?), `SortDescending` (bool), column tag (string)
**Output**: `StreamGeometry?` — ascending arrow when sorted asc, descending arrow when sorted desc, null when unsorted or different column

## Resource Strings

### Removed
- `Filings_Filter_Unpaid` — no longer displayed (controls removed)
- `Filings_Filter_All` — no longer displayed (controls removed)

*Note*: String keys are kept in `.resx` for backward compatibility; they are simply no longer referenced in AXAML.

### No New Strings Required
Sort arrows are visual indicators (icons), not text.
