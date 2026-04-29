# Feature Specification: Missing Serbian Translations Audit & Fix

**Feature Branch**: `043-missing-translations-sr`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "Audit and fix all missing Serbian (Latin script) translations across the application. Several UI elements are displaying English text or resource keys instead of proper Serbian translations."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sync Page Fully Translated (Priority: P1)

A Serbian user opens the Sync page to synchronize their email data. All labels, descriptions, dropdown options, and informational text on the page are displayed in Serbian (Latin script). The user sees "Preuzima nove mejlove od poslednje sinhronizacije" instead of "Fetches new emails since last sync", and "Duplikati se preskaču" instead of "Duplicates are skipped". All dropdown items (sync mode, duplicate strategy, importer selections) display Serbian labels.

**Why this priority**: The Sync page is the entry point for new data — users interact with it frequently. English text here is immediately visible and breaks the fully-localized experience.

**Independent Test**: Can be fully tested by navigating to the Sync page with Serbian locale and verifying every visible text element is in Serbian.

**Acceptance Scenarios**:

1. **Given** the app is running with Serbian (Latin) locale, **When** the user navigates to the Sync page, **Then** all description text, button labels, info messages, and dropdown option labels are displayed in Serbian (Latin script) with no English text visible.
2. **Given** the app is running with Serbian (Latin) locale, **When** the user opens any dropdown on the Sync page, **Then** all option labels are displayed in Serbian (Latin script).
3. **Given** the app is running with Serbian (Latin) locale, **When** a sync operation completes or shows status messages, **Then** all feedback text is in Serbian (Latin script).

---

### User Story 2 - Filings Page Fully Translated (Priority: P1)

A Serbian user opens the Filings page to review their tax filings. All column headers, status values (e.g., "inicijalizovano", "prijavljeno", "plaćeno" instead of "init", "filed", "paid"), income type labels (tip prihoda), and any other text are displayed in Serbian (Latin script).

**Why this priority**: The Filings page contains critical tax data. Untranslated status and income type values prevent users from understanding their filing state at a glance.

**Independent Test**: Can be fully tested by navigating to the Filings page with data present and verifying every status badge, income type label, column header, and action button is in Serbian.

**Acceptance Scenarios**:

1. **Given** the app is running with Serbian (Latin) locale and filings exist with various statuses, **When** the user views the Filings page, **Then** all filing status values are displayed in Serbian (Latin script).
2. **Given** the app is running with Serbian (Latin) locale and filings exist with various income types, **When** the user views the Filings page, **Then** all income type values are displayed in Serbian (Latin script).
3. **Given** the app is running with Serbian (Latin) locale, **When** the user views any column header, label, or button on the Filings page, **Then** the text is in Serbian (Latin script).

---

### User Story 3 - Reports Page Fully Translated (Priority: P1)

A Serbian user opens the Reports page to review generated reports. All column headers, status values, action buttons, and informational text are displayed in Serbian (Latin script).

**Why this priority**: Reports are the output of the application's core workflow. English status labels here undermine trust in the tool's completeness.

**Independent Test**: Can be fully tested by navigating to the Reports page with reports in various statuses and verifying all text is in Serbian.

**Acceptance Scenarios**:

1. **Given** the app is running with Serbian (Latin) locale and reports exist with various statuses, **When** the user views the Reports page, **Then** all report status values are displayed in Serbian (Latin script).
2. **Given** the app is running with Serbian (Latin) locale, **When** the user views the Reports page, **Then** all column headers, labels, and buttons are in Serbian (Latin script).

---

### User Story 4 - Dashboard and Settings Pages Fully Translated (Priority: P2)

A Serbian user navigates through the Dashboard and all Settings sub-pages (Profile, Holidays, Mailboxes, Importers, Appearance). All text including labels, placeholders, tooltips, informational messages, and the update notification bar are displayed in Serbian (Latin script).

**Why this priority**: While less data-dense than Filings/Reports, these pages still contribute to the overall localized experience. The update notification bar in particular is visible globally across all pages.

**Independent Test**: Can be fully tested by navigating to each page and sub-page and verifying all visible text is in Serbian.

**Acceptance Scenarios**:

1. **Given** the app is running with Serbian (Latin) locale, **When** the user navigates to the Dashboard, **Then** all text elements are in Serbian (Latin script).
2. **Given** the app is running with Serbian (Latin) locale, **When** the user navigates through each Settings sub-page, **Then** all labels, placeholders, tooltips, and informational text are in Serbian (Latin script).
3. **Given** the app is running with Serbian (Latin) locale and an update notification is visible, **When** the user sees the update bar, **Then** all button labels and messages ("Ažuriraj sada", "Kasnije", "Ažuriranje spremno. Restartujte da primenite.", "Restartuj sada", "Ponovi", "Odbaci") are in Serbian (Latin script).

---

### User Story 5 - Complete Translation Parity Audit (Priority: P2)

