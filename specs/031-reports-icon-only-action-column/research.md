# Research: Reports — Icon-Only Action Column

**Feature**: 031-reports-icon-only-action-column  
**Date**: 2025-07-17  
**Status**: Complete

## Research Tasks

### R1: Icon Resource Approach — Reuse from Feature 030

**Context**: Feature 031 depends on the icon infrastructure established by feature 030 (Filings Action Column Consolidation). Need to determine what icon resources 030 provides and what 031 needs to add.

**Decision**: Reuse the `Icons.axaml` resource dictionary and `TrashIcon` StreamGeometry established by feature 030. Add one new icon resource (`ViewFilingsIcon`) to the same dictionary.

**Rationale**:
- Feature 030 introduces a shared `Icons.axaml` resource dictionary (in `src/Rentier.Desktop/Resources/`) containing `StreamGeometry` icon definitions for the Filings Actions column (TrashIcon, AdvanceStatusIcon, ExportIcon).
- Feature 030 registers `Icons.axaml` in `App.axaml` as a merged resource, making all icons application-wide.
- Feature 031 only needs two icons: a **trash icon** (reuse `TrashIcon` from 030) and a **list/arrow icon** for "View Filings" (new `ViewFilingsIcon`).
- Using `StreamGeometry` path data is the established pattern — no external icon library (e.g., FluentIcons NuGet) is needed.
- The 16×16 logical-pixel icon size convention from feature 030 applies here.

**Alternatives Considered**:
| Alternative | Rejected Because |
|---|---|
| FluentIcons NuGet package | Adds a dependency for only 2 icons; inconsistent with 030's approach |
| Unicode symbols (e.g., 🗑️, →) | Inconsistent rendering across platforms; not professional for a desktop app |
| Avalonia built-in PathIcon resources | Avalonia FluentTheme does not ship user-facing action icon geometries |

---

### R2: Icon Selection for "View Filings" Action

**Context**: The spec calls for a "list/arrow icon" to represent the "View Filings" action. Need to select appropriate SVG path data.

**Decision**: Use a right-arrow/chevron-right icon (`ViewFilingsIcon`) to represent navigation to linked filings. This matches the semantic "go to filings" action and is universally understood as a navigation/drill-down action.

**Rationale**:
- "View Filings" is a navigation action (it invokes `_navigateToFilings(id)` on the ViewModel), not a data-display action.
- A right-arrow/chevron icon clearly communicates "navigate to" or "drill into" across all user demographics.
- The spec mentions "list/arrow icon" — a chevron-right satisfies the "arrow" intent and is more compact than a list icon at 16×16.
- The Filings page sidebar navigation already uses directional metaphors, making this consistent.

**Alternatives Considered**:
| Alternative | Rejected Because |
|---|---|
| List icon (three horizontal lines) | Ambiguous at 16×16; could be confused with a menu/hamburger icon |
| External link icon (box + arrow) | Implies opening in a new window/tab, which is incorrect — this is in-app navigation |
| Eye icon (view/preview) | Suggests read-only preview, not navigation to a related entity list |

---

### R3: Tooltip Text Localisation Strategy

**Context**: Feature spec requires tooltip text ("View linked filings", "Delete report") defined as localised resource strings (FR-010).

**Decision**: Add two new string resources to `Strings.resx` following the existing naming convention: `Reports_Tooltip_ViewFilings` and `Reports_Tooltip_Delete`.

**Rationale**:
- Existing string resources follow the pattern `{Page}_{Category}_{Action}` (e.g., `Reports_Button_ViewFilings`, `Reports_Col_Name`).
- Tooltip strings are a new category, so `Reports_Tooltip_*` is a natural extension.
- The `Strings.Designer.cs` auto-generates strongly-typed accessors, keeping bindings type-safe.
- Tooltip text is bound via `ToolTip.Tip="{x:Static res:Strings.Reports_Tooltip_ViewFilings}"` — same binding approach used throughout the app.

