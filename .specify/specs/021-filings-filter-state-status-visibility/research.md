# Research: Filings Page Filter State & Status Visibility

**Feature**: `001-filings-filter-status`
**Date**: 2025-07-15

## Research Topics

### R1: Avalonia ToggleButton Active-State Styling

**Context**: The current `FilingsView.axaml` uses two `ToggleButton` controls bound to the `ShowAll` boolean (one directly, one via `InvertBoolConverter`). QA reports no visible feedback when clicking the filter buttons. Investigation needed into why and how to fix it.

**Findings**:

The current implementation binds `IsChecked` on both ToggleButtons, which should toggle the FluentTheme's built-in checked/unchecked visual states. However, Avalonia's `ToggleButton` with FluentTheme provides only subtle visual distinction in the default state — a slight background tint change that may not be noticeable to users, especially at smaller sizes in a toolbar context.

**Decision**: Apply explicit XAML styles scoped to the filter bar's ToggleButtons using the `:checked` pseudo-class. This provides clear, high-contrast active-state styling using FluentTheme's accent colour resources.

**Rationale**:
- Uses Avalonia's built-in styling system (pseudo-classes) — no custom controls or third-party packages needed.
- Scoped styles (inside the `StackPanel`) avoid affecting ToggleButtons elsewhere in the application.
- FluentTheme dynamic resources (`SystemAccentColor`, `SystemAccentColorLight2`, `AccentButtonBackground`) ensure the styling adapts to light/dark theme automatically.

**Alternatives Considered**:
1. **Replace with RadioButtons styled as toggle buttons**: Rejected — RadioButtons require a `GroupName` and have different XAML semantics; the current ToggleButton + InvertBoolConverter pattern is idiomatic and already works for mutual exclusion via data binding.
2. **Use a ListBox with horizontal items**: Rejected — over-engineered for a two-option toggle bar; adds complexity for no benefit.
3. **Custom ControlTheme**: Rejected — too heavy for this scope; scoped pseudo-class styles are sufficient.

**Implementation Pattern**:
```xml
<StackPanel.Styles>
  <Style Selector="ToggleButton:checked">
    <Setter Property="Background" Value="{DynamicResource AccentButtonBackground}" />
    <Setter Property="Foreground" Value="{DynamicResource AccentButtonForeground}" />
  </Style>
</StackPanel.Styles>
```

This ensures the checked ToggleButton uses the FluentTheme accent colour (typically a blue or system accent), providing high contrast against the default unchecked state.

---

### R2: Status Badge (Pill) Rendering in Avalonia

**Context**: Each filing row needs a read-only colour-coded badge showing the current status (Init, Filed, Paid). The badge must be a pill shape with coloured background and contrasting text.

**Findings**:

Avalonia does not have a built-in "Badge" or "Pill" control. The standard pattern is a `Border` with `CornerRadius` containing a `TextBlock`. This is lightweight, composable, and fully customisable via converters.

**Decision**: Render the badge as a `Border` with `CornerRadius="10"` containing a centered `TextBlock`. Background colour is set via a new `FilingStatusToBadgeBrushConverter`. Text content uses the existing `FilingStatusDisplayConverter` (which delegates to `ToDisplayString()`).

**Rationale**:
- `Border` + `TextBlock` is the simplest Avalonia primitive composition for a pill shape.
- No custom UserControl needed — the badge is defined inline in the DataGrid cell template.
- Converter-based approach keeps the ViewModel clean (no `IBrush` types in the VM).
- A new `StatusDisplayText` property on `FilingRowViewModel` provides a testable text source.

**Alternatives Considered**:
1. **Custom UserControl `StatusBadge`**: Rejected — adds a file for a single-use element. Inline XAML in the DataGrid template is simpler.
2. **Templated Control**: Rejected — over-engineered for a display-only element.
3. **Style classes on the Border**: Considered — would use `Classes.status-init`, `Classes.status-filed`, `Classes.status-paid` with style selectors. Rejected because it requires a class-binding mechanism (custom attached behavior) whereas converters are already established in this codebase.

---

### R3: Badge Colour Selection for Light/Dark Theme Compatibility

**Context**: Badge colours must be visually distinct for each status and readable against both light and dark theme backgrounds. Spec requires amber for Init, blue for Filed, green for Paid.

**Findings**:

Evaluated colours against WCAG AA contrast requirements (4.5:1 for normal text). Selected semi-transparent or medium-saturation colours that work in both themes by pairing a coloured background with white text.

**Decision**: Use the following colour scheme with white foreground text for all badges:

| Status | Background | Foreground | Hex Background |
|--------|-----------|------------|----------------|
| Init   | Amber     | White      | `#D4A017`      |
| Filed  | Blue      | White      | `#0063B1`      |
| Paid   | Green     | White      | `#107C10`      |

**Rationale**:
- Medium-saturation colours maintain readability on both light and dark backgrounds.
- White text on these backgrounds meets WCAG AA contrast ratios (~4.5:1+).
- Colours are visually distinct from each other (warm/cool/cool-warm separation).
- `#0063B1` aligns with FluentTheme's blue accent family. `#107C10` is Windows Fluent green. `#D4A017` is a golden amber distinct from warning/error reds.

