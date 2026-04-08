# Feature Specification: Dashboard with Deadline Alerts

**Feature Branch**: `005-dashboard-deadline-alerts`  
**Created**: 2025-07-18  
**Status**: Draft  
**Input**: User description: "Implement a dashboard for Rentier. Add a new Dashboard navigation entry (first position in sidebar, before Filings/Reports). Shows upcoming deadlines, overdue filings, summary cards, and last sync timestamp."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Upcoming Filing Deadlines (Priority: P1)

As a taxpayer, I want to see all filings with approaching deadlines (within the next 30 days) in a single table when I open the application, so I can prioritize which filings to act on before they become overdue.

The dashboard is the first screen the user sees when opening Rentier. A DataGrid displays filings with status Init or Filed whose deadline falls within the next 30 days, sorted by deadline ascending (soonest first). Each row shows the paying entity, income type, filing deadline date, tax payable amount (formatted as RSD currency), and current status. Clicking a row navigates to the Filings pane using the existing sidebar navigation pattern — no separate detail view is opened.

**Why this priority**: This is the core value proposition of the dashboard — proactive visibility into time-sensitive work. Without upcoming deadlines, the dashboard provides no actionable guidance.

**Independent Test**: Can be fully tested by creating filings with various deadline dates and verifying the DataGrid displays only those within 30 days, sorted correctly, and delivers immediate deadline awareness.

**Acceptance Scenarios**:

1. **Given** there are 3 filings with status Init and deadlines within 30 days and 2 filings with deadlines beyond 30 days, **When** the user navigates to the Dashboard, **Then** only the 3 filings within 30 days are shown in the DataGrid sorted by deadline ascending.
2. **Given** there are filings with status Paid and deadline within 30 days, **When** the user navigates to the Dashboard, **Then** those Paid filings are not displayed in the upcoming deadlines grid.
3. **Given** there are filings with status Filed and deadline within 30 days, **When** the user navigates to the Dashboard, **Then** those Filed filings appear in the upcoming deadlines grid.
4. **Given** there are no filings with deadlines within 30 days, **When** the user navigates to the Dashboard, **Then** the DataGrid shows an empty state message indicating no upcoming deadlines.
5. **Given** the user opens the application, **When** the main window appears, **Then** the Dashboard is the default selected navigation entry and is displayed immediately.
6. **Given** the user clicks a row in the upcoming deadlines DataGrid, **When** the click is processed, **Then** the sidebar selection switches to the Filings entry and the Filings pane is displayed (no detail flyout or modal is opened).

---

### User Story 2 - See Overdue Filings Alert (Priority: P1)

As a taxpayer, I want to see a prominently highlighted count and list of overdue filings (deadline has passed and not yet paid) so I can immediately identify filings that require urgent action.

A count badge displays the total number of overdue filings. Below or alongside the badge, a list shows each overdue filing with its paying entity, deadline date, and tax payable amount. Overdue filings are visually highlighted in red to convey urgency.

**Why this priority**: Overdue filings carry legal and financial consequences. This is equal priority with upcoming deadlines because missing a deadline is the most critical risk the dashboard should surface.

**Independent Test**: Can be fully tested by creating filings with past deadlines in Init and Filed status and verifying the count badge and list render correctly with red highlighting.

**Acceptance Scenarios**:

1. **Given** there are 5 filings with deadline before today and status Init or Filed, **When** the user views the Dashboard, **Then** the overdue badge displays "5" and the list shows all 5 filings highlighted in red.
2. **Given** there are overdue filings but all have status Paid, **When** the user views the Dashboard, **Then** the overdue count is 0 and no overdue list is displayed.
3. **Given** there are no overdue filings, **When** the user views the Dashboard, **Then** the overdue badge displays "0" (or is hidden) and the overdue list section shows an empty state.
4. **Given** a filing has deadline equal to today and status Init, **When** the user views the Dashboard, **Then** that filing does not appear in the overdue list (deadline must be strictly before today to be overdue).

---

### User Story 3 - View Summary Statistics (Priority: P2)

