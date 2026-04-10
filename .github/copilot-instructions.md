# GitHub Copilot Instructions — Rentier

## Identity
You are assisting in building **Rentier**, a C# + Avalonia desktop application
for automated Serbian PP-OPO passive income tax filing. The architecture is
**Clean Architecture** with strict layer separation. Financial correctness is
the highest priority.

## Clean Architecture Rules (Memorize These)
1. `Rentier.Domain` — no external dependencies. Pure C# records and interfaces.
2. `Rentier.Application` — references Domain only. Defines `IRepository` interfaces.
   Contains CQRS commands/queries.
3. `Rentier.Infrastructure` — implements Application interfaces. EF Core, MailKit, HttpClient.
4. `Rentier.Desktop` — Avalonia UI. Calls Application use cases only. Never touches
   Infrastructure directly.

If you're about to put EF Core in Domain, stop. If you're about to call a repository
from a ViewModel, stop. Route through Application instead.

## Absolute Rules
1. **`decimal` only** for all monetary values, tax amounts, exchange rates, percentages.
2. **`DateOnly`** for all dates. Convert external `DateTime` at infrastructure boundary.
3. **No passwords in SQLite.** Always OS credential store.
4. **All async.** Every I/O method is `async Task<T>`. No `.Result`, no `.Wait()`.
5. **No UI thread blocking.** Use `ReactiveCommand.CreateFromTask`.
6. **Result pattern.** Infrastructure returns `Result<T, Error>`. No exception-as-flow-control.

## CQRS Pattern
When implementing features, create:
- A `*Command` record in `Rentier.Application/Commands/`
- A `*CommandHandler` in `Rentier.Application/Handlers/`
- Or a `*Query` + `*QueryHandler` for reads

## Domain Model Enforcement
- `Filing` status transitions are enforced in Domain: `init → filed → paid`.
  Invalid transitions throw `DomainException`.
- `Money` is a value object: `(decimal Amount, string Currency)`.
- `MailboxCursor` is a discriminated union via `abstract record`.

## When Generating Avalonia XAML
- `ReactiveUserControl<TViewModel>` for all views.
- Bind to ViewModel observables only — no event handlers in code-behind.
- `IsLoading`, `ErrorMessage`, `HasItems` are standard ViewModel properties for state.
- Use `DataGrid` for lists. Use `ContentDialog` for dialogs (async only).

## When Generating Tests
- xUnit + FluentAssertions + NSubstitute.
- Domain tests: no mocks — test pure logic.
- Application tests: mock repositories with NSubstitute.
- Infrastructure tests: use EF Core InMemory or SQLite in-memory.
- Naming: `MethodName_StateUnderTest_ExpectedBehavior`.

## Domain Knowledge
- PP-OPO deadline = payment date + 30 days, shifted to next business day if weekend/holiday.
- Serbian public holidays are configured in `HolidayConf` value object.
- Withholding tax credit cannot exceed computed Serbian tax (15%).
- NBS exchange rates are fetched per-date, cached in SQLite.
- IBKR CSV: only process "Dividends" and "Interest" activity sections.
