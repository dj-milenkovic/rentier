---
paths:
  - "src/Rentier.Application/**"
---

# Rentier.Application rules

- **References Domain only.** Never reference EF Core, MailKit, HttpClient, or any
  concrete Infrastructure type. Define `IRepository`-style interfaces here instead;
  Infrastructure implements them.
- **CQRS pattern** for every feature:
  - A `*Command` record in `Commands/`
  - A `*CommandHandler` in `Handlers/`
  - Or a `*Query` + `*QueryHandler` for reads
- **All async.** Every handler method is `async Task<T>`. No `.Result`, no `.Wait()`.
- **Result pattern at the boundary.** Handlers should surface failures from
  Infrastructure via `Result<T, Error>` rather than letting infrastructure exceptions
  leak as control flow — catch, log (if applicable), and translate.
- DTOs crossing into the Desktop layer belong here (e.g. `FilingDto`, `ReportDto`,
  `SyncResultDto`) — Desktop must never bind directly to Domain entities or EF models.
- **Logging is metadata only.** `ILogger<T>` templates carry ids (Guids), error codes, counts,
  durations, dates, currency codes and public FX rates. Never amounts, tickers, ISINs, account
  ids, report/file names, email subjects, hosts, usernames, or any `Error.Message`,
  `ParseError.Message`, `ToLogMessage()` or exception message placed in a template — attach the
  exception object instead (the log renders it safely). Logs are shared in bug reports.
  New `ILogger<T>` constructor parameters are optional (`= null`) so tests can build the class directly.
- `decimal` for all monetary values, `DateOnly` for all dates — convert any external
  `DateTime` at the Infrastructure boundary, never inside Application.
