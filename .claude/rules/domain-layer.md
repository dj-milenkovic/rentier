---
paths:
  - "src/Rentier.Domain/**"
---

# Rentier.Domain rules

- **No external dependencies.** Pure C# records, enums, and interfaces only. No EF
  Core, no MailKit, no HttpClient, no framework types of any kind.
- Model entities and value objects as immutable `record`/`record struct` where
  possible. Mutation happens through explicit methods that enforce invariants, not
  public setters.
- **`Filing` status transitions** are enforced here: `init → filed → paid`. Any
  invalid transition must throw `DomainException` — never allow silent no-ops or
  generic exceptions for business-rule violations.
- **`Money`** is the canonical value object: `(decimal Amount, string Currency)`.
  Reuse it; do not introduce parallel money representations.
- **`MailboxCursor`** is a discriminated union via `abstract record` with sealed
  derived records. Follow the same pattern for any new discriminated-union-style
  domain concept.
- **`decimal` only** for money, tax amounts, exchange rates, percentages. **`DateOnly`**
  only for dates — never `DateTime` in Domain.
- Serbian public holidays live in the `HolidayConf` value object — do not hardcode
  holiday dates elsewhere.
- No repository interfaces here — those belong in `Rentier.Application`. Domain
  exposes only entities, value objects, domain services, and domain exceptions.