**Alternatives Considered**:
| Alternative | Rejected Because |
|---|---|
| Inline string in AXAML | Violates constitution coding standard: "User-visible strings MUST be in Resources/Strings.resx" |
| Shared tooltip resources across pages | Feature 030 and 031 have different tooltip wording ("Delete filing" vs "Delete report"); page-scoped strings are more maintainable |

---

### R4: AXAML Button Pattern — Following Feature 030

**Context**: Need to define the exact AXAML markup for icon-only buttons in the Reports Action column, consistent with feature 030's established pattern.

**Decision**: Use the same button-with-PathIcon-content pattern from feature 030:
```xml
<Button ToolTip.Tip="{x:Static res:Strings.Reports_Tooltip_ViewFilings}"
        Command="{Binding DataContext.ViewFilingsCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
        CommandParameter="{Binding Id}"
        Padding="4" Background="Transparent" BorderThickness="0">
  <PathIcon Data="{StaticResource ViewFilingsIcon}" Width="16" Height="16" />
</Button>
```

**Rationale**:
- Avalonia's `PathIcon` control renders `StreamGeometry` data and automatically inherits the `Foreground` colour from its parent, making destructive-action styling straightforward (`Foreground="Red"` on the delete button).
- `Padding="4" Background="Transparent" BorderThickness="0"` creates a compact, chromeless button that visually matches feature 030.
- The existing `Command` and `CommandParameter` bindings are preserved identically — only the `Content` changes from text to `PathIcon`.
- The `StackPanel Orientation="Horizontal" Spacing="4" Margin="4,2"` wrapper from the current markup can be preserved as-is.

**Alternatives Considered**:
| Alternative | Rejected Because |
|---|---|
| DrawingImage with DrawingGroup | Overly complex for single-colour icons; PathIcon is the Avalonia standard |
| Image control with PNG assets | Doesn't scale with DPI; doesn't inherit Foreground colour for destructive styling |
| TextBlock with Unicode glyphs | Platform-dependent rendering; no Foreground colour inheritance for consistent styling |

---

### R5: Destructive Action Styling

**Context**: The delete icon button must use a red foreground (FR-005) consistent with the existing destructive styling in the app.

**Decision**: Apply `Foreground="Red"` directly to the delete button, matching the existing pattern used throughout the app (e.g., bulk-delete button in ReportsView.axaml line 41, error text across all views).

**Rationale**:
- The app consistently uses `Foreground="Red"` for destructive actions and error states (confirmed in 11 instances across DashboardView, FilingsView, ReportsView, SettingsViews, SyncView).
- Feature 030 uses the same `Foreground="Red"` on its delete icon button.
- No theme-level destructive colour token exists — inline `Red` is the established convention.

**Alternatives Considered**:
| Alternative | Rejected Because |
|---|---|
| Avalonia theme DynamicResource for destructive colour | No such resource exists in FluentTheme; introducing one is out of scope |
| Crimson or DarkRed | Inconsistent with existing `Red` usage across the app |

---

### R6: Dependency on Feature 030 — What Must Exist Before 031

**Context**: The spec assumes feature 030 is completed first. Need to enumerate what 031 depends on from 030.

**Decision**: Feature 031 depends on the following artifacts from feature 030:

| Artifact | What 030 Provides | What 031 Uses |
|---|---|---|
| `Resources/Icons.axaml` | Resource dictionary with `StreamGeometry` icon definitions | Adds `ViewFilingsIcon`; reuses `TrashIcon` |
| `App.axaml` inclusion | `<ResourceInclude Source="avares://Rentier.Desktop/Resources/Icons.axaml" />` | Already included; no change needed |
| Icon-button AXAML pattern | `PathIcon` inside chromeless `Button` with `ToolTip.Tip` | Follows identical pattern |
| 16×16 icon size convention | Established in Filings Actions column | Reused for Reports Actions column |

**Rationale**: This explicit dependency list ensures 031 implementation can validate that all prerequisites are in place before starting. If 030's icon approach changes during its implementation, 031 must adapt accordingly.

**Risk Mitigation**: If feature 030 is not yet merged when 031 begins, the implementer should branch from the 030 branch (or `feat/027-031-ux-improvements` integration branch) to access the icon infrastructure.
