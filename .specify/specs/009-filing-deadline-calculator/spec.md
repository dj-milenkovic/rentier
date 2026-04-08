# Feature Specification: Filing Deadline Calculator

**Feature Branch**: `009-filing-deadline-calculator`  
**Created**: 2026-05-30  
**Status**: Draft  
**Input**: Pure domain service — `FilingDeadlineCalculator.CalculateDeadline(DateOnly incomeDate, HolidayConf holidays) → DateOnly`

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Deadline Lands on a Plain Weekday (Priority: P1)

A developer computes a PP-OPO filing deadline for an income date whose +30-day candidate falls on a regular weekday not listed in the configured holidays. The calculator returns that date unchanged.

**Why this priority**: This is the happy-path and the most frequent case. Every other rule builds on top of this baseline.

**Independent Test**: Can be fully tested by supplying an income date whose +30 days lands on a known weekday (e.g. Thursday) with an empty holiday list, and verifying the returned date equals the income date plus 30 calendar days.

**Acceptance Scenarios**:

1. **Given** an income date of 2025-12-03 and an empty holiday list, **When** `CalculateDeadline` is called, **Then** the result is 2026-01-02 (Friday — no adjustment).
2. **Given** an income date of 2026-02-01 and an empty holiday list, **When** `CalculateDeadline` is called, **Then** the result is 2026-03-03 (Tuesday — no adjustment).

---

### User Story 2 — Deadline Lands on a Weekend (Priority: P1)

A developer computes a PP-OPO filing deadline whose +30-day candidate falls on a Saturday or Sunday. The calculator advances the candidate day-by-day until the first following weekday is reached.

**Why this priority**: Weekend-shift is the most common real-world adjustment; it must work perfectly before any holiday logic is layered on top.

**Independent Test**: Supply an income date whose +30 days lands on a known Saturday or Sunday with an empty holiday list, and verify the result is the following Monday.

**Acceptance Scenarios**:

1. **Given** an income date of 2026-01-02 and an empty holiday list, **When** `CalculateDeadline` is called, **Then** the result is 2026-02-02 (Monday; +30 = Sunday 2026-02-01, advanced one day).
2. **Given** an income date of 2026-01-01 and an empty holiday list, **When** `CalculateDeadline` is called, **Then** the result is 2026-02-02 (Monday; +30 = Saturday 2026-01-31, advanced two days).

---

### User Story 3 — Deadline Lands on a Configured Holiday (Priority: P1)

A developer computes a PP-OPO filing deadline whose +30-day candidate falls on a weekday that appears in `HolidayConf`. The calculator advances the candidate until a day that is neither a weekend nor a holiday is reached.

**Why this priority**: Holiday shifting is the primary regulatory requirement; the calculator exists specifically to handle this case correctly.

**Independent Test**: Supply a known holiday in `HolidayConf` and an income date whose +30 days lands on that holiday (a weekday), and verify the result is the next non-weekend, non-holiday day.

**Acceptance Scenarios**:

1. **Given** an income date of 2026-01-06 and holidays `[2026-02-05]`, **When** `CalculateDeadline` is called, **Then** the result is 2026-02-06 (Friday; +30 = Thursday 2026-02-05, holiday, advanced one day).
2. **Given** an income date of 2025-12-02 and holidays `[2026-01-01, 2026-01-02]`, **When** `CalculateDeadline` is called, **Then** the result is 2026-01-05 (Monday; +30 = Thursday 2026-01-01 → holiday, advance → Friday 2026-01-02 → holiday, advance → Saturday 2026-01-03 → weekend, advance → Sunday 2026-01-04 → weekend, advance → Monday 2026-01-05 → clean).

---

### User Story 4 — Safety Guard for Pathological Holiday Configuration (Priority: P2)

A developer accidentally configures 15 or more consecutive days as holidays (or a combination of weekends and holidays fills 14+ slots). The calculator throws a domain exception rather than looping indefinitely.

**Why this priority**: Protects the domain from silent infinite loops caused by misconfigured data; important for operational stability, but a rare scenario.

**Independent Test**: Supply a `HolidayConf` that blocks every day in a 15-day window after +30, and verify a `DomainException` is raised.

**Acceptance Scenarios**:

1. **Given** an income date and a holiday list containing 15 consecutive days starting at `incomeDate + 30`, **When** `CalculateDeadline` is called, **Then** a `DomainException` is thrown with a message referencing the 14-day safety limit.
2. **Given** a realistic maximum skip chain (5 consecutive holidays bridging a weekend, totalling 7 advances), **When** `CalculateDeadline` is called, **Then** the calculator completes without exception and returns the correct deadline.

---

### Edge Cases

- What happens when `+30` lands on a Saturday and the following Monday is a holiday?  
  → Saturday advances to Sunday (+1), Sunday to Monday (+1), Monday is a holiday (+1), Tuesday is clean → **Tuesday returned**. Three iterations consumed.

- What happens when `+30` lands on a holiday that is also a Saturday?  
  → Weekend rule fires first (Saturday → Sunday → Monday). The Saturday holiday entry has no additional effect; no extra advance is triggered.

- What happens when the calendar crosses a month or year boundary (e.g. Dec → Jan)?  
  → Calendar date arithmetic handles all Gregorian month and year rollovers correctly. No special handling needed.

- What happens for a leap-year income date (e.g. 2024-01-30)?  
  → Income date 2024-01-30 plus 30 days = 2024-02-29, a valid date in a leap year. If that date is a weekday and not a holiday, it is returned as-is.

- What happens with an empty `HolidayConf.Holidays`?  
  → The holiday check never fires; only weekend shifts apply. This is a valid configuration.

