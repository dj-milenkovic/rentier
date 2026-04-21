# UI Layout Contract: DataGrid Column Widths

**Feature**: 032-column-width-audit
**Date**: 2025-07-17
**Type**: Internal UI contract (no external API)

## Purpose

This contract defines the visual layout specifications for the Filings and Reports DataGrids. It serves as the authoritative reference for column sizing and cell padding that must be maintained across future feature changes.

## Contract: Filings DataGrid Columns

```
┌──────────┬────────┬─────────────┬──────────────┬──────────┬────────────┬─────────────┬─────────┐
│Selection │ Status │ Income Type │Paying Entity │ Deadline │Tax Payable │ Payment Ref │ Actions │
│  40px    │  90px  │   110px     │   * (fill)   │  120px   │   130px    │    180px    │  Auto   │
│ fixed    │ fixed  │   fixed     │   star       │  fixed   │   fixed    │    fixed    │  auto   │
└──────────┴────────┴─────────────┴──────────────┴──────────┴────────────┴─────────────┴─────────┘
```

**Minimum viewport for no horizontal scroll**: 670px fixed + ~100px auto + ~200px star ≈ **970px**

## Contract: Reports DataGrid Columns

```
┌──────────┬─────────────┬─────────────┬────────────┬──────────┬────────┬──────────┬─────────┐
│Selection │ Report Name │ Import Date │ Email Date │ Importer │ Status │ Filings  │ Actions │
│  40px    │  * (fill)   │   110px     │   110px    │  160px   │ 100px  │   70px   │  Auto   │
│ fixed    │   star      │   fixed     │   fixed    │  fixed   │ fixed  │  fixed   │  auto   │
└──────────┴─────────────┴─────────────┴────────────┴──────────┴────────┴──────────┴─────────┘
```

**Minimum viewport for no horizontal scroll**: 590px fixed + ~80px auto + ~200px star ≈ **870px**

## Contract: Cell Padding

All cell content elements across both DataGrids:

```
Margin = "4,0"  (4px left, 0px top, 4px right, 0px bottom)
```

Applies to:
- `TextBlock` elements in `DataGridTextColumn` (via `ElementStyle`)
- `CheckBox` elements in Selection column templates
- `Border` elements in Status badge templates
- `TextBox` elements in editable cell templates
- `StackPanel` elements in Actions column templates

## Invariants

1. **Exactly one star column per table** — Paying Entity (Filings) and Report Name (Reports)
2. **Actions columns MUST be auto-width** — never hardcoded pixel values
3. **All fixed columns MUST preserve their width on window resize**
4. **Star columns absorb all remaining space** — they shrink on narrow windows
5. **Cell padding is uniform** — no per-column or per-table variance in horizontal margin
6. **Behavioral neutrality** — column sizing MUST NOT affect sorting, selection, editing, or command execution

## Versioning

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-07-17 | Initial column width and padding standardization |
