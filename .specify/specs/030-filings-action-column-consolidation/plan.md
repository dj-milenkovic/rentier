# Implementation Plan: Filings — Action Column Consolidation & Icon-Only Buttons

**Branch**: `feat/027-031-ux-improvements` | **Date**: 2025-07-17 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `.specify/specs/030-filings-action-column-consolidation/spec.md`

## Summary

Collapse three separate action columns (Change Status ComboBox, Export button, Delete button) in the Filings DataGrid into a single "Actions" column containing three icon-only buttons with tooltips. Replace all action-button code-behind event handlers with direct MVVM command bindings via per-row `ReactiveCommand` properties on `FilingRowViewModel`. This is a Desktop-layer-only change — no Domain, Application, or Infrastructure modifications.

## Technical Context

**Language/Version**: C# 12 / .NET 8  
**Primary Dependencies**: Avalonia UI 11+ (FluentTheme), ReactiveUI, CommunityToolkit.Mvvm  
**Storage**: N/A (no storage changes)  
**Testing**: xUnit + FluentAssertions + NSubstitute  
**Target Platform**: Windows + macOS desktop (Avalonia cross-platform)  
**Project Type**: Desktop application (Avalonia MVVM)  
**Performance Goals**: N/A (UI restructuring; no new I/O paths)  
**Constraints**: Icon-only buttons must remain discoverable via tooltips; advance-status button must respect the domain state machine  
**Scale/Scope**: 4 files modified, 5 new string resources, ~4 existing test files updated

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - **Justification**: All changes are confined to `Rentier.Desktop` (Views, ViewModels, Resources). No new dependencies on Application or Domain are introduced — the existing `AdvanceStatusCommand`, `ExportCommand`, and `DeleteCommand` on `FilingsViewModel` are reused. `FilingRowViewModel` creates lightweight wrapper commands that delegate to the parent ViewModel. No new `using` directives for Application or Infrastructure namespaces are needed in the row VM.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - **Justification**: No monetary fields are added, removed, or modified. `TaxPayable` remains `decimal` throughout.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - **Justification**: No date fields are added, removed, or modified. `FilingDeadline` remains `DateOnly` throughout.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - **Justification**: No data storage changes. No new network calls. No new personal data exposed.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified as a constitution amendment.
  - **Justification**: No network calls introduced. Export uses existing local file-save mechanism.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - **Justification**: Per-row commands delegate to existing `ReactiveCommand.CreateFromTask` commands on `FilingsViewModel`. No new I/O paths. All command execution remains non-blocking.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - **Justification**: Domain and Application layers are untouched — no coverage impact. Desktop ViewModel tests must be updated to account for the new `FilingRowViewModel` factory signature and to verify per-row command behavior (enabled/disabled state, tooltip text, delegation).
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - **Justification**: Will be mapped upon task generation via `/speckit.tasks`.

## Project Structure

### Documentation (this feature)

```text
.specify/specs/030-filings-action-column-consolidation/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 research output
├── data-model.md        # Phase 1 data model
├── quickstart.md        # Phase 1 quickstart guide
├── contracts/
│   └── ui-binding-contract.md  # Phase 1 ViewModel↔View binding contract
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Rentier.Domain/          # No changes
├── Rentier.Application/     # No changes
├── Rentier.Infrastructure/  # No changes
└── Rentier.Desktop/
    ├── ViewModels/
    │   ├── FilingsViewModel.cs        # Modified: pass delegates to row VM factory
    │   └── FilingRowViewModel.cs      # Modified: add per-row commands, tooltip, HasNextStatus
    ├── Views/
    │   ├── FilingsView.axaml          # Modified: replace 3 columns with 1 Actions column
    │   └── FilingsView.axaml.cs       # Modified: remove 3 event handlers
    └── Resources/
        └── Strings.resx               # Modified: add 5 new string resources

tests/
└── Rentier.UnitTests/
    └── Desktop/
        ├── FilingsViewModelTests.cs           # Modified: update for new row VM factory
        ├── FilingsViewModelBulkDeleteTests.cs # Modified: update for new row VM factory
        └── FilingRowViewModelTests.cs         # New: test per-row commands, tooltips, enabled state
```

**Structure Decision**: Single solution with four projects following Clean Architecture. All changes are in the `Rentier.Desktop` project (outermost layer) and its associated test project. No structural changes needed.

## Complexity Tracking

> No Constitution Check violations to justify. All gates pass cleanly.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | — | — |

---

## Design Decisions (from Research)

### D-001: Icon Rendering — PathIcon with Inline Geometry

Use Avalonia's built-in `PathIcon` with SVG path `Data` attribute directly in AXAML. No external icon package needed. Three small vector path strings render crisply at all DPIs.

**See**: [research.md](research.md) § R-001

### D-002: Command Binding — Per-Row Commands on FilingRowViewModel

Add `AdvanceStatusCommand`, `ExportCommand`, and `DeleteCommand` as `ReactiveCommand` properties on `FilingRowViewModel`. Each delegates to the parent `FilingsViewModel` command with the row's identity pre-bound. This eliminates all three action-button code-behind event handlers.