A developer or QA reviewer verifies that every string key defined in the English resource source has a corresponding Serbian (Latin) translation entry. No key is left untranslated or missing from the Serbian translation dictionary.

**Why this priority**: Ensures systematic completeness beyond page-by-page checking, preventing future regressions where new keys are added in English but not in Serbian.

**Independent Test**: Can be tested by comparing the full set of English resource keys against the Serbian translation dictionary and confirming 100% coverage.

**Acceptance Scenarios**:

1. **Given** the English resource source defines N string keys, **When** the Serbian translation dictionary is inspected, **Then** it contains exactly N entries with no missing keys.
2. **Given** a new string key was recently added to the English resources, **When** the Serbian translations are checked, **Then** the new key has a corresponding Serbian translation.

---

### Edge Cases

- What happens when a translation value contains special characters (e.g., š, č, ž, đ, ć)? They must render correctly.
- What happens when a translation string contains format placeholders (e.g., `{0}`, `{1}`)? Placeholders must be preserved and function correctly with Serbian text.
- What happens when a translated string is significantly longer than its English equivalent? The UI layout must accommodate the longer text without truncation or overflow.
- What happens if the English fallback locale is active? English text should display as before — no regressions to the English locale.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: All user-visible text on the Sync page MUST be displayed in Serbian (Latin script) when the Serbian locale is active, including description text, info text, dropdown labels, and button text.
- **FR-002**: All enum display values (filing statuses, income types, report statuses, sync modes, duplicate strategies) MUST have Serbian (Latin script) translations and display correctly in grids, dropdowns, and status badges.
- **FR-003**: All user-visible text on the Filings page MUST be displayed in Serbian (Latin script), including column headers, status values, income type labels, action buttons, and any informational text.
- **FR-004**: All user-visible text on the Reports page MUST be displayed in Serbian (Latin script), including column headers, status values, action buttons, and any informational text.
- **FR-005**: All user-visible text on the Dashboard page MUST be displayed in Serbian (Latin script).
- **FR-006**: All user-visible text on Settings sub-pages (Profile, Holidays, Mailboxes, Importers, Appearance) MUST be displayed in Serbian (Latin script), including labels, placeholders, and tooltips.
- **FR-007**: The update notification bar MUST display all text (button labels, status messages) in Serbian (Latin script) when the Serbian locale is active.
- **FR-008**: Every string key in the English resource source MUST have a corresponding entry in the Serbian (Latin) translation dictionary — no keys may be missing.
- **FR-009**: Existing resource keys MUST NOT be renamed or changed — only translation values may be added or updated.
- **FR-010**: All translated strings containing format placeholders MUST preserve those placeholders in the correct positions.
- **FR-011**: The English locale MUST NOT be affected — no regressions to existing English translations.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Only the Desktop (UI) layer is impacted. Translation strings live in the resource/localization files within the Desktop project. No changes to Domain, Application, or Infrastructure layers. Clean Architecture boundaries remain valid.
- **CA-002 (Money and Dates)**: Not applicable — no monetary or date field changes. Existing date/money formatting in Serbian locale is out of scope.
- **CA-003 (Privacy and Security)**: Not applicable — translation strings contain no sensitive data. Local-first storage is unaffected.
- **CA-004 (Network Scope)**: Not applicable — no network calls involved. Translations are embedded in the application.
- **CA-005 (Async and UI)**: Not applicable — translation lookups are synchronous in-memory dictionary reads. No I/O involved.
- **CA-006 (Testing Impact)**: Desktop tests should verify that every English resource key has a matching Serbian translation key (parity test). No Domain, Application, or Infrastructure test changes needed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of English resource keys have corresponding Serbian (Latin) translation entries — zero missing keys.
- **SC-002**: A user navigating through all five main sections (Dashboard, Sync, Filings, Reports, Settings) encounters zero English text or raw resource keys when the Serbian locale is active.
- **SC-003**: All enum display values (filing statuses, income types, report statuses, sync modes, duplicate strategies) display localized Serbian text in every location they appear.
- **SC-004**: The update notification bar displays fully translated Serbian text for all states (update available, downloading, ready to restart, error/retry).
- **SC-005**: No regressions in the English locale — all existing English text continues to display correctly.

## Assumptions

- Serbian Latin script (sr-Latn) is the only target locale for this feature; Cyrillic (sr-Cyrl) is out of scope.
- The existing localization system (LocalizationService with static string dictionaries) will be reused — no new localization framework will be introduced.
- The English resource file (Strings.resx / Strings.Designer.cs) serves as the authoritative source of all string keys.
- Hardcoded English strings found in AXAML views (e.g., the update notification bar) will be moved to the resource system and translated, following the existing pattern.
- Translation quality for Serbian text will use natural, idiomatic Serbian (Latin script) appropriate for a financial/tax application context.
- UI layout already accommodates Serbian text lengths; if minor layout adjustments are needed for longer translations, they are in scope.
