# Feature Specification: Settings Navigation Sub-menu Items in Sidebar

**Feature Branch**: `036-settings-navigation-submenu`  
**Created**: 2025-07-15  
**Status**: Draft  
**Input**: User description: "The Settings section in the main sidebar is restructured from a single 'Settings' navigation item (that shows a tabbed view with Profile / Holidays / Mailboxes / Importers / Language) into an expandable group with a dedicated sub-menu item for each settings section. Each item navigates directly to its settings view, giving users one-click access without first entering a shared Settings pane."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Navigate Directly to a Settings Sub-page (Priority: P1)

A user wants to change a specific setting (e.g., configure their taxpayer profile). Instead of clicking "Settings" and then selecting the correct tab, they click the "Profile" item directly in the sidebar and land on the profile settings view immediately.

**Why this priority**: This is the core value proposition — reducing clicks from two (Settings → tab) to one (sidebar child item) for every settings interaction. Every other story depends on this navigation restructure being in place.

**Independent Test**: Can be fully tested by clicking any child item under the Settings group in the sidebar and verifying the correct settings view renders in the content area.

**Acceptance Scenarios**:

1. **Given** the sidebar is visible and the Settings group is expanded, **When** the user clicks "Profile", **Then** the content area displays the Profile settings view.
2. **Given** the sidebar is visible and the Settings group is expanded, **When** the user clicks "Holidays", **Then** the content area displays the Holidays settings view.
3. **Given** the sidebar is visible and the Settings group is expanded, **When** the user clicks "Mailboxes", **Then** the content area displays the Mailboxes settings view.
4. **Given** the sidebar is visible and the Settings group is expanded, **When** the user clicks "Importers", **Then** the content area displays the Importers settings view.
5. **Given** the sidebar is visible and the Settings group is expanded, **When** the user clicks "Language", **Then** the content area displays the Language settings view.

---

### User Story 2 - Settings Group Collapse and Expand (Priority: P2)

A user who does not frequently use settings wants to collapse the Settings group to reduce visual clutter in the sidebar. When they need settings again, they expand the group to reveal the child items.

**Why this priority**: The collapsible group is the organizing mechanism that keeps the sidebar clean despite adding five new visible items. Without it, the sidebar would feel cluttered compared to the old single-item design.

**Independent Test**: Can be fully tested by toggling the Settings group header between collapsed and expanded states and verifying child items appear or disappear accordingly.

**Acceptance Scenarios**:

1. **Given** the Settings group is expanded, **When** the user clicks the Settings group header, **Then** all five child items (Profile, Holidays, Mailboxes, Importers, Language) are hidden.
2. **Given** the Settings group is collapsed, **When** the user clicks the Settings group header, **Then** all five child items become visible.
3. **Given** the application has just launched, **When** the sidebar renders, **Then** the Settings group is expanded by default so all child items are visible.

---

### User Story 3 - Active Sub-page Highlighting (Priority: P2)

A user navigating between different parts of the application can always see which settings sub-page they are currently on by looking at the sidebar highlight indicator.

**Why this priority**: Visual feedback is essential for orientation. Users must always know where they are in the application. This shares priority with collapse/expand because both are needed for a usable sidebar.

**Independent Test**: Can be fully tested by clicking each settings child item and verifying the selection indicator appears next to the active item only.

**Acceptance Scenarios**:

1. **Given** the user clicks "Holidays" in the Settings group, **When** the Holidays view loads, **Then** the "Holidays" child item displays the active selection indicator (same accent style as top-level nav items like Dashboard or Filings).
2. **Given** "Holidays" is the active settings sub-page, **When** the user clicks "Mailboxes", **Then** the active indicator moves from "Holidays" to "Mailboxes".
3. **Given** the user is on the Filings page, **When** they look at the sidebar, **Then** no Settings child item shows the active indicator, and the Filings top-level item shows it instead.

---

### User Story 4 - Navigation State Persistence Within Session (Priority: P3)

A user navigates to the Importers settings page, then switches to the Filings view to review tax filings. When they click back into any Settings child item, they can return to Importers (or whichever sub-page they last visited) if the application preserves navigation state.

**Why this priority**: State persistence is a quality-of-life improvement that reduces friction for users who switch between settings and other pages frequently. It is not required for basic functionality.

**Independent Test**: Can be fully tested by navigating to a settings sub-page, switching to a different top-level page, and then returning to the Settings area to verify the last-active sub-page is remembered.

**Acceptance Scenarios**:

1. **Given** the user is viewing the Importers settings sub-page, **When** they click "Filings" in the sidebar, **Then** the Filings view loads normally.
2. **Given** the user previously visited the Importers settings sub-page, **When** they click any Settings child item (e.g., "Importers" again), **Then** the Importers settings view loads, confirming state is accessible.
3. **Given** the user has never visited any settings sub-page during the current session, **When** they click the first Settings child item, **Then** that sub-page loads without error (no stale state from a previous session).

---

### User Story 5 - Removal of Tabbed Settings View (Priority: P1)

The old single "Settings" navigation item that led to a tabbed view with all settings sections combined is fully removed. Users can no longer encounter the tabbed layout; each settings section is only accessible through its own sidebar child item.

**Why this priority**: This is a structural prerequisite for the new navigation model. The tabbed view must be eliminated to avoid user confusion and duplicate navigation paths. Shares P1 with Story 1 because they are two sides of the same change.

