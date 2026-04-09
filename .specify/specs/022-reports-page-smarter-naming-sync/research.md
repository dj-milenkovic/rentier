# Research: Reports Page – Smarter Naming and Sync Clarification

**Feature**: `003-reports-naming-sync` | **Date**: 2025-07-09

## Research Topics

### R-001: Where to compute the display name

**Decision**: Compute in `GetReportsQueryHandler` (Application layer) at query time.

**Rationale**: The display name is a derived read-model concern — it combines data from Report (ImportDate, ImporterId), Importer (DisplayName), and Filing (MIN IncomeDate). Computing it in the Application query handler keeps the logic centralized, testable with mocks, and avoids polluting Domain entities with display concerns. No database schema change is needed since nothing is persisted.

**Alternatives considered**:
- **Domain entity method on Report**: Rejected — would require Report to know about Filing and Importer, violating the aggregate boundary.
- **Computed column in SQL/EF**: Rejected — would require a cross-table join in a computed column, adding schema complexity for a display-only concern.
- **ViewModel-only derivation**: Rejected — display name assembly involves Application-layer knowledge (importer resolution, filing queries), which should not leak into the Desktop layer.

---

### R-002: New repository method for earliest income date

**Decision**: Add `GetEarliestIncomeDateByReportIdAsync(Guid reportId, CancellationToken ct)` returning `Task<DateOnly?>` to `IFilingRepository`.

**Rationale**: The existing `GetByReportIdAsync` returns full Filing entities — loading all filings just to extract a MIN date is wasteful. A dedicated method generates a single `SELECT MIN(IncomeDate) FROM Filings WHERE ReportId = @id` query via EF Core, which is the most efficient approach. Returns `null` when no filings exist, cleanly signalling the fallback-to-ImportDate path.

**Alternatives considered**:
- **Reuse `GetByReportIdAsync` + LINQ Min in handler**: Rejected — loads full entity graphs into memory for a single scalar value. Acceptable for <10 filings but wasteful at scale and sets a poor precedent.
- **Batch method returning `Dictionary<Guid, DateOnly?>`**: Considered — a single GROUP BY query across all report IDs would be more efficient (1 query vs N). However, the existing handler already uses a per-report loop for `GetFilingCountByReportIdAsync`, so the new method follows the same convention. A batch optimization can be introduced later as a separate performance improvement without changing the interface contract.

---

### R-003: DTO design for display name and original file name

**Decision**: Extend `ReportRowDto` with two new positional parameters: `DisplayName` (string) and `OriginalFileName` (string). The existing `ReportName` parameter is retained but its semantics shift to the original file name (already the case today). `DisplayName` is the new primary label.

**Rationale**: Records in C# support adding positional parameters. Keeping both values in the DTO allows the Desktop layer to bind the display name as the primary column text and the original file name as the tooltip, without needing secondary queries or ViewModel-level computation.

**Alternatives considered**:
- **Separate DTO for display name**: Rejected — adds unnecessary type proliferation for two string fields.
- **Remove `ReportName` from DTO**: Rejected — the original file name is needed for the tooltip (FR-007) and may be used elsewhere.

---

### R-004: Tooltip implementation in Avalonia DataGrid

**Decision**: Use `ToolTip.Tip` attached property on a `TextBlock` inside a `DataGridTemplateColumn.CellTemplate`.

**Rationale**: Avalonia's `DataGridTextColumn` does not natively support per-cell tooltips via binding. Replacing the first column with a `DataGridTemplateColumn` containing a `<TextBlock Text="{Binding DisplayName}" ToolTip.Tip="{Binding OriginalFileName}" />` is the standard Avalonia pattern for cell-level tooltips. This is a minimal change (one column definition) and follows Avalonia's attached property model.

**Alternatives considered**:
- **Custom CellStyle on DataGridTextColumn**: Limited tooltip binding support in Avalonia 11 DataGrid — unreliable.
- **Context menu or click-to-copy**: Rejected — tooltip is the most discoverable and low-friction UX for accessing the original name (matches spec FR-007 which explicitly says "hover").

---

### R-005: Sync clarification text placement

**Decision**: Add a `TextBlock` subtitle below the toolbar `StackPanel`, visible as an info line near the "Sync Mailboxes" button.

**Rationale**: A subtitle line is the least intrusive UI change. It does not require a new control, dialog, or layout restructure. The text is sourced from `Strings.resx` for localization (FR-010). The content explains what syncing does and differentiates it from the Sync page per FR-008 and FR-009.

**Alternatives considered**:
- **InfoBar/Banner control**: Considered — Avalonia's `InfoBar` (FluentAvalonia) would be visually prominent but adds a dependency and may feel heavy for static informational text.
- **Tooltip on the Sync button itself**: Rejected — tooltips require hover intent and are not discoverable enough (the spec says "subtitle or info banner" which implies always-visible text).
- **Inline button content with description**: Rejected — would make the button excessively wide and violate standard button sizing.

---

### R-006: En dash character handling

**Decision**: Use the literal en dash character `–` (U+2013) in the format string within `GetReportsQueryHandler`. Store the format pattern as a constant.

**Rationale**: The spec explicitly specifies an en dash (–), not a hyphen (-), as the separator between importer name and date. The en dash provides clear visual distinction. C# string literals handle Unicode natively. The format string `$"{importerName} \u2013 {date:yyyy-MM-dd}"` or `$"{importerName} – {date:yyyy-MM-dd}"` (with literal en dash) are equivalent.

**Alternatives considered**:
- **Hyphen (-)**: Rejected — spec explicitly uses en dash, and the visual difference matters for readability.
- **Localized separator from Strings.resx**: Over-engineered — the en dash is a formatting choice, not translatable content.

---

### R-007: Impact on existing tests

**Decision**: Existing `GetReportsQueryHandlerTests` will need updates because `ReportRowDto`'s constructor signature changes (two new positional parameters). All existing tests that construct or assert on `ReportRowDto` must be updated.

**Rationale**: Since `ReportRowDto` is a `record` with positional parameters, adding `DisplayName` and `OriginalFileName` changes the constructor. This is a compile-time breaking change within the test project. The fix is mechanical: update the mock setup to provide the new `GetEarliestIncomeDateByReportIdAsync` return value and update DTO assertions.

**Alternatives considered**:
- **Non-breaking DTO change (optional properties)**: Possible with `{ get; init; }` properties instead of positional parameters, but breaks the existing `record(...)` convention used across all DTOs in the project.
