# Implementation Plan: Reports Filter Header Flyouts

**Branch**: `051-reports-filter-header-flyouts` | **Date**: 2025-07-15 | **Spec**: `.specify/specs/051-reports-filter-header-flyouts/spec.md`
**Input**: Feature specification from `.specify/specs/051-reports-filter-header-flyouts/spec.md`

## Summary

Replace the existing inline filter row on the Reports page with Excel-style flyout popups anchored to DataGrid column headers. Each filterable column gets a funnel icon in its header; clicking opens a flyout with column-type-appropriate filter controls (text search, checkbox list, or numeric input). Operator selectors are removed — the UI uses only Equals/Contains. The backend `ReportColumnFilter` DTO is simplified (date fields become text-contains, status becomes multi-select set), and the repository/handler are updated accordingly. Server-side filtering with pagination is preserved.

## Technical Context

**Language/Version**: C# 12 / .NET 8
**Primary Dependencies**: Avalonia UI 11+, ReactiveUI, FluentTheme, EF Core 8 (SQLite)
**Storage**: SQLite (local), EF Core ORM
**Testing**: xUnit + FluentAssertions + NSubstitute
**Target Platform**: Windows + macOS desktop (Avalonia)
**Project Type**: Desktop application (Clean Architecture)
**Performance Goals**: Filter results within 1 second of Apply click (local DB)
**Constraints**: Server-side filtering required (pagination loads 30 items); no client-side filtering of full dataset
**Scale/Scope**: 6 filterable columns, 4 enum statuses, ~30 items per page

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Clean Architecture boundary is preserved (`Desktop -> Application -> Domain`; Infrastructure implements Application contracts only). — View/ViewModel changes in Desktop, DTO changes in Application, query changes in Infrastructure. No cross-boundary violations.
- [x] All monetary/rate/percentage values are modeled as `decimal`. — N/A: no monetary values in this feature.
- [x] All business dates are modeled as `DateOnly`; boundary conversions are identified. — `Report.ImportDate` and `Report.EmailDate` remain `DateOnly` in Domain. The filter change is UI-level: user types text, repository applies string-contains on the stored date representation. No DateOnly type changes.
- [x] Security/privacy constraints hold: local-first data, OS credential store for secrets, no telemetry. — No change. All queries local.
- [x] External network usage is limited to approved endpoints (IMAP and NBS) or explicitly justified. — No new network calls. Filter queries are local DB only.
- [x] All I/O paths are async; UI work avoids blocking calls and uses reactive async command flow. — `LoadPageAsync` is already async. Flyout open/close is synchronous UI. Filter apply triggers async `LoadPageCommand`. No blocking calls.
- [x] Tests and coverage impact are defined for Domain (rule/state coverage) and Application (>=90%). — Domain: no changes. Application: update handler tests for simplified filter. Desktop: update ViewModel tests for flyout flow + multi-select status. Infrastructure: update repository tests for text-contains date queries and IN status queries.
- [x] Feature work is mapped to an approved spec task under `.specify/tasks/`. — Will be created via `/speckit.tasks`.

## Project Structure

### Documentation (this feature)

```text
.specify/specs/051-reports-filter-header-flyouts/
├── plan.md              # This file
├── research.md          # Phase 0 output — technology decisions
├── data-model.md        # Phase 1 output — entity changes
├── quickstart.md        # Phase 1 output — implementation guide
└── contracts/
    └── ui-contracts.md  # Phase 1 output — flyout layout contracts
```

### Source Code (impacted files)

```text
src/
├── Rentier.Application/
│   ├── DTOs/
│   │   └── ReportColumnFilter.cs          # MODIFY — simplify filter fields
│   └── Handlers/
│       └── GetReportsQueryHandler.cs      # MODIFY — remove operator switch on FilingCount
├── Rentier.Infrastructure/
│   └── Repositories/
│       └── ReportRepository.cs            # MODIFY — text-contains dates, IN status
├── Rentier.Desktop/
│   ├── ViewModels/
│   │   ├── ReportsViewModel.cs            # MODIFY — flyout filter props, multi-select status
│   │   └── StatusCheckboxItem.cs          # NEW — checkbox item for status flyout
│   ├── Views/
│   │   └── ReportsView.axaml             # MODIFY — remove filter row, add header flyouts
│   └── Resources/
│       └── Strings.resx                   # MODIFY — add flyout UI strings

tests/
├── Rentier.UnitTests/
│   └── Desktop/
│       ├── ReportsViewModelTests.cs       # MODIFY — update filter tests
│       └── ReportsViewHeadlessTests.cs    # MODIFY — update if filter row removal affects
└── Rentier.IntegrationTests/
    └── Repositories/
        └── ReportRepositoryTests.cs       # MODIFY — test text-contains + IN queries
```