**Independent Test**: Can be fully tested by verifying no tabbed settings container page exists and that clicking any settings child item renders a standalone view (not a tab within a shared container).

**Acceptance Scenarios**:

1. **Given** the new navigation structure is active, **When** the user browses all available sidebar items, **Then** there is no single "Settings" item that navigates to a tabbed container view.
2. **Given** the user clicks "Profile" in the Settings group, **When** the Profile view loads, **Then** it renders as a standalone page in the content area without any tab strip or tab host.

---

### Edge Cases

- What happens when the user rapidly clicks between different Settings child items? The content area should update to reflect only the most recently clicked item without rendering intermediate states.
- How does the sidebar behave when the Settings group is collapsed and the user is already viewing a settings sub-page? The active settings sub-page continues to display in the content area; the sidebar simply hides the child items visually.
- What happens if the application window is resized to a very narrow width? The sidebar retains its fixed width and the Settings group header and child items remain usable (no truncation of item labels within the standard sidebar width).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The sidebar MUST display a "Settings" group header that acts as a collapsible section toggle.
- **FR-002**: The Settings group MUST contain exactly five child navigation items: Profile, Holidays, Mailboxes, Importers, and Language, displayed in that order.
- **FR-003**: Each Settings child item MUST navigate the content area to its corresponding standalone settings view when clicked.
- **FR-004**: The Settings group MUST be expanded by default when the application launches.
- **FR-005**: Clicking the Settings group header MUST toggle the visibility of all child items between shown and hidden.
- **FR-006**: The active Settings child item MUST display the same selection indicator style used by top-level navigation items (accent-colored vertical pipe).
- **FR-007**: Only one navigation item (top-level or Settings child) MUST show the active indicator at any time.
- **FR-008**: The old tabbed SettingsView container MUST be removed; each settings sub-view MUST render as a standalone page.
- **FR-009**: Navigation state for settings sub-pages MUST persist for the duration of the application session (not across application restarts).
- **FR-010**: The Settings group header MUST display an expand/collapse visual affordance (e.g., a chevron icon that rotates to indicate state).
- **FR-011**: No existing data, business logic, or backend behavior MUST be altered by this change — this is a pure navigation and presentation refactor.
- **FR-012**: The top-level navigation items (Dashboard, Filings, Reports, Sync) MUST remain unchanged in appearance and behavior.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This change impacts only the Desktop (UI) layer. The sidebar navigation structure, view routing, and view composition are modified. Clean Architecture boundaries remain valid because no Application, Domain, or Infrastructure code is affected.
- **CA-002 (Money and Dates)**: No monetary or date fields are introduced or modified. Existing settings views that handle dates (e.g., Holidays) retain their current `DateOnly` usage unchanged.
- **CA-003 (Privacy and Security)**: No changes to data storage or credential handling. All settings data continues to be stored locally. Navigation state is held in-memory only and not persisted to disk.
- **CA-004 (Network Scope)**: No new outbound network calls are introduced. This is a purely local UI refactor.
- **CA-005 (Async and UI)**: Navigation transitions are synchronous property updates (ViewModel swapping via data binding). No I/O operations are involved in the navigation change itself. Individual settings views retain their existing async patterns for loading data.
- **CA-006 (Testing Impact)**: Desktop layer tests require updates — specifically ViewModel tests for the new navigation group behavior, collapse/expand state, and active item tracking. No Domain, Application, or Infrastructure test changes are needed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can reach any specific settings section in one click from the sidebar, reducing navigation steps from two (Settings → tab) to one.
- **SC-002**: The Settings group can be collapsed and expanded in under 0.5 seconds with no visible rendering lag.
- **SC-003**: The active settings sub-page is correctly indicated in the sidebar 100% of the time when a settings view is displayed in the content area.
- **SC-004**: All five settings sections (Profile, Holidays, Mailboxes, Importers, Language) are individually accessible and render identically to their previous tabbed versions.
- **SC-005**: Returning to a previously visited settings sub-page within the same session preserves navigation context without requiring the user to re-navigate.
- **SC-006**: The total number of visible sidebar items when Settings is collapsed remains the same as the current sidebar (five top-level items plus one group header replacing the old Settings item).
- **SC-007**: No existing user workflows for Dashboard, Filings, Reports, or Sync are affected by this change.

## Assumptions

- The existing settings sub-views (ProfileSettingsView, HolidaySettingsView, MailboxSettingsView, ImporterSettingsView, and the Language/Appearance settings view) are self-contained and do not depend on a parent TabControl host for layout or data context.
- The "Language" child item corresponds to the existing Appearance/Language settings view (previously the fifth tab in the tabbed SettingsView). If Feature 035 (Language Selection) renamed the Appearance tab, that name is carried forward here.
- Navigation state persistence means in-memory session state only — no disk persistence of which settings sub-page was last active. Restarting the application resets to the default expanded Settings group with no pre-selected child item.
- The sidebar width (220px) provides sufficient space to display indented child item labels without truncation.
- The collapsible group pattern introduced here is a one-off for Settings. If other navigation groups are needed in the future, that will be addressed by a separate feature.
- Keyboard navigation and screen-reader accessibility for the new group structure follow the same patterns as the existing sidebar ListBox items.
