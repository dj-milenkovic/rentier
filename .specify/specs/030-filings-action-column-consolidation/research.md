# Research: Filings — Action Column Consolidation & Icon-Only Buttons

**Feature**: 030-filings-action-column-consolidation  
**Date**: 2025-07-17

## R-001: Icon-Only Buttons in Avalonia DataGrid — Icon Source Strategy

### Context
The feature requires three icon-only buttons (Advance Status, Export XML, Delete) in a consolidated Actions column. The project currently uses no icon library — only a single PNG app icon (`avares://Rentier.Desktop/Assets/Icons/app-icon.png`). Buttons currently use text labels from `Strings.resx`.

### Decision: Use Avalonia Built-In PathIcon with Inline Geometry Data

Use `PathIcon` with SVG path data (`Data` attribute) directly in AXAML. This uses Avalonia's native `StreamGeometry` rendering — no external package needed.

### Rationale
- **Zero dependencies**: No NuGet package required. Avalonia's `PathIcon` and `StreamGeometry` are built-in.
- **Scalable**: Vector paths render crisply at any DPI, unlike PNG assets.
- **Lightweight**: Three small path strings in AXAML; no bundled font or asset file.
- **Consistent with Fluent theme**: `PathIcon` integrates with Avalonia's Fluent theme and responds to `Foreground` styling.
- **Simple maintenance**: Icon paths are directly embedded in the button template; no icon font mapping table.

### Alternatives Considered
| Alternative | Rejected Because |
|---|---|
| FluentAvalonia Icons package (`FluentAvaloniaUI`) | Adds a large dependency for only 3 icons; the project doesn't use Fluent icons elsewhere |
| Material.Icons.Avalonia | External NuGet dependency; overkill for 3 icons |
| PNG icon assets | Not scalable across DPI; requires multiple resolutions; already avoided by the project |
| Unicode emoji/symbols | Inconsistent rendering across platforms (Windows vs macOS) |

### Icon Path Sources
Standard icon paths can be sourced from open-source icon sets (MIT/Apache-licensed):
- **Advance Status**: Forward/chevron-right icon (→ or ▶) indicating state progression
- **Export XML**: Download/file-export icon indicating file output
- **Delete**: Trash/bin icon indicating deletion

---

## R-002: Tooltip Implementation in Avalonia DataGrid Cell Templates

### Context
The feature requires tooltips on all three icon buttons. The project has one existing tooltip usage in `ReportsView.axaml` using `ToolTip.Tip` attached property binding.

### Decision: Use `ToolTip.Tip` Attached Property with Binding

Apply `ToolTip.Tip` directly on each `Button` element inside the `DataGridTemplateColumn.CellTemplate`. For the advance-status button, bind to a computed property on `FilingRowViewModel` that returns the dynamic tooltip text (e.g., "Mark as Filed"). For export and delete, use static resource strings.

### Rationale
- **Consistent**: Matches the existing pattern in `ReportsView.axaml`.
- **Native**: `ToolTip.Tip` is built into Avalonia; no additional configuration needed.
- **Dynamic**: Binding to a ViewModel property allows the advance-status tooltip to change based on filing status.
- **Accessible**: Avalonia tooltips are announced by screen readers on supported platforms.

### Alternatives Considered
| Alternative | Rejected Because |
|---|---|
| Custom popup overlay | Over-engineered for simple tooltip text; not standard |
| `AutomationProperties.HelpText` only | Not visible to mouse users; tooltips are a visual requirement |

---

## R-003: Advance Status Button — Command Binding Strategy (Replacing Code-Behind)

### Context
FR-017 requires the advance-status button to use a direct command binding instead of the existing `StatusComboBox_SelectionChanged` code-behind event handler. The current code-behind extracts the row context and status, then calls `ViewModel?.AdvanceStatusCommand.Execute((row.Id, newStatus)).Subscribe()`.

### Decision: Bind Button Command Directly to FilingsViewModel via RelativeSource

Use the button's `Command` property with a `RelativeSource` binding to the parent `FilingsViewModel`. The `CommandParameter` is a `MultiBinding` or the button binds to a per-row command on `FilingRowViewModel` that wraps the parent ViewModel's command with the correct parameters.

**Preferred approach**: Add an `AdvanceStatusCommand` property to `FilingRowViewModel` that is initialized with a reference to the parent ViewModel's command, pre-parameterized with the row's `Id` and first available next status. This avoids complex AXAML multi-bindings and keeps the view simple.