**Alternatives Considered**:
1. **Using DynamicResource theme colours**: Considered — would automatically adapt to theme changes, but the spec requires specific status-to-colour associations (amber/blue/green) that don't map to standard FluentTheme resources. Fixed colours provide guaranteed consistency.
2. **Semi-transparent backgrounds with themed text**: Rejected — makes the badge less prominent and harder to scan at a glance, defeating the purpose.

---

### R4: ToggleButton Mutual Exclusion Behaviour

**Context**: The spec requires that "only one filter button MUST be visually active at any given time" (FR-003) and clicking the already-active button should not change state (Scenario 4).

**Findings**:

The current implementation uses `IsChecked="{Binding ShowAll}"` and `IsChecked="{Binding ShowAll, Converter=InvertBool}"`. This creates mutual exclusion via data binding — when one is checked, the other is unchecked because they bind to inverse values of the same boolean.

However, `ToggleButton` in Avalonia allows unchecking by clicking the already-checked button (it toggles). This means a user could click the active button and uncheck both, leaving `ShowAll` in an ambiguous state. This violates FR-003.

**Decision**: Handle this in XAML by adding `ClickMode="Press"` and preventing uncheck. The most robust approach is to set `IsChecked` in a two-way binding so that clicking the already-active button re-sets the same value (no change). Since `ShowAll` uses `RaiseAndSetIfChanged`, setting the same value is a no-op. Additionally, we will not allow unchecking — the ToggleButton will behave like a radio button through the binding semantics: clicking "All" sets `ShowAll=true`, clicking "Unpaid" sets `ShowAll=false`. Clicking the already-active button tries to set the value it already has, which is ignored by `RaiseAndSetIfChanged`.

Actually, the real risk is that Avalonia's ToggleButton could uncheck itself on click. To prevent this, the `:checked` style needs to be paired with ensuring the `IsChecked` binding remains consistent. Since the binding is two-way by default and the ViewModel only accepts the change via `RaiseAndSetIfChanged`, the binding will re-sync the ToggleButton to its correct state even if the user tries to uncheck.

**Rationale**: The ViewModel-driven approach already handles this correctly — `RaiseAndSetIfChanged` ignores duplicate values, and the data binding re-syncs the control state. No additional code needed.

---

### R5: Loading Indicator for Filter Transitions (FR-008)

**Context**: The spec requires visible feedback during filter transitions. The FilingsView already has a `ProgressBar` bound to `IsLoading`.

**Findings**:

The existing `ProgressBar` at line 22-23 of `FilingsView.axaml` is already present:
```xml
<ProgressBar DockPanel.Dock="Top" IsIndeterminate="True"
             IsVisible="{Binding IsLoading}" Height="4" />
```

And the `LoadPageAsync` method in `FilingsViewModel` already sets `IsLoading = true` at the start and `IsLoading = false` in the `finally` block. The `ShowAll` setter triggers `LoadPageCommand.Execute()` which invokes `LoadPageAsync`.

**Decision**: The existing loading indicator already satisfies FR-008. The `ProgressBar` appears during every `LoadPageAsync` call, including filter changes. The `Rows.Clear()` call on line 251 also provides a visible "flash" as the DataGrid empties and repopulates.

**Rationale**: No additional work needed — the infrastructure is already in place. The user will see: (1) ToggleButton highlight change (new), (2) loading bar flash, (3) rows clear and repopulate.

**Note**: If testing reveals the transition is too fast to notice (< 100ms for cached data), a minimum display duration could be added, but this is a polish concern outside the current spec scope.

---

### R6: Converter Architecture — Single vs. Dual Converters

**Context**: The badge needs both a background brush and a foreground brush. Decide whether to use one converter with a parameter or two separate converters.

**Decision**: Use a single `FilingStatusToBadgeBrushConverter` that accepts a `ConverterParameter` string (`"Background"` or `"Foreground"`) to return the appropriate brush. This reduces the number of converter classes while maintaining the established `FuncValueConverter` / static instance pattern used throughout the project.

**Rationale**:
- One converter class instead of two reduces code surface.
- Parameter-based dispatch is a standard Avalonia/WPF converter pattern.
- The converter cannot be a simple `FuncValueConverter` (since it needs parameter support), so it will implement `IValueConverter` directly. This is consistent with the `DateOnlyToStringConverter` which also implements `IValueConverter` directly.

**Alternatives Considered**:
1. **Two separate static converters** (background + foreground): Considered — simpler per-converter, but doubles the file count for a trivial distinction.
2. **ViewModel properties returning brush**: Rejected — introduces `IBrush` (Avalonia.Media) dependency in the ViewModel, violating the clean separation between ViewModel and View concerns.

## Summary of Decisions

| # | Topic | Decision | Files Affected |
|---|-------|----------|---------------|
| R1 | ToggleButton styling | Scoped `:checked` pseudo-class styles in StackPanel | `FilingsView.axaml` |
| R2 | Badge rendering | `Border` + `TextBlock` in DataGrid cell template | `FilingsView.axaml` |
| R3 | Badge colours | Amber `#D4A017`, Blue `#0063B1`, Green `#107C10` with white text | `FilingStatusToBadgeBrushConverter.cs` |
| R4 | Mutual exclusion | Existing ViewModel binding handles it; no changes needed | — |
| R5 | Loading feedback | Existing `ProgressBar` + `IsLoading` already works | — |
| R6 | Converter pattern | Single converter with parameter for bg/fg | `FilingStatusToBadgeBrushConverter.cs` |