As a taxpayer, I want to see summary cards showing total unpaid tax and filing counts by status so I can quickly understand my overall tax obligation and filing progress at a glance.

Summary cards display: (1) total unpaid tax as a decimal amount formatted in RSD, (2) count of filings in Init status, (3) count of filings in Filed status, (4) count of filings in Paid status. The cards use styled text blocks or card-style containers for visual clarity.

**Why this priority**: Summary statistics provide useful context but are informational — they don't drive immediate action like deadline awareness does.

**Independent Test**: Can be fully tested by creating filings across all three statuses with known TaxPayableRsd values and verifying the counts and total match expected values.

**Acceptance Scenarios**:

1. **Given** there are 3 Init filings (TaxPayableRsd: 1000.00, 2500.50, 500.00), 2 Filed filings (TaxPayableRsd: 3000.00, 1500.00), and 1 Paid filing, **When** the user views the Dashboard, **Then** the Init count shows 3, Filed count shows 2, Paid count shows 1, and total unpaid tax shows 8,500.50 RSD (sum of Init + Filed TaxPayableRsd).
2. **Given** there are no filings in the system, **When** the user views the Dashboard, **Then** all counts show 0 and total unpaid tax shows 0.00 RSD.
3. **Given** all filings have status Paid, **When** the user views the Dashboard, **Then** total unpaid tax shows 0.00 RSD, Init count shows 0, Filed count shows 0, and Paid count reflects the total.

---

### User Story 4 - View Last Sync Timestamp (Priority: P3)

As a taxpayer, I want to see when the last successful mailbox sync occurred so I know whether my filing data is up to date.

A text element on the dashboard displays the last sync date. If no sync has ever occurred (no mailbox configured or cursor is null), the dashboard shows a message indicating no sync has been performed.

**Why this priority**: This is supplementary information. It builds confidence in data freshness but is not critical for decision-making.

**Independent Test**: Can be fully tested by configuring a mailbox with a known LastSyncDate and verifying the dashboard displays that date, or by having no mailbox and verifying the "no sync" message.

**Acceptance Scenarios**:

1. **Given** a mailbox exists with LastSyncDate of 2025-07-15, **When** the user views the Dashboard, **Then** the last sync date displays "2025-07-15" (or locale-appropriate format).
2. **Given** no mailbox is configured, **When** the user views the Dashboard, **Then** the last sync section shows a message indicating that no sync has been performed.
3. **Given** a mailbox exists but LastSyncDate is null (initial state, no sync yet), **When** the user views the Dashboard, **Then** the last sync section shows a message indicating that no sync has been performed.

---

### User Story 5 - Dashboard Navigation Entry (Priority: P1)

As a taxpayer, I want the Dashboard to be the first item in the sidebar navigation so that it is the default screen when I open the application and I can return to it easily.

A "Dashboard" navigation entry appears at the top of the sidebar, before Filings, Reports, and Settings. When the application launches, Dashboard is selected by default and its view is displayed.

**Why this priority**: Without the navigation entry, the dashboard is unreachable. This is foundational infrastructure for all other stories.

**Independent Test**: Can be fully tested by launching the application and verifying the sidebar shows Dashboard as the first entry and it is selected by default.

**Acceptance Scenarios**:

1. **Given** the user launches the application, **When** the main window appears, **Then** the sidebar shows Dashboard as the first entry, followed by Filings, Reports, and Settings.
2. **Given** the user is on the Filings screen, **When** the user clicks Dashboard in the sidebar, **Then** the dashboard view is displayed.
3. **Given** the user is on the Dashboard, **When** the user clicks another navigation entry and then clicks Dashboard again, **Then** the dashboard data is refreshed and displayed.

---

### Edge Cases

