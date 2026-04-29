# Data Model: Missing Serbian Translations Audit & Fix

**Feature**: 049-missing-translations-sr
**Date**: 2025-07-15

## Overview

This feature does not introduce new domain entities or database schema changes. It modifies only the Desktop layer's localization resources and UI bindings.

## Affected Data Structures

### 1. English Resource Keys (Strings.resx / Strings.Designer.cs)

**New keys to add** (12 total):

| Key | English Value | Category |
|-----|--------------|----------|
| `Update_Available_Format` | `Update v{0} available` | Update bar |
| `Update_Now_Button` | `Update Now` | Update bar |
| `Update_Later_Button` | `Later` | Update bar |
| `Update_Downloading_Format` | `Downloading update... {0}%` | Update bar |
| `Update_Ready_Text` | `Update ready. Restart to apply.` | Update bar |
| `Update_RestartNow_Button` | `Restart Now` | Update bar |
| `Update_Failed_Format` | `Update failed: {0}` | Update bar |
| `Update_Retry_Button` | `Retry` | Update bar |
| `Update_Dismiss_Button` | `Dismiss` | Update bar |
| `Holidays_Empty_Text` | `No holidays configured. Click Add or Fetch from web to get started.` | Holidays |
| `Reports_Col_Actions` | `Actions` | Reports |
| `ReportStatus_PartialError` | `Partial Error` | Enum display |

### 2. Serbian Resource Keys (SrLatnStrings.cs)

**New keys to add** (12 total):

| Key | Serbian (Latin) Value |
|-----|----------------------|
| `Update_Available_Format` | `Dostupno ažuriranje v{0}` |
| `Update_Now_Button` | `Ažuriraj sada` |
| `Update_Later_Button` | `Kasnije` |
| `Update_Downloading_Format` | `Preuzimanje ažuriranja... {0}%` |
| `Update_Ready_Text` | `Ažuriranje spremno. Restartujte da primenite.` |
| `Update_RestartNow_Button` | `Restartuj sada` |
| `Update_Failed_Format` | `Ažuriranje neuspešno: {0}` |
| `Update_Retry_Button` | `Ponovi` |
| `Update_Dismiss_Button` | `Odbaci` |
| `Holidays_Empty_Text` | `Nema konfigurisanih praznika. Kliknite Dodaj ili Preuzmi sa weba da počnete.` |
| `Reports_Col_Actions` | `Akcije` |
| `ReportStatus_PartialError` | `Delimična greška` |

### 3. Enum Display Converters (Modified)

**ReportStatusDisplayConverter** — add `PartialError` case:
```csharp
ReportStatus.PartialError => Strings.ReportStatus_PartialError,
```

### 4. ViewModel Changes (MainWindowViewModel)

New computed properties for localized format strings:
- `UpdateAvailableText` — formats `Update_Available_Format` with version
- `DownloadingText` — formats `Update_Downloading_Format` with progress %
- `UpdateFailedText` — formats `Update_Failed_Format` with error message

These replace the AXAML `StringFormat` patterns that bypass localization.

## Validation Rules

- All 12 new keys must appear in both English and Serbian dictionaries
- Format placeholders (`{0}`, `{1}`) must be preserved in translations
- Serbian text must use Latin script exclusively (no Cyrillic characters)
- No existing keys may be renamed or removed (FR-009)