**Structure Decision**: Existing Clean Architecture 4-project layout. No new projects. One new file (`StatusCheckboxItem.cs`). All other changes are modifications to existing files.

## Complexity Tracking

No constitution violations. No complexity justifications needed.

---

## Design Decisions (from research.md)

### D-001: Flyout Mechanism
Use Avalonia `Flyout` attached to a `Button` (funnel icon) inside `DataGridTemplateColumn.Header`. Light-dismiss behavior = closing without Apply discards changes (FR-011).

### D-002: Staged Filter Values
Flyout content binds to **local staging properties**, not directly to ViewModel filter properties. On "Apply", staged values are copied to ViewModel properties, which triggers the reactive chain → `LoadPageCommand`. On dismiss, staged values are discarded.

**Implementation approach**: Since Avalonia `Flyout` creates/destroys content on open/close, the flyout `TextBox` can bind to a property that is initialized from the current filter value when the flyout opens. The "Apply" button copies it to the committed filter property.

Simplest approach: Use `Flyout.Opening` event (or `FlyoutBase.AttachedFlyoutShowMode`) to populate staged values. Bind the TextBox in the flyout to a staging property on the ViewModel. Apply copies staging → committed.

### D-003: Status Multi-Select
Replace single `ReportStatus?` with `ObservableCollection<StatusCheckboxItem>`. Each item has `IsChecked`. On Apply, collect checked statuses into `IReadOnlySet<ReportStatus>` for the filter. "Select All" checks all; "Clear" unchecks all.

### D-004: Date Text-Contains Filtering
Replace `DateOnly?` + `ComparisonOperator` with `string?` text-contains. Repository applies `LIKE '%value%'` on the stored date string (SQLite stores DateOnly as "yyyy-MM-dd" text). Partial matches like "2024-03" find all March 2024 dates.

### D-005: Numeric Parse-or-Ignore
Filing count flyout has a TextBox. On Apply, try `int.TryParse`. If success → set `FilingCountValue`. If failure → set null (no filter, FR-014).

### D-006: Debounce Strategy
Per spec FR-013 and clarification in assumptions: debounce is NOT per-keystroke live filtering. The Apply button triggers the query. The 300ms Throttle on text filters in `WhenActivated` prevents rapid double-clicks or programmatic rapid-fire property changes.

### D-007: Funnel Icon Visual State
The funnel `PathIcon` foreground is bound to a computed property: if the column has an active filter → accent color brush, else → default foreground brush. Each column needs a `bool` property like `HasNameFilter`, `HasStatusFilter`, etc., or a single method `IsFilterActive(columnName)`.

Simplest: compute `bool` per column from the committed filter properties.

---

## Detailed Change Specification

### Layer 1: Application — ReportColumnFilter

**File**: `src/Rentier.Application/DTOs/ReportColumnFilter.cs`

Replace the current record with:

```csharp
public sealed record ReportColumnFilter(
    string? NameContains = null,
    string? ImporterContains = null,
    IReadOnlyList<Guid>? ImporterIds = null,
    string? ImportDateContains = null,
    string? EmailDateContains = null,
    int? FilingCountValue = null,
    IReadOnlySet<ReportStatus>? StatusFilters = null);
```

### Layer 2: Infrastructure — ReportRepository

**File**: `src/Rentier.Infrastructure/Repositories/ReportRepository.cs`

In `GetPagedAsync`, replace date filter logic:

```csharp
// Date text-contains (SQLite stores DateOnly as "yyyy-MM-dd" text)
if (!string.IsNullOrWhiteSpace(filter.ImportDateContains))
{
    var term = filter.ImportDateContains;
    query = query.Where(r => EF.Functions.Like(
        r.ImportDate.ToString(), $"%{term}%"));
}

if (!string.IsNullOrWhiteSpace(filter.EmailDateContains))
{
    var term = filter.EmailDateContains;
    query = query.Where(r => r.EmailDate != null &&
        EF.Functions.Like(r.EmailDate.Value.ToString(), $"%{term}%"));
}
```

Replace status filter:
```csharp
if (filter.StatusFilters is { Count: > 0 })
    query = query.Where(r => filter.StatusFilters.Contains(r.Status));
```

Remove `ApplyImportDateFilter` and `ApplyEmailDateFilter` helper methods.

### Layer 3: Application Handler — GetReportsQueryHandler

**File**: `src/Rentier.Application/Handlers/GetReportsQueryHandler.cs`

Simplify filing count post-filter (remove operator switch):