- What happens when the system has thousands of filings? The upcoming deadlines grid is bounded by the 30-day window and only Init/Filed status, limiting the result set naturally. If the count is still large, the grid should remain performant without pagination (since the 30-day window is a natural limiter).
- What happens if the same filing appears in both the upcoming deadlines and overdue lists? This cannot occur — a filing with deadline < today is overdue (not upcoming), and a filing with deadline >= today and within 30 days is upcoming (not overdue).
- What happens if the database is empty (no filings, no mailbox)? The dashboard shows zero counts, 0.00 RSD for total unpaid tax, an empty upcoming deadlines grid with an empty-state message, no overdue filings, and a "no sync performed" message.
- What happens if a filing has a deadline exactly 30 days from today? It should be included in the upcoming deadlines list (the 30-day window is inclusive of the boundary day).
- What happens if the dashboard data changes while the user is viewing it (e.g., a background sync adds new filings)? The dashboard loads data when navigated to. Users can re-navigate to Dashboard to refresh. Real-time push updates are out of scope.
- What happens when the dashboard query encounters a data access error? The dashboard displays a user-friendly error message and allows the user to retry.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a Dashboard screen accessible via the sidebar navigation as the first entry.
- **FR-002**: System MUST display the Dashboard as the default screen when the application launches.
- **FR-003**: System MUST display a DataGrid of upcoming filings with status Init or Filed and deadline within 30 days from today (inclusive), sorted by deadline ascending.
- **FR-004**: Each row in the upcoming deadlines DataGrid MUST show: paying entity, income type, filing deadline, tax payable (formatted as RSD currency), and status.
- **FR-005**: System MUST display a count badge showing the number of overdue filings (deadline strictly before today and status not Paid).
- **FR-006**: System MUST display a list of overdue filings with paying entity, deadline, and tax payable amount.
- **FR-007**: Overdue filings MUST be visually highlighted in red.
- **FR-008**: System MUST display summary cards showing: total unpaid tax (decimal, formatted as RSD), count of Init filings, count of Filed filings, and count of Paid filings.
- **FR-009**: Total unpaid tax MUST be the sum of TaxPayableRsd for all filings with status Init or Filed.
- **FR-010**: System MUST display the last successful sync date from the mailbox cursor.
- **FR-011**: When no mailbox is configured or the sync date is null, the system MUST display a message indicating no sync has been performed.
- **FR-012**: All user-facing strings MUST be defined in the localization resource file.
- **FR-013**: All monetary values MUST use decimal type and be displayed with RSD currency formatting.
- **FR-014**: All date values MUST use DateOnly type.
- **FR-015**: The dashboard layout MUST be responsive, adapting to different window sizes.
- **FR-016**: The dashboard MUST load data asynchronously and show a loading indicator during data retrieval.
- **FR-017**: The dashboard MUST display a user-friendly error message if data retrieval fails, with an option to retry.
- **FR-018**: No new database schema changes (tables or columns) are required — the dashboard is a pure read-only view over existing data.
- **FR-019**: Two new read-only query methods MUST be added to `IFilingRepository`: `GetUpcomingAsync(DateOnly today, int days, CancellationToken ct)` returning filings with status Init or Filed and deadline in [today, today+days]; and `GetOverdueAsync(DateOnly today, CancellationToken ct)` returning filings with status Init or Filed and deadline strictly before today. Summary aggregates (InitCount, FiledCount, PaidCount, TotalUnpaidRsd) are computed in the query handler from a separate `GetAllAsync` call.
- **FR-020**: Clicking a row in the upcoming deadlines DataGrid MUST navigate to the Filings pane by setting the sidebar `SelectedEntry` to the Filings navigation entry. No detail view or modal is opened.
- **FR-021**: `DashboardViewModel` MUST be registered as `AddTransient<DashboardViewModel>()` and injected as the first constructor parameter of `MainWindowViewModel`. The Dashboard `NavigationEntry` MUST be inserted at index 0 in `NavigationEntries`, before Filings, Reports, and Settings.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: This feature impacts all four layers. Domain layer is read-only (no new entities). Application layer adds a new query (`GetDashboardQuery`), a query handler (`GetDashboardQueryHandler`), and three DTOs (DashboardDto, UpcomingDeadlineDto, OverdueFilingDto). Infrastructure layer adds two new methods to `IFilingRepository` (`GetUpcomingAsync`, `GetOverdueAsync`) and their EF Core implementations. Desktop layer adds `DashboardViewModel`, `DashboardView.axaml`, a `Strings.Nav_Dashboard` resource key, and updates `MainWindowViewModel` constructor to accept `DashboardViewModel` as its first parameter. Clean Architecture boundaries are preserved — data flows inward only.
- **CA-002 (Money and Dates)**: TaxPayableRsd and TotalUnpaidRsd use `decimal`. FilingDeadline, IncomeDate, and LastSyncDate use `DateOnly`. All monetary display uses RSD formatting.
- **CA-003 (Privacy and Security)**: All data is local-first (read from local SQLite database). No credentials are accessed or stored by this feature. No external data exposure.
- **CA-004 (Network Scope)**: This feature makes zero outbound network calls. All data is read from the local database.
- **CA-005 (Async and UI)**: The dashboard query handler runs asynchronously. The ViewModel loads data via async commands. UI updates are scheduled on the main thread scheduler. No blocking I/O on the UI thread.
- **CA-006 (Testing Impact)**: Application layer tests for the query handler (verify correct filtering, sorting, aggregation, max LastSyncDate logic). Desktop layer tests for the ViewModel (verify property bindings, loading states, error handling, row-click navigation command). Infrastructure layer tests for `GetUpcomingAsync` and `GetOverdueAsync` (verify boundary conditions: today inclusive, today-1 overdue, 30-day window inclusive). No domain tests needed (no new domain logic).

