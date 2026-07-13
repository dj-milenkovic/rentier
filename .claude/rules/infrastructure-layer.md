---
paths:
  - "src/Rentier.Infrastructure/**"
---

# Rentier.Infrastructure rules

- Implements `Rentier.Application` interfaces. This is the only layer allowed to
  reference EF Core, MailKit, HttpClient, and other concrete external dependencies.
- **Result pattern.** Public methods return `Result<T, Error>` — do not use exceptions
  for expected failure flow-control (network errors, parse failures, missing files).
  Exceptions are reserved for truly unexpected states.
- **No passwords in SQLite.** Credentials always go through the OS credential store
  (`ICredentialStore`), never persisted in the SQLite database.
- **All async.** No `.Result`/`.Wait()` on tasks; every I/O call is `async Task<T>`.
- **NBS exchange rates** are fetched per-date via `NbsRateFetcher` and cached in
  SQLite through `IExchangeRateCacheRepository` — never refetch a cached date.
- **IBKR CSV parsing** (`IbkrCsvParser`) must only process "Dividends" and "Interest"
  activity sections — ignore all other sections.
- Convert any external `DateTime` to `DateOnly` at this boundary before it flows
  into Application/Domain. Convert monetary values to `decimal` immediately on parse.
- EF Core migrations are **forward-only and never destructive** — do not edit past
  migrations; add a new one.
- See `.claude/skills/ef-core` and `.claude/skills/rentier-integration-tests` for
  detailed EF Core and integration-test guidance.