**See**: [research.md](research.md) § R-003

### D-003: Tooltip Strategy — ToolTip.Tip Attached Property

Use `ToolTip.Tip` with binding for dynamic advance-status tooltip and static resource strings for export/delete. Disabled advance-status buttons show "No further transitions".

**See**: [research.md](research.md) § R-002, R-006

### D-004: Destructive Style — Inline Foreground="Red"

Apply `Foreground="Red"` on the delete button's `PathIcon` to match the existing bulk-delete button pattern.

**See**: [research.md](research.md) § R-004

---

## Implementation Guidance

### Step 1: Extend FilingRowViewModel

**File**: `src/Rentier.Desktop/ViewModels/FilingRowViewModel.cs`

Add the following to the class:

1. **Properties**:
   - `bool HasNextStatus` — computed: `AvailableNextStatuses.Count > 0`
   - `string AdvanceStatusTooltip` — computed: when `HasNextStatus`, format `Strings.Filings_Tooltip_AdvanceStatus` with the next status display name; otherwise, `Strings.Filings_Tooltip_AdvanceStatus_None`

2. **Commands**:
   - `ReactiveCommand<Unit, Unit> AdvanceStatusCommand` — created with `canExecute: Observable.Return(HasNextStatus)`. Execute body: invoke the parent delegate `advanceStatus((Id, AvailableNextStatuses[0]))`.
   - `ReactiveCommand<Unit, Unit> ExportCommand` — always enabled. Execute body: invoke `export(Id)`.
   - `ReactiveCommand<Unit, Unit> DeleteCommand` — always enabled. Execute body: invoke `delete(Id)`.

3. **Factory method change**:
   ```csharp
   public static FilingRowViewModel From(
       FilingRowDto dto,
       Action<(Guid Id, FilingStatus NewStatus)> advanceStatus,
       Action<Guid> export,
       Action<Guid> delete)
   ```

### Step 2: Update FilingsViewModel.LoadPageAsync

**File**: `src/Rentier.Desktop/ViewModels/FilingsViewModel.cs`

In `LoadPageAsync`, change the row creation loop:

```csharp
foreach (var dto in page.Rows)
    Rows.Add(FilingRowViewModel.From(
        dto,
        args => AdvanceStatusCommand.Execute(args).Subscribe(),
        id => ExportCommand.Execute(id).Subscribe(),
        id => DeleteCommand.Execute(id).Subscribe()));
```

### Step 3: Add String Resources

**File**: `src/Rentier.Desktop/Resources/Strings.resx`

| Key | Value |
|---|---|
| `Filings_Tooltip_AdvanceStatus` | `Mark as {0}` |
| `Filings_Tooltip_AdvanceStatus_None` | `No further transitions` |
| `Filings_Tooltip_Export` | `Export PP-OPO XML` |
| `Filings_Tooltip_Delete` | `Delete filing` |
| `Filings_Col_Actions` | `Actions` |

### Step 4: Restructure FilingsView.axaml

**File**: `src/Rentier.Desktop/Views/FilingsView.axaml`

1. **Remove** the Status ComboBox column (lines 114–130)
2. **Remove** the Export button column (lines 161–171)
3. **Remove** the Delete button column (lines 173–183)
4. **Add** a consolidated Actions column as the last column:

```xml
<!-- Actions column: Advance Status, Export XML, Delete -->
<DataGridTemplateColumn Header="{x:Static res:Strings.Filings_Col_Actions}" Width="130">
  <DataGridTemplateColumn.CellTemplate>
    <DataTemplate>
      <StackPanel Orientation="Horizontal" Spacing="4"
                  HorizontalAlignment="Center" VerticalAlignment="Center">
        <!-- Advance Status -->
        <Button Command="{Binding AdvanceStatusCommand}"
                ToolTip.Tip="{Binding AdvanceStatusTooltip}"
                Padding="6" Background="Transparent" BorderThickness="0">
          <PathIcon Data="M8 4l8 8-8 8" Width="16" Height="16" />
        </Button>
        <!-- Export XML -->
        <Button Command="{Binding ExportCommand}"
                ToolTip.Tip="{x:Static res:Strings.Filings_Tooltip_Export}"
                Padding="6" Background="Transparent" BorderThickness="0">
          <PathIcon Data="M12 2v10m0 0l-4-4m4 4l4-4M4 14v4h16v-4" Width="16" Height="16" />
        </Button>
        <!-- Delete -->
        <Button Command="{Binding DeleteCommand}"
                ToolTip.Tip="{x:Static res:Strings.Filings_Tooltip_Delete}"
                Padding="6" Background="Transparent" BorderThickness="0">
          <PathIcon Data="M6 6l12 0M9 6V4h6v2M7 6v12h10V6M10 9v6M14 9v6"
                    Foreground="Red" Width="16" Height="16" />
        </Button>
      </StackPanel>
    </DataTemplate>
  </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

> **Note**: The `PathIcon Data` values above are illustrative. Final icon paths should be sourced from an open-source icon set (e.g., Lucide, Heroicons) at implementation time for visual quality.

### Step 5: Clean Up Code-Behind

**File**: `src/Rentier.Desktop/Views/FilingsView.axaml.cs`

1. **Remove** `StatusComboBox_SelectionChanged` method
2. **Remove** `ExportButton_Click` method
3. **Remove** `DeleteButton_Click` method
4. **Keep** `PaymentRef_LostFocus` (still needed — TextBox LostFocus cannot be expressed as a command without code-behind)
5. **Remove** unused `using Rentier.Domain.Entities;` if no longer referenced

### Step 6: Update Tests

**File**: `tests/Rentier.UnitTests/Desktop/FilingsViewModelTests.cs`  
**File**: `tests/Rentier.UnitTests/Desktop/FilingsViewModelBulkDeleteTests.cs`

Update the mock `FilingRowViewModel.From()` calls to pass the new delegate parameters. Existing test assertions for `AdvanceStatusCommand`, `ExportCommand`, and `DeleteCommand` on `FilingsViewModel` remain valid.

**New file**: `tests/Rentier.UnitTests/Desktop/FilingRowViewModelTests.cs`

New test cases:
- `AdvanceStatusCommand_InitStatus_IsEnabled`
- `AdvanceStatusCommand_FiledStatus_IsEnabled`
- `AdvanceStatusCommand_PaidStatus_IsDisabled`
- `AdvanceStatusTooltip_InitStatus_ReturnsMarkAsFiled`
- `AdvanceStatusTooltip_FiledStatus_ReturnsMarkAsPaid`
- `AdvanceStatusTooltip_PaidStatus_ReturnsNoFurtherTransitions`
- `HasNextStatus_InitStatus_ReturnsTrue`
- `HasNextStatus_PaidStatus_ReturnsFalse`
- `ExportCommand_AnyStatus_IsAlwaysEnabled`
- `DeleteCommand_AnyStatus_IsAlwaysEnabled`
- `AdvanceStatusCommand_Execute_DelegatesToParentCommand`
- `ExportCommand_Execute_DelegatesToParentCommand`
- `DeleteCommand_Execute_DelegatesToParentCommand`

---

## Requirement Traceability

| Requirement | Implementation Location | Verified By |
|---|---|---|
| FR-001: Remove Status ComboBox column | `FilingsView.axaml` — remove lines 114–130 | Visual inspection; column count test |
| FR-002: Remove Export column | `FilingsView.axaml` — remove lines 161–171 | Visual inspection; column count test |
| FR-003: Remove Delete column | `FilingsView.axaml` — remove lines 173–183 | Visual inspection; column count test |
| FR-004: Add Actions column (rightmost) | `FilingsView.axaml` — add Actions template column | Visual inspection |
| FR-005: Three icon-only buttons | `FilingsView.axaml` — StackPanel with 3 PathIcon buttons | Visual inspection |
| FR-006: Advance Status enabled only when next status exists | `FilingRowViewModel.AdvanceStatusCommand` canExecute | `FilingRowViewModelTests` |
| FR-007: Advance Status invokes command with first next status | `FilingRowViewModel.AdvanceStatusCommand` execute body | `FilingRowViewModelTests` |
| FR-008: Export always enabled | `FilingRowViewModel.ExportCommand` (no canExecute) | `FilingRowViewModelTests` |
| FR-009: Export invokes export command | `FilingRowViewModel.ExportCommand` execute body | `FilingRowViewModelTests` |
| FR-010: Delete always enabled | `FilingRowViewModel.DeleteCommand` (no canExecute) | `FilingRowViewModelTests` |
| FR-011: Delete invokes delete command | `FilingRowViewModel.DeleteCommand` execute body | `FilingRowViewModelTests` |
| FR-012: Delete destructive style | `FilingsView.axaml` — `Foreground="Red"` on delete PathIcon | Visual inspection |
| FR-013: Tooltips on all buttons | `FilingsView.axaml` — `ToolTip.Tip` bindings | Visual inspection; `FilingRowViewModelTests` |
| FR-014: Disabled advance-status at terminal state | `FilingRowViewModel.HasNextStatus` + canExecute | `FilingRowViewModelTests` |
| FR-015: Status badge unchanged | No changes to status badge column | Visual inspection |
| FR-016: Remove ComboBox code-behind handler | `FilingsView.axaml.cs` — remove `StatusComboBox_SelectionChanged` | Code review |
| FR-017: Direct command binding for advance status | `FilingsView.axaml` — `Command="{Binding AdvanceStatusCommand}"` | Code review |
| FR-018: Icon-only buttons (no text) | `FilingsView.axaml` — buttons contain only `PathIcon`, no `Content` text | Visual inspection |

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| PathIcon geometry paths render incorrectly on macOS | Low | Medium | Test on macOS CI runner; use well-tested icon path data from established icon sets |
| Per-row command subscription leaks | Low | Medium | Ensure `FilingRowViewModel` disposes commands when row is removed from collection; test with repeated page loads |
| Breaking existing `FilingsViewModelTests` due to factory signature change | High | Low | Update test helpers to pass delegate parameters; straightforward mechanical change |
| Tooltip not visible in narrow DataGrid column | Low | Low | Set minimum column width to 130px; tooltips appear on hover regardless of column width |