### Rationale
- **Clean**: Eliminates the code-behind event handler entirely (FR-016, FR-017).
- **Testable**: The per-row command is a standard `ReactiveCommand` that can be unit-tested.
- **Simple AXAML**: `Command="{Binding AdvanceStatusCommand}"` — no `RelativeSource` tricks or multi-bindings.
- **Consistent**: Export and Delete buttons can follow the same pattern, eliminating ALL code-behind event handlers for the three action buttons.

### Alternatives Considered
| Alternative | Rejected Because |
|---|---|
| `RelativeSource` binding to parent ViewModel | Complex AXAML; `FindAncestor` in DataGrid cell templates is fragile in Avalonia |
| Keep code-behind for advance-status only | Violates FR-017; missed opportunity to simplify all three buttons |
| `CommandParameter` with `MultiBinding` | Multi-bindings are verbose and harder to test |

### Implementation Note
The `FilingRowViewModel` will receive a delegate or the parent ViewModel's command at construction time. This means the `From(FilingRowDto)` factory method gains a parameter for the parent commands. This is acceptable because row VMs are always created inside `FilingsViewModel.LoadPageAsync`.

---

## R-004: Destructive Button Styling in Avalonia Fluent Theme

### Context
FR-012 requires the delete button to use a "destructive visual style". Currently, the bulk-delete button uses `Foreground="Red"` inline. No reusable danger/destructive button style exists in the project.

### Decision: Apply Inline `Foreground="Red"` on Delete Icon Button

For this feature, apply `Foreground="Red"` on the delete button's `PathIcon` (or the button itself) to tint the trash icon red. This matches the existing pattern used by the bulk-delete button.

### Rationale
- **Consistent**: Matches the project's existing approach for destructive actions (`Foreground="Red"` on bulk delete button, line 42 of FilingsView.axaml).
- **Minimal scope**: Creating a reusable `DangerButton` style class is out of scope for this feature. The feature spec only requires the delete button to be visually distinct.
- **Sufficient contrast**: A red icon on the default Fluent button background provides clear visual distinction from the neutral advance-status and export buttons.

### Alternatives Considered
| Alternative | Rejected Because |
|---|---|
| Custom `DangerButton` style class | Out of scope; over-engineering for one button; can be refactored later |
| Red background button | Too aggressive for an inline DataGrid cell; doesn't match Fluent theme conventions |
| Icon-only distinction (different icon) | Already implemented via different icon shape; color reinforces destructiveness |

---

## R-005: Disabling Advance-Status Button for Terminal Status

### Context
FR-006 and FR-014 require the advance-status button to be disabled when no valid next status exists (terminal state = Paid). The current `FilingRowViewModel.AvailableNextStatuses` returns an empty list for `Paid`.

### Decision: Use `CanExecute` Observable Based on `AvailableNextStatuses.Count > 0`

The per-row `AdvanceStatusCommand` will be created with a `canExecute` parameter that checks whether the row has any available next statuses. Since `FilingRowViewModel` properties are immutable (set at construction), this is a constant observable.

### Rationale
- **Native ReactiveUI**: `canExecute` naturally disables the button via the command binding.
- **No extra AXAML**: No need for `IsEnabled` binding or converter — ReactiveCommand handles it.
- **Testable**: Unit tests can verify `CanExecute` emits `false` for Paid filings.

### Alternatives Considered
| Alternative | Rejected Because |
|---|---|
| `IsEnabled="{Binding HasNextStatus}"` in AXAML | Redundant with command `canExecute`; two sources of truth |
| Hide the button entirely for terminal status | Spec requires the button to be visible but disabled (FR-014) |

---

## R-006: Tooltip Behavior for Disabled Advance-Status Button

### Context
Acceptance scenario 5.4 states: "When the user hovers over the disabled advance-status button, no tooltip is displayed (or the tooltip indicates no further status transitions are available)."

### Decision: Show a Static Tooltip "No further transitions" on Disabled Button

When the advance-status button is disabled (terminal status), display a tooltip indicating there are no further transitions. This is more user-friendly than hiding the tooltip entirely.

### Rationale
- **Discoverable**: Users can understand why the button is disabled without guessing.
- **Avalonia default**: Avalonia shows tooltips on disabled controls by default, so no extra configuration is needed.
- **Simpler implementation**: A single computed property handles both enabled and disabled tooltip text.

### Alternatives Considered
| Alternative | Rejected Because |
|---|---|
| Hide tooltip entirely on disabled button | Requires overriding Avalonia's default tooltip behavior; less user-friendly |
| Show "Paid — no further statuses" | Redundant with the visible status badge |