### Key Entities *(include if feature involves data)*

- **Filing**: Existing entity representing a PP-OPO tax filing. Key attributes for dashboard: Id, Status (Init/Filed/Paid), PayingEntity, IncomeType, IncomeDate (DateOnly), FilingDeadline (DateOnly), TaxPayableRsd (decimal). Used to compute upcoming deadlines, overdue filings, status counts, and total unpaid tax.
- **Mailbox**: Existing entity representing an IMAP mailbox connection. Key attribute for dashboard: `Mailbox.Cursor.LastSyncDate` (`DateOnly?`) via the `MailboxCursor` value object. The dashboard handler calls `IMailboxRepository.GetAllAsync()` and returns the **maximum** `Cursor.LastSyncDate` across all mailboxes. If no mailbox exists or all `LastSyncDate` values are null, `LastSyncDate` in the DTO is null and the UI shows the "no sync performed" message.
- **DashboardDto**: New read-only data transfer object aggregating all dashboard data. Exact shape: `record DashboardDto(IReadOnlyList<UpcomingDeadlineDto> UpcomingDeadlines, IReadOnlyList<OverdueFilingDto> OverdueFilings, int InitCount, int FiledCount, int PaidCount, decimal TotalUnpaidRsd, DateOnly? LastSyncDate)`.
- **UpcomingDeadlineDto**: New read-only data transfer object for a single row in the upcoming deadlines grid. Exact shape: `record UpcomingDeadlineDto(Guid Id, string PayingEntity, DateOnly FilingDeadline, decimal TaxPayableRsd, FilingStatus Status, IncomeType IncomeType)`.
- **OverdueFilingDto**: New read-only data transfer object for a single row in the overdue filings list. Exact shape: `record OverdueFilingDto(Guid Id, string PayingEntity, DateOnly FilingDeadline, decimal TaxPayableRsd)`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can identify their most urgent filing deadline within 3 seconds of opening the application.
- **SC-002**: Users can see the total number and list of overdue filings within 3 seconds of opening the application.
- **SC-003**: The dashboard accurately displays all filings with deadlines in the next 30 days — zero false positives (filings outside the window) and zero false negatives (missed filings within the window).
- **SC-004**: The total unpaid tax amount matches the exact sum of TaxPayableRsd for all Init and Filed filings, with no rounding errors.
- **SC-005**: Filing counts by status (Init, Filed, Paid) match the actual counts in the system with 100% accuracy.
- **SC-006**: The dashboard loads and displays all data within 2 seconds for a dataset of up to 10,000 filings.
- **SC-007**: All dashboard text is localizable — no hardcoded strings in the view layer.
- **SC-008**: Users can determine data freshness by viewing the last sync date, or a clear message when no sync has occurred.

## Assumptions

