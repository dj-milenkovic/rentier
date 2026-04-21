# Data Model: Column Width Audit — Filings & Reports Tables

**Feature**: 032-column-width-audit
**Date**: 2025-07-17
**Status**: Complete

## Overview

This feature is a **purely presentational change** — no domain entities, value objects, database tables, or ViewModel properties are added, modified, or removed. The data model documentation below captures the **view-layer layout constants** that constitute the "model" for this feature.

## Column Width Specifications

### Filings DataGrid Column Layout

| # | Column | Binding / Template | Width Type | Width Value | Padding (Margin) |
|---|--------|--------------------|-----------|-------------|------------------|
| 1 | Selection | `IsSelected` (CheckBox) | Fixed | `40` | `4,0` on CheckBox |
| 2 | Status | `StatusDisplayText` (Badge) | Fixed | `90` | `4,0` on Border |
| 3 | Income Type | `IncomeType` (via converter) | Fixed | `110` | `4,0` via ElementStyle |
| 4 | Paying Entity | `PayingEntity` | Star | `*` | `4,0` via ElementStyle |
| 5 | Filing Deadline | `DeadlineDisplay` | Fixed | `120` | `4,0` via ElementStyle |
| 6 | Tax Payable | `TaxPayableDisplay` | Fixed | `130` | `4,0` via ElementStyle |
| 7 | Payment Reference | `PaymentReference` (TextBox) | Fixed | `180` | `4,0` on TextBox |
| 8 | Actions | Commands (3 buttons) | Auto | `Auto` | `4,0` on StackPanel |

**Total fixed allocation**: 40 + 90 + 110 + 120 + 130 + 180 = **670px** (+ auto Actions column)
**Flex column**: Paying Entity fills remaining space

### Reports DataGrid Column Layout

| # | Column | Binding / Template | Width Type | Width Value | Padding (Margin) |
|---|--------|--------------------|-----------|-------------|------------------|
| 1 | Selection | `IsSelected` (CheckBox) | Fixed | `40` | `4,0` on CheckBox |
| 2 | Report Name | `ReportName` / `DisplayName` | Star | `*` | `4,0` on TextBlock |
| 3 | Import Date | `ImportDateDisplay` | Fixed | `110` | `4,0` via ElementStyle |
| 4 | Email Date | `EmailDateDisplay` | Fixed | `110` | `4,0` via ElementStyle |
| 5 | Importer | `ImporterName` | Fixed | `160` | `4,0` via ElementStyle |
| 6 | Status | `Status` (via converter) | Fixed | `100` | `4,0` via ElementStyle |
| 7 | Filing Count | `FilingCount` | Fixed | `70` | `4,0` via ElementStyle |
| 8 | Actions | Commands (2 buttons) | Auto | `Auto` | `4,0` on StackPanel |

**Total fixed allocation**: 40 + 110 + 110 + 160 + 100 + 70 = **590px** (+ auto Actions column)
**Flex column**: Report Name fills remaining space

## Change Delta Matrix

### Filings DataGrid Changes

| Column | Current | Target | Change |
|--------|---------|--------|--------|
| Selection | `Width="42"` | `Width="40"` | Width adjustment |
| Status | `Width="84"` | `Width="90"` | Width adjustment |
| Income Type | `Width="96"` | `Width="110"` | Width adjustment |
| Paying Entity | `Width="*"` | `Width="*"` | No change |
| Filing Deadline | `Width="110"` | `Width="120"` | Width adjustment |
| Tax Payable | `Width="120"` | `Width="130"` | Width adjustment |
| Payment Reference | `Width="140"` | `Width="180"` | Width adjustment |
| Actions | `Width="108"` | `Width="Auto"` | Type change (fixed → auto) |
| All cells | Inconsistent | `Margin="4,0"` | Add/normalize margins |

### Reports DataGrid Changes

| Column | Current | Target | Change |
|--------|---------|--------|--------|
| Selection | `Width="44"` | `Width="40"` | Width adjustment |
| Report Name | `Width="2*"` | `Width="*"` | Star weight normalization |
| Import Date | `Width="96"` | `Width="110"` | Width adjustment |
| Email Date | `Width="96"` | `Width="110"` | Width adjustment |
| Importer | `Width="120"` | `Width="160"` | Width adjustment |
| Status | `Width="88"` | `Width="100"` | Width adjustment |
| Filing Count | `Width="56"` | `Width="70"` | Width adjustment |
| Actions | `Width="88"` | `Width="Auto"` | Type change (fixed → auto) |
| All cells | Inconsistent | `Margin="4,0"` | Add/normalize margins |

## State Transitions

Not applicable — this feature introduces no state changes.

## Validation Rules

- All fixed-width values MUST be positive integers.
- Exactly one column per table MUST use star (`*`) width.
- Actions columns MUST use `Auto` width (not fixed).
- All cell content elements MUST have `Margin="4,0"` (4px horizontal, 0px vertical).

## Entities Impacted

| Entity | Impact |
|--------|--------|
| `FilingRowViewModel` | **None** — no property or binding changes |
| `ReportRowViewModel` | **None** — no property or binding changes |
| `FilingsViewModel` | **None** — no command or collection changes |
| `ReportsViewModel` | **None** — no command or collection changes |
| `FilingsView.axaml` | **Modified** — column widths and cell margins |
| `ReportsView.axaml` | **Modified** — column widths and cell margins |
