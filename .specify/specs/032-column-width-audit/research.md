# Research: Column Width Audit — Filings & Reports Tables

**Feature**: 032-column-width-audit
**Date**: 2025-07-17
**Status**: Complete

## Research Questions & Findings

### RQ-001: What are the current column widths and how do they differ from spec targets?

**Decision**: Adopt the specified column widths from the feature spec.

**Findings — Filings DataGrid:**

| Column | Current Width | Target Width | Delta | Change Type |
|--------|--------------|-------------|-------|-------------|
| Selection (checkbox) | 42px | 40px | −2px | Shrink |
| Status badge | 84px | 90px | +6px | Widen |
| Income Type | 96px | 110px | +14px | Widen |
| Paying Entity | `*` (star) | `*` (star) | None | Keep |
| Filing Deadline | 110px | 120px | +10px | Widen |
| Tax Payable | 120px | 130px | +10px | Widen |
| Payment Reference | 140px | 180px | +40px | Widen |
| Actions | 108px (fixed) | Auto | Change to auto-size |

**Findings — Reports DataGrid:**

| Column | Current Width | Target Width | Delta | Change Type |
|--------|--------------|-------------|-------|-------------|
| Selection (checkbox) | 44px | 40px | −4px | Shrink |
| Report Name | `2*` (2-star) | `*` (star) | Change star weight |
| Import Date | 96px | 110px | +14px | Widen |
| Email Date | 96px | 110px | +14px | Widen |
| Importer | 120px | 160px | +40px | Widen |
| Status | 88px | 100px | +12px | Widen |
| Filing Count | 56px | 70px | +14px | Widen |
| Actions | 88px (fixed) | Auto | Change to auto-size |

**Rationale**: The current widths are slightly undersized for most columns, causing potential truncation of dates (`yyyy-MM-dd` needs ~110px), amounts (`1,234.56 RSD` needs ~130px), and payment references (mixed alphanumeric strings need ~180px). The spec values were determined through representative data analysis.

**Alternatives considered**: Leaving some widths unchanged where close to target — rejected because the audit goal is to achieve a coherent, intentional sizing system across both tables.

---

### RQ-002: How should auto-sizing be implemented for Actions columns in Avalonia DataGrid?

**Decision**: Use `Width="Auto"` on `DataGridTemplateColumn` for Actions columns.

**Rationale**: Avalonia's `DataGridTemplateColumn` supports `Width="Auto"`, which measures the content of the cell template (the StackPanel of icon buttons) and sizes to fit. This replaces the current hardcoded pixel widths (108px and 88px) with content-driven sizing, ensuring the column always fits the buttons without excess space.

**Alternatives considered**:
- `Width="SizeToCells"` — Not supported in Avalonia DataGrid; this is a WPF-specific value.
- `Width="SizeToHeader"` — Would size to header text, not cell content, which is wrong for icon-only action columns.
- Keep fixed widths — Rejected because button padding/icon sizes may vary across themes, and auto-sizing is more resilient.

---

### RQ-003: What is the difference between `*` and `2*` star widths, and which should be used?

**Decision**: Use `Width="*"` (1-star) for both Paying Entity (Filings) and Report Name (Reports).

**Rationale**: Since each table has only one star-width column, the star weight (`1*` vs `2*`) makes no practical difference — the column fills all remaining space regardless. However, using `*` (equivalent to `1*`) is the conventional default and avoids confusion about whether other columns might also use star widths. Standardizing on `*` for both tables aligns the convention.

**Alternatives considered**: Keeping `2*` for Reports Name column — rejected for consistency since the star weight has no effect when it's the only star column.

---

### RQ-004: What is the correct approach for applying uniform cell padding?

**Decision**: Apply `Margin="4,0"` to the innermost content element (TextBlock, CheckBox, Border, StackPanel) within each cell template and to `DataGridTextColumn` via `ElementStyle`.

**Rationale**: The spec requires a 4-pixel horizontal margin on all cell content elements. The current state is inconsistent:
- Reports Name TextBlock already has `Margin="4,0"` ✓
- FilingsView Status badge has `Margin="4,2"` (close but vertical differs)
- FilingsView Actions StackPanel has `Margin="4,2"` (close but vertical differs)
- Most DataGridTextColumn cells have no explicit margin

For `DataGridTextColumn`, Avalonia supports `ElementStyle` to set a style on the generated TextBlock. We will use:
```xml
<DataGridTextColumn.ElementStyle>
    <Style Selector="TextBlock">
        <Setter Property="Margin" Value="4,0" />
    </Style>
</DataGridTextColumn.ElementStyle>
```

For `DataGridTemplateColumn`, we apply `Margin="4,0"` directly on the inner content element within the `<CellTemplate>`.

**Alternatives considered**:
- Setting `Padding` on the DataGrid cell container via a global style — rejected because Avalonia's `DataGridCell` Padding interacts with the Fluent theme's built-in cell padding and could cause double-spacing.
- Using a shared implicit style for all TextBlocks inside DataGrids — rejected because it would also affect header text and non-DataGrid TextBlocks.

---

### RQ-005: Does changing column widths affect existing sorting, selection, or command behavior?

**Decision**: No behavioral impact. Column width changes are purely presentational.

**Rationale**: 
- `Width` is a layout property that does not affect data binding, command execution, or event handling.
- Sorting is controlled by `Tag` attributes and the `DataGrid_Sorting` event handler — unchanged.
- Selection is bound via `IsSelected` on each row ViewModel — unchanged.
- Commands (`AdvanceStatusCommand`, `ExportCommand`, `DeleteCommand`, etc.) are bound in `CellTemplate` — unchanged.
- The `CanUserResizeColumns="True"` on FilingsView means users can override defaults at runtime — this is expected and desired.

**Alternatives considered**: None — this is a factual assessment, not a design choice.

---

### RQ-006: Are there any Avalonia DataGrid quirks for auto-width columns?

**Decision**: Use `Width="Auto"` with awareness that auto-width measures only visible rows.

**Rationale**: In Avalonia DataGrid (as in WPF DataGrid), `Width="Auto"` measures the content of currently rendered rows. With virtualization enabled (the default), only visible rows contribute to the auto-size measurement. Since our Actions columns contain template-rendered buttons that are identical in every row (same icons, same padding), the auto-size result will be consistent regardless of which rows are visible.

**Alternatives considered**: Disabling virtualization for accurate measurement — rejected because it would degrade performance for large datasets and is unnecessary when cell templates are uniform.
