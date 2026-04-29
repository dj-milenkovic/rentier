# Implementation Plan: Filings Per-Report Filter Chip

**Branch**: `feature/043-filings-filter-chip` | **Date**: 2025-07-22 | **Spec**: `.specify/specs/043-filings-filter-chip/spec.md`
**Input**: Feature specification from `.specify/specs/043-filings-filter-chip/spec.md`

## Summary

When the user navigates to the Filings page from the Reports page via "View Filings", a ReportIdFilter is applied but was previously invisible and irremovable. This feature adds a dismissible chip ("Filtered by report ✕") to the filter bar. The chip appears only when a report filter is active; clicking ✕ clears the filter (sets `ReportIdFilter = null`) and reloads all filings via the existing reactive pipeline. Only the Desktop layer (ViewModel + View) is impacted.

## Technical Context

**Language/Version**: C# 12 / .NET 8  
**Primary Dependencies**: Avalonia UI 11+, ReactiveUI, CommunityToolkit.Mvvm  
**Storage**: N/A (no persistence changes)  
**Testing**: xUnit + FluentAssertions + NSubstitute  
**Target Platform**: Windows + macOS (cross-platform desktop)  
**Project Type**: Desktop application (Avalonia)  
**Performance Goals**: N/A (trivial UI addition)  
**Constraints**: Chip must not block UI thread; dismiss action must be async-safe  
**Scale/Scope**: 2 modified files, 1 resource file update, 2 test files updated

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only).
  - ✅ All changes are in Desktop layer (ViewModel + View). No Application, Domain, or Infrastructure changes.
- [x] All monetary/rate/percentage values are modeled as `decimal`.
  - ✅ N/A — no monetary or rate values introduced.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified.
  - ✅ N/A — no date fields introduced.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry.
  - ✅ N/A — chip is in-memory UI state, no data persisted or transmitted.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified as a constitution amendment.
  - ✅ N/A — no network calls. Reuses existing `GetFilingsQuery`.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow.
  - ✅ `ClearReportFilterCommand` sets a property; reload triggers existing `ReactiveCommand.CreateFromTask` pipeline.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%).
  - ✅ Domain/Application unchanged. Desktop ViewModel tests will cover chip visibility and dismiss.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`.
  - ⏳ Will be mapped when `/speckit.tasks` generates `tasks.md`.

## Project Structure

### Documentation (this feature)

```text
.specify/specs/043-filings-filter-chip/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (files to modify)

```text
src/Rentier.Desktop/
├── ViewModels/
│   └── FilingsViewModel.cs          # Add HasReportFilter OAPH + ClearReportFilterCommand
├── Views/
│   └── FilingsView.axaml            # Add chip in filter bar DockPanel
└── Resources/
    └── Strings.resx                 # Add 2 localization entries

tests/Rentier.UnitTests/
└── Desktop/
    ├── FilingsViewModelTests.cs     # Add chip visibility + dismiss tests
    └── Views/
        └── FilingsViewHeadlessTests.cs  # Add chip rendering tests
```

**Structure Decision**: No new files or directories. All changes are modifications to existing files within the established Clean Architecture layout.

## Complexity Tracking

No constitution violations. No complexity justifications needed.

---

## Design Details

### ViewModel Changes (`FilingsViewModel.cs`)

#### New Properties

```csharp
// Derived property — canonical ReactiveUI pattern
private readonly ObservableAsPropertyHelper<bool> _hasReportFilter;
public bool HasReportFilter => _hasReportFilter.Value;

// Dismiss command
public ReactiveCommand<Unit, Unit> ClearReportFilterCommand { get; }
```

#### Wiring (in constructor or WhenActivated)

```csharp
// HasReportFilter derived from ReportIdFilter
_hasReportFilter = this.WhenAnyValue(x => x.ReportIdFilter)
    .Select(id => id.HasValue)
    .ToProperty(this, x => x.HasReportFilter);

// ClearReportFilterCommand — sets filter to null, existing pipeline reloads
var canClear = this.WhenAnyValue(x => x.HasReportFilter);
ClearReportFilterCommand = ReactiveCommand.Create(
    () => { ReportIdFilter = null; },
    canClear,
    _scheduler);
```

#### Existing Reactive Pipeline (no changes needed)

The existing subscription (lines 393-397) already handles `ReportIdFilter` changes:
```csharp
this.WhenAnyValue(x => x.ReportIdFilter)
    .Skip(1)
    .Select(_ => Unit.Default)
    .InvokeCommand(LoadPageCommand);
```

Setting `ReportIdFilter = null` triggers this pipeline, which reloads filings unfiltered. The `ReportIdFilter` setter already resets `_currentPage = 1`.

### View Changes (`FilingsView.axaml`)

Insert chip after the radio buttons `StackPanel`, left-aligned in the existing `DockPanel`:

```xml
<!-- Report filter chip — placed AFTER radio buttons -->
<Border IsVisible="{Binding HasReportFilter}"
        Background="{DynamicResource RentierChipBackgroundBrush}"
        CornerRadius="10" Padding="8,2" Margin="8,0,0,0"
        VerticalAlignment="Center">
  <StackPanel Orientation="Horizontal" Spacing="4">
    <TextBlock Text="{Binding [Filings_FilterChip_Report], Source={StaticResource Localizer}}"
               VerticalAlignment="Center" FontSize="12" />
    <Button Command="{Binding ClearReportFilterCommand}"
            AutomationProperties.Name="{Binding [Filings_FilterChip_Dismiss], Source={StaticResource Localizer}}"
            Padding="2" Background="Transparent" BorderThickness="0"
            VerticalAlignment="Center" Cursor="Hand">
      <TextBlock Text="✕" FontSize="12" />
    </Button>
  </StackPanel>
</Border>
```

**Brush note**: If `RentierChipBackgroundBrush` doesn't exist, use an appropriate existing theme brush (e.g., `RentierSurfaceSecondaryBrush`) or define a new one following the design system tokens.

### Localization (`Strings.resx`)

| Key | Value |
|---|---|
| `Filings_FilterChip_Report` | `Filtered by report` |
| `Filings_FilterChip_Dismiss` | `Remove report filter` |

### Test Plan

#### ViewModel Tests (`FilingsViewModelTests.cs`)

| Test Name | Scenario |
|---|---|
| `HasReportFilter_WhenReportIdFilterIsNull_ReturnsFalse` | Default state |
| `HasReportFilter_WhenReportIdFilterIsSet_ReturnsTrue` | After navigation from Reports |
| `ClearReportFilterCommand_WhenExecuted_SetsReportIdFilterToNull` | Dismiss behavior |
| `ClearReportFilterCommand_WhenNoReportFilter_CannotExecute` | Guard condition |
| `ClearReportFilterCommand_WhenExecuted_TriggersLoadPageCommand` | Reload verification |

#### Headless UI Tests (`FilingsViewHeadlessTests.cs`)

| Test Name | Scenario |
|---|---|
| `FilterChip_WhenReportFilterActive_IsVisible` | Chip renders |
| `FilterChip_WhenNoReportFilter_IsNotVisible` | Chip hidden |
| `FilterChip_DismissButton_WhenClicked_ChipDisappears` | Dismiss interaction |