- What happens when a multi-day holiday period spans a weekend (e.g. Thursday–Monday of a long weekend)?  
  → Each calendar day is a separate entry in `HolidayConf.Holidays`. The loop advances one day at a time, checking both conditions on every iteration, until the first clean day is found.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `FilingDeadlineCalculator.CalculateDeadline(DateOnly incomeDate, HolidayConf holidays)` MUST return a `DateOnly` representing the last valid PP-OPO filing day (inclusive).
- **FR-002**: The base candidate MUST be the income date plus 30 calendar days.
- **FR-003**: If the candidate is Saturday or Sunday, the calculator MUST advance the candidate by one day and re-evaluate.
- **FR-004**: If the candidate is a weekday date contained in `holidays.Holidays`, the calculator MUST advance the candidate by one day and re-evaluate.
- **FR-005**: The loop MUST terminate and return the candidate as soon as it is simultaneously a weekday and not present in `holidays.Holidays`.
- **FR-006**: If the candidate has been advanced more than 14 times without resolving, the calculator MUST throw a `DomainException` with a message identifying the 14-day safety limit.
- **FR-007**: A `HolidayConf.Holidays` entry that coincides with a date already skipped by weekend logic MUST NOT trigger an additional advance; the loop evaluates only the current candidate on each iteration.
- **FR-008**: An empty `HolidayConf.Holidays` list MUST be accepted as valid input; the calculator MUST return a weekend-only-adjusted deadline.
- **FR-009**: The returned date is the last valid filing day (inclusive); downstream callers MUST NOT subtract one from the result.
- **FR-010**: The implementation MUST be covered by parametrized unit tests achieving 100 % branch coverage, with no I/O, no network access, and no async code.

### Constitution Alignment *(mandatory)*

- **CA-001 (Architecture)**: Feature touches **Domain layer only**. `FilingDeadlineCalculator` is a static domain service class in `Rentier.Domain/Services/`. No Application, Infrastructure, or Desktop layers are modified. Clean Architecture boundaries remain intact.
- **CA-002 (Money and Dates)**: All dates use `DateOnly`. No `DateTime`, no `TimeSpan`, no money fields. Fully compliant.
- **CA-003 (Privacy and Security)**: No user data stored or transmitted. Pure in-memory calculation. No privacy concerns.
- **CA-004 (Network Scope)**: No outbound calls. No external dependencies. Fully offline.
- **CA-005 (Async and UI)**: No async required — pure synchronous calculation. No UI components introduced.
- **CA-006 (Testing Impact)**: New test file required under `tests/Rentier.Domain.Tests/Services/`. Minimum 10 parametrized test cases covering all deadline-adjustment rules and edge cases. 100 % branch coverage required per constitution §V.

### Key Entities *(include if feature involves data)*

- **FilingDeadlineCalculator**: A stateless, static domain service. Accepts an income date and a holiday configuration; produces a `DateOnly` filing deadline. Contains no mutable state and holds no dependencies.
- **HolidayConf** *(existing, unchanged)*: A value object holding an ordered list of calendar dates — the set of public holiday dates for a tax year. Consumed as an input argument; not modified by this feature.
- **DomainException** *(existing)*: Thrown when the safety guard fires. Carries a human-readable message describing the pathological configuration.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 10 required test cases pass — one per deadline-adjustment rule and edge case — with zero test failures.
- **SC-002**: 100 % branch coverage is achieved on `FilingDeadlineCalculator` as reported by the project's code coverage tool, with no excluded branches.
- **SC-003**: The calculator completes a deadline calculation in under 1 millisecond for any valid input (verified by test execution time; no special benchmarks required).
- **SC-004**: A `DomainException` is reliably produced when a 15-consecutive-day blocked window is provided — confirmed by a dedicated test case.
- **SC-005**: Zero modifications to `HolidayConf` or any other existing domain type — confirmed by a clean `git diff` of all files outside the two new files.

## Assumptions

- **A-001**: `FilingDeadlineCalculator` is a stateless domain service class in the Domain layer, located alongside other domain services (`src/Rentier.Domain/Services/`). It holds no mutable state and carries no dependencies.
- **A-002**: The calculator accepts exactly two inputs — an income date and a holiday configuration — and returns a single date. No overloads are defined in this feature.
- **A-003**: The holiday configuration is assumed non-null by contract; the existing `HolidayConf` type already enforces this upon construction. No redundant guard is needed inside the calculator.
- **A-004**: A holiday configuration with zero entries is a valid input (e.g. a year with no public holidays). The calculator applies weekend-only shifting in this case.
- **A-005**: On every iteration the calculator re-checks both conditions (weekend and holiday) against the current candidate date. Advancing one day at a time — rather than jumping directly to Monday — ensures a holiday falling on a Monday is caught in the same pass without special-case branching.
- **A-006**: The safety cap of 14 consecutive advances covers the maximum realistic skip chain (5 consecutive holidays bridging a weekend = 7 days) with a 2× safety margin.
- **A-007**: Holiday date comparison uses calendar-date value equality — two dates are equal if they represent the same year, month, and day. No locale or time-zone logic is involved.
- **A-008**: The returned date is the last valid PP-OPO filing day (inclusive). Downstream display logic shows it as the due date without further adjustment.
- **A-009**: Each day of a multi-day holiday period is a separate entry in `HolidayConf` (established by Feature 003, A-018 seed data).
- **A-010**: Desktop UI for displaying the calculated deadline is out of scope for this feature; that belongs to Feature 011 (Filing Pipeline).
- **A-011**: Fetching, seeding, or caching holiday data is out of scope. The caller provides a fully populated `HolidayConf`.
- **A-012**: No new third-party libraries are introduced. The calculator relies solely on the project's existing domain types and standard calendar date arithmetic.