```csharp
if (query.Filter?.FilingCountValue.HasValue == true)
{
    var fcVal = query.Filter.FilingCountValue.Value;
    dtos = dtos.Where(d => d.FilingCount == fcVal).ToList();
}
```

### Layer 4: Desktop ViewModel — ReportsViewModel

**File**: `src/Rentier.Desktop/ViewModels/ReportsViewModel.cs`

**Remove**: `ImportDateOperator`, `EmailDateOperator`, `FilingCountOperator` properties and backing fields. Remove `StatusFilterOptions`. Change `ImportDateFilter` and `EmailDateFilter` from `DateOnly?` to `string?`. Change `FilingCountFilter` to `string?` (parsed to int in `BuildFilter`).

**Add**:
- `ObservableCollection<StatusCheckboxItem> StatusCheckboxItems` — initialized with all `ReportStatus` values, all checked
- `ReactiveCommand<Unit, Unit> ApplyFilterCommand` — commits staged values and triggers `LoadPageCommand`
- Computed booleans: `HasNameFilter`, `HasImporterFilter`, `HasImportDateFilter`, `HasEmailDateFilter`, `HasFilingCountFilter`, `HasStatusFilter` — for funnel icon highlighting

**Update `BuildFilter()`**:
```csharp
private ReportColumnFilter? BuildFilter()
{
    var hasName = !string.IsNullOrWhiteSpace(_nameFilter);
    var hasImporter = !string.IsNullOrWhiteSpace(_importerFilter);
    var hasImportDate = !string.IsNullOrWhiteSpace(_importDateFilter);
    var hasEmailDate = !string.IsNullOrWhiteSpace(_emailDateFilter);
    var hasFilingCount = int.TryParse(_filingCountFilter, out var filingCountVal);
    
    var checkedStatuses = StatusCheckboxItems
        .Where(s => s.IsChecked)
        .Select(s => s.Status)
        .ToHashSet();
    var hasStatusFilter = checkedStatuses.Count < StatusCheckboxItems.Count; // not all checked

    if (!hasName && !hasImporter && !hasImportDate && !hasEmailDate && !hasFilingCount && !hasStatusFilter)
        return null;

    return new ReportColumnFilter(
        NameContains: hasName ? _nameFilter : null,
        ImporterContains: hasImporter ? _importerFilter : null,
        ImportDateContains: hasImportDate ? _importDateFilter : null,
        EmailDateContains: hasEmailDate ? _emailDateFilter : null,
        FilingCountValue: hasFilingCount ? filingCountVal : null,
        StatusFilters: hasStatusFilter ? checkedStatuses : null);
}
```

**Update `ClearFiltersCommand`**: Reset all text filters to null, check all `StatusCheckboxItems`.

**Update `HasActiveFilters`**: Recompute from text filter emptiness + status checkbox state.

**Update `WhenActivated`**: Remove operator change subscriptions. The filter trigger now comes from `ApplyFilterCommand` execution (not from property changes). Remove all the individual `WhenAnyValue` filter subscriptions and replace with a single subscription to `ApplyFilterCommand` → `LoadPageCommand`.

### Layer 5: Desktop View — ReportsView.axaml

**File**: `src/Rentier.Desktop/Views/ReportsView.axaml`

1. **Remove** the entire filter row `Border` (lines 86-164 in current file)

2. **Convert** remaining `DataGridTextColumn` columns to `DataGridTemplateColumn` with custom headers:

Example for ImportDate column:
```xml
<DataGridTemplateColumn Width="110" CanUserSort="False">
  <DataGridTemplateColumn.Header>
    <StackPanel Orientation="Horizontal" Spacing="4">
      <TextBlock Text="{Binding [Reports_Col_ImportDate], Source={StaticResource Localizer}}"
                 VerticalAlignment="Center" />
      <Button Padding="2" Background="Transparent" BorderThickness="0">
        <PathIcon Data="{StaticResource FilterIcon}" Width="12" Height="12"
                  Foreground="{Binding DataContext.HasImportDateFilter,
                    RelativeSource={RelativeSource AncestorType=DataGrid},
                    Converter={StaticResource BoolToFilterBrushConverter}}" />
        <Button.Flyout>
          <Flyout Placement="BottomEdgeAlignedLeft" ShowMode="Standard">
            <StackPanel Width="180" Spacing="8" Margin="8">
              <TextBox Watermark="{Binding [Reports_Filter_Date_Watermark], Source={StaticResource Localizer}}"
                       Text="{Binding DataContext.ImportDateFilter,
                         RelativeSource={RelativeSource AncestorType=DataGrid},
                         Mode=TwoWay}" />
              <Button Content="{Binding [Reports_Filter_Apply], Source={StaticResource Localizer}}"
                      Command="{Binding DataContext.ApplyFilterCommand,
                        RelativeSource={RelativeSource AncestorType=DataGrid}}"
                      HorizontalAlignment="Right" />
            </StackPanel>
          </Flyout>
        </Button.Flyout>
      </Button>
    </StackPanel>
  </DataGridTemplateColumn.Header>
  <DataGridTemplateColumn.CellTemplate>
    <DataTemplate>
      <TextBlock Text="{Binding ImportDateDisplay}" VerticalAlignment="Center" Margin="4,0" />
    </DataTemplate>
  </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

3. **Status column flyout** uses `ItemsControl` with `CheckBox` per `StatusCheckboxItem`:
```xml
<Flyout Placement="BottomEdgeAlignedLeft" ShowMode="Standard">
  <StackPanel Width="200" Spacing="4" Margin="8">
    <StackPanel Orientation="Horizontal" Spacing="8">
      <Button Content="{Binding [Reports_Filter_SelectAll], Source={StaticResource Localizer}}"
              Command="{Binding DataContext.SelectAllStatusesCommand, ...}"
              Classes="link" />
      <Button Content="{Binding [Reports_Filter_ClearSelection], Source={StaticResource Localizer}}"
              Command="{Binding DataContext.ClearAllStatusesCommand, ...}"
              Classes="link" />
    </StackPanel>
    <ItemsControl ItemsSource="{Binding DataContext.StatusCheckboxItems, ...}">
      <ItemsControl.ItemTemplate>
        <DataTemplate>
          <CheckBox Content="{Binding DisplayName}" IsChecked="{Binding IsChecked, Mode=TwoWay}" />
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>
    <Button Content="{Binding [Reports_Filter_Apply], Source={StaticResource Localizer}}"
            Command="{Binding DataContext.ApplyFilterCommand, ...}"
            HorizontalAlignment="Right" />
  </StackPanel>
</Flyout>
```

4. **Add** `FilterIcon` PathIcon to app resources (if not already present — a funnel/filter SVG path)

5. **Add** `BoolToFilterBrushConverter` — converts `true` → accent brush, `false` → default foreground brush

### Layer 6: New File — StatusCheckboxItem

**File**: `src/Rentier.Desktop/ViewModels/StatusCheckboxItem.cs`

```csharp
public sealed class StatusCheckboxItem : ReactiveObject
{
    public ReportStatus Status { get; }
    public string DisplayName { get; }

    private bool _isChecked = true;
    public bool IsChecked
    {
        get => _isChecked;
        set => this.RaiseAndSetIfChanged(ref _isChecked, value);
    }

    public StatusCheckboxItem(ReportStatus status, string displayName)
    {
        Status = status;
        DisplayName = displayName;
    }
}
```

### Layer 7: Resources

**File**: `src/Rentier.Desktop/Resources/Strings.resx`

Add keys:
- `Reports_Filter_Apply` = "Primijeni"
- `Reports_Filter_SelectAll` = "Odaberi sve"
- `Reports_Filter_ClearSelection` = "Očisti"
- `Reports_Filter_Date_Watermark` = "Pretraži datum..."
- `Reports_Filter_Count_Watermark` = "#"

---

## Testing Impact

### Desktop ViewModel Tests (MODIFY)

Update `ReportsViewModelTests.cs`:
- Test `ApplyFilterCommand` triggers `LoadPageCommand`
- Test text filter → `BuildFilter()` produces correct `ReportColumnFilter`
- Test date filter as string text → `BuildFilter()` sets `ImportDateContains`
- Test filing count invalid text → `BuildFilter()` sets `FilingCountValue = null`
- Test status multi-select → unchecking some statuses → `BuildFilter()` sets `StatusFilters`
- Test `ClearFiltersCommand` resets all filters and checks all statuses
- Test `HasActiveFilters` computes correctly from new filter shape
- Remove all operator-related test cases

### Infrastructure Integration Tests (MODIFY)

Update `ReportRepositoryTests.cs`:
- Test `ImportDateContains = "2024-03"` returns only March 2024 reports
- Test `EmailDateContains` partial match
- Test `StatusFilters = { Init, Processed }` returns only those statuses
- Test `StatusFilters` with all statuses = returns all (no filter)
- Remove operator-based date filter tests

### Application Handler Tests (MODIFY)

Update handler tests:
- Test filing count filter uses equals only (no operator)
- Test `StatusFilters` passed through correctly to repository

### Headless View Tests (MODIFY)

Update `ReportsViewHeadlessTests.cs`:
- Verify filter row is removed (no inline filter controls)
- Verify DataGrid columns render with header content (funnel icons)
