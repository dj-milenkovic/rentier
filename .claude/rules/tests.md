---
paths:
  - "tests/**"
---

# Rentier test project rules

- Framework: **xUnit + FluentAssertions + NSubstitute**.
- Naming: `MethodName_StateUnderTest_ExpectedBehavior`.
- **`tests/Rentier.UnitTests`** — Domain tests use no mocks (test pure logic).
  Application tests mock repositories/ports with NSubstitute — never mock the code
  under test.
- **`tests/Rentier.Infrastructure.Tests`** — use EF Core InMemory or SQLite
  in-memory; real parsers/serializers against fixture files. Integration tests are
  tagged `[Trait("Category", "Integration")]` (matches CI's `--filter` usage).
- **`tests/Rentier.Scenarios.Tests`** — end-to-end scenarios spanning multiple
  layers; prefer realistic fixture data over mocks where practical.
- **`tests/Rentier.Tests.Common`** — shared builders/fixtures; put reusable test
  data here instead of duplicating across projects.
- One behavior per test; Arrange-Act-Assert; avoid multiple assertions per test
  (write separate tests instead); tests must be runnable in any order/parallel.
- Consult `.claude/skills/rentier-unit-tests`, `rentier-ui-tests`, or
  `rentier-integration-tests` depending on which layer you're testing, and
  `.claude/skills/csharp-xunit` for xUnit-specific patterns (data-driven tests, etc).