- The 30-day window for upcoming deadlines is calculated relative to the current system date (today) at the time of query execution.
- "Overdue" means the filing deadline is strictly before today (deadline < today). A filing due today is not overdue.
- The 30-day upcoming window is inclusive: filings with deadline from today through today + 30 days are included.
- Total unpaid tax includes filings in both Init and Filed statuses (all non-Paid filings).
- The last sync date is sourced by calling `IMailboxRepository.GetAllAsync()` and selecting the maximum `Mailbox.Cursor.LastSyncDate` across all returned mailboxes. If no mailboxes exist, or all `Cursor.LastSyncDate` values are null, the `DashboardDto.LastSyncDate` is null and the UI displays the "no sync performed" message.
- The dashboard does not provide real-time updates. Data is loaded when the dashboard is navigated to. Users re-navigate to refresh.
- `IFilingRepository.GetUpcomingAsync(DateOnly today, int days, CancellationToken ct)` and `IFilingRepository.GetOverdueAsync(DateOnly today, CancellationToken ct)` are added as purpose-built read methods. Summary aggregates (InitCount, FiledCount, PaidCount, TotalUnpaidRsd) are computed in the query handler from a separate `IFilingRepository.GetAllAsync()` call to avoid duplicating filter logic.
- RSD currency formatting follows the Serbian locale convention (e.g., "8.500,50 RSD" or "8,500.50 RSD" depending on locale settings). The application uses the system's current culture for formatting.
- The dashboard does not support filtering or sorting controls — it presents a fixed, curated view. Users who need detailed filing management use the existing Filings screen.
- Service registration uses AddTransient only (except MainWindowViewModel which remains AddSingleton), consistent with the existing Desktop service provider pattern.
- `DashboardViewModel` is registered as `AddTransient<DashboardViewModel>()`. `MainWindowViewModel` is updated to accept `DashboardViewModel` as its first constructor parameter and inserts it as index 0 in `NavigationEntries`.

## Clarifications

### Session 2025-07-18

- Q: Should `IFilingRepository` use `GetAllAsync` or purpose-built methods for the dashboard queries? → A: Add `GetUpcomingAsync(DateOnly today, int days, CancellationToken ct)` and `GetOverdueAsync(DateOnly today, CancellationToken ct)` to `IFilingRepository` for the deadline lists; compute aggregates (InitCount, FiledCount, PaidCount, TotalUnpaidRsd) from a separate `GetAllAsync` call in the handler.
- Q: How is the `LastSyncDate` resolved when multiple mailboxes exist? → A: Call `IMailboxRepository.GetAllAsync()` and return the maximum `Mailbox.Cursor.LastSyncDate` across all mailboxes; null if no mailbox exists or all cursors are null.
- Q: What are the exact property names and types for `DashboardDto`, `UpcomingDeadlineDto`, and `OverdueFilingDto`? → A: `record DashboardDto(IReadOnlyList<UpcomingDeadlineDto> UpcomingDeadlines, IReadOnlyList<OverdueFilingDto> OverdueFilings, int InitCount, int FiledCount, int PaidCount, decimal TotalUnpaidRsd, DateOnly? LastSyncDate)`; `record UpcomingDeadlineDto(Guid Id, string PayingEntity, DateOnly FilingDeadline, decimal TaxPayableRsd, FilingStatus Status, IncomeType IncomeType)`; `record OverdueFilingDto(Guid Id, string PayingEntity, DateOnly FilingDeadline, decimal TaxPayableRsd)`.
- Q: How is `DashboardViewModel` wired into `MainWindowViewModel`? → A: Add `DashboardViewModel` as the first constructor parameter of `MainWindowViewModel`; register `AddTransient<DashboardViewModel>()`; insert as `NavigationEntries[0]` before Filings, Reports, and Settings.
- Q: What happens when a user clicks a row in the upcoming deadlines DataGrid? → A: Clicking a row navigates to the Filings pane by setting `MainWindowViewModel.SelectedEntry` to the Filings `NavigationEntry` using the existing sidebar navigation pattern; no detail flyout or modal is opened.
