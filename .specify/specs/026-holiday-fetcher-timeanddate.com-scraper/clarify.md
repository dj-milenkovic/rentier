# Clarification Notes: Holiday Fetcher — timeanddate.com Scraper

**Feature Branch**: `026-holiday-web-scraper`  
**Created**: 2025-07-18  
**Status**: Resolved — all clarifications answered with informed defaults

## Resolved Clarifications

### Q1: Merge vs. Replace Behavior

**Context**: The feature description says "imported/merged into the existing HolidayConf". The current implementation (TimeAndDateHolidayScraper + ViewModel) replaces the entire holiday list with the fetched results (`Entries.Clear()` followed by re-add).

**Decision**: **Merge by date**. Fetched holidays are added to the existing in-memory list. If a date already exists, the existing entry is preserved (no overwrite). Only genuinely new dates are inserted.

**Rationale**: Users may have manually edited holiday names or added custom entries. A full replace would destroy that work. De-duplication by date is the safest merge strategy since a given calendar date can only have one "is it a holiday?" answer for deadline calculations.

**Impact**: The ViewModel's ImportCommand needs modification — instead of `Entries.Clear()`, it should iterate fetched results and `Add()` only those whose date doesn't match any existing entry.

---

### Q2: Single-Year vs. Multi-Year Fetch Trigger

**Context**: The feature description mentions "currently selected year(s)" (plural). The current UI has a single `ImportYear` numeric input. The page also has StartYear/EndYear range controls.

**Decision**: **Dual mode**. The "Fetch from web" button fetches for all years in the StartYear–EndYear range. The separate `ImportYear` control may be removed or repurposed, since the year range already defines scope. If retained for quick single-year use, it should default to the current calendar year.

**Rationale**: The year range is already the primary filtering mechanism on the page. Fetching for the same range aligns with user expectations — "populate everything I'm looking at."

**Impact**: The FetchFromWebCommand changes from `ReactiveCommand<int, Unit>` to `ReactiveCommand<Unit, Unit>`, iterating `StartYear..EndYear` internally.

---

### Q3: User Confirmation Before Merge

**Context**: Should the system show a preview/confirmation dialog before merging fetched holidays into the list?

**Decision**: **No confirmation dialog**. Fetched holidays appear as unsaved changes in the editable grid. The user reviews them visually and must explicitly click "Save" to persist. This two-step flow (fetch → save) provides inherent safety without an extra dialog.

**Rationale**: The existing save-on-demand pattern already provides a review gate. Adding a confirmation dialog would slow down the workflow and is inconsistent with the add-row/delete-row patterns which also don't require confirmation before appearing in the grid.

---

### Q4: Naming Convention — Fetch vs. Import

**Context**: The existing code uses both "Fetch" and "Import" terminology. The View binds to `ImportCommand`, while the ViewModel declares `FetchFromWebCommand` (unused). The feature description uses "Fetch from web".

**Decision**: **Standardize on "Fetch from web"**. The button label, command property, and user-facing messages should consistently use "Fetch" terminology. "Import" implies file-based ingestion; "Fetch" correctly conveys network retrieval.

**Impact**: Rename `ImportCommand` → `FetchFromWebCommand` in ViewModel. Update AXAML binding. Update localization strings.

---

### Q5: Error Reporting Granularity for Multi-Year Fetch

**Context**: When fetching for a range (e.g., 2024–2028) and some years fail, how should errors be reported?

**Decision**: **Aggregate reporting with partial success**. After all years are attempted, display a single summary message listing successful years and failed years with reasons. Example: "Fetched holidays for 2024, 2025, 2027. Failed: 2026 (no holidays found), 2028 (network error)."

**Rationale**: Failing fast on the first error would leave the user unsure which years succeeded. Aggregate reporting gives full visibility.

---

## No Outstanding Clarifications

All ambiguities have been resolved with informed defaults documented above. The specification is ready for `/speckit.plan`.
