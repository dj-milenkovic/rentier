---
description: "Task list for feature 033 — PP-OPO XML Schema Compliance Fix + Export Filename Convention"
---

# Tasks: PP-OPO XML Schema Compliance Fix + Export Filename Convention

**Feature**: 033-ppopo-xml-schema-compliance  
**Branch**: `feature/032-033-034-column-xml-manual`  
**Input**: `.specify/specs/033-ppopo-xml-schema-compliance/` (spec.md, plan.md, research.md, data-model.md, contracts/xml-export-contract.md)

**Tests**: Included — Domain and Application coverage gates required per constitution CA-006. Infrastructure serializer tests are a full rewrite.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1 through US4)
- Exact file paths included in all descriptions

---

## Phase 1: Setup

**Purpose**: Confirm working context and establish a clean baseline before changes begin.

- [ ] T001 Verify active branch is `feature/032-033-034-column-xml-manual` and run `dotnet test Rentier.slnx` to confirm all existing tests pass (zero failures are the baseline to preserve throughout this feature)

---

## Phase 2: Foundational — Filing.Ticker Domain Entity + Persistence

**Purpose**: Introduce the `Ticker` field at the domain and persistence layers. This MUST be complete before US4 (ticker propagation) and US3 (filename convention) can be implemented.

**⚠️ CRITICAL**: US3 and US4 propagation work cannot begin until this phase is complete.

- [ ] T002 Add `string? Ticker` nullable property to `Filing` aggregate in `src/Rentier.Domain/Entities/Filing.cs`; add optional `string? ticker = null` parameter to the `CreateFromIncome()` factory method; add validation: throw `DomainException("Ticker must not exceed 20 characters.")` if length > 20; normalize whitespace-only values to `null` via `string.IsNullOrWhiteSpace` check; trim whitespace before storing
- [ ] T003 [P] Add `Ticker` column EF Core configuration to `FilingConfiguration` in `src/Rentier.Infrastructure/Persistence/Configurations/FilingConfiguration.cs`: `builder.Property(f => f.Ticker).IsRequired(false).HasMaxLength(20)` (depends on T002)
- [ ] T004 Generate EF Core migration `0013_FilingTicker` by running `dotnet ef migrations add 0013_FilingTicker --project src/Rentier.Infrastructure --startup-project src/Rentier.Desktop --output-dir Persistence/Migrations` from repo root; verify the generated migration adds a nullable `Ticker TEXT` column to the `Filings` table with no `Up()` data migration needed (depends on T002, T003)

**Checkpoint**: `Filing.Ticker` property exists, is persisted, and existing rows load with `Ticker = null`. Foundation for US3 and US4 is ready.

---

## Phase 3: User Story 1 — Upload Compliant XML to ePorezi Portal (Priority: P1) 🎯 MVP

**Goal**: Rewrite `PpOpoXmlSerializer` so exported XML fully conforms to the ePorezi portal schema — correct root element with `xmlns:ns1="http://pid.purs.gov.rs"` namespace prefix on every element, all corrected element names, required sections (`Ukupno`, `Kamata`, `PodaciODodatnojKamati`), nested JMBG structure, plain-text (no CDATA) taxpayer fields, and uppercase `UTF-8` encoding declaration.

**Independent Test**: Export a filing and verify the XML document structure with XDocument.Parse; assert root element is `ns1:PodaciPoreskeDeklaracije`, every child uses the `ns1:` prefix, all 7 required sections are present, encoding declaration is `UTF-8` uppercase, taxpayer JMBG is nested inside `PoreskiIdentifikacioniBroj > JMBGPodnosiocaPrijave`.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST — they MUST FAIL against the old serializer before T007 is implemented**

- [ ] T005 [P] [US1] Rewrite the element-level assertions in `tests/Rentier.Infrastructure.Tests/Serialization/PpOpoXmlSerializerTests.cs` to verify: (a) root element name and `xmlns:ns1` attribute, (b) every top-level element uses `ns1:` prefix, (c) `PodaciOPrijavi` contains `VrstaPrijave`, `ObracunskiPeriod`, and `Rok` with value `"1"`, (d) `PodaciOPoreskomObvezniku` contains `PoreskiIdentifikacioniBroj/JMBGPodnosiocaPrijave`, `ImePrezimeObveznika`, `UlicaBrojPoreskogObveznika`, `PrebivalisteOpstina`, `TelefonKontaktOsobe`, `ElektronskaPosta` — all plain text (no CDATA), (e) `PodaciOVrstamaPrihoda` contains `RedniBroj` with value `"1"`, (f) `Ukupno` section exists with all monetary fields, (g) `Kamata` section exists with `PorezZaUplatu` and `DoprinosiZaUplatu` both `"0.00"`, (h) `PodaciODodatnojKamati` exists as empty/self-closing element
- [ ] T006 [P] [US1] Add an encoding-declaration test to `tests/Rentier.Infrastructure.Tests/Serialization/PpOpoXmlSerializerTests.cs` that parses the raw `byte[]` output as a UTF-8 string and asserts the XML declaration contains `encoding="UTF-8"` (uppercase)

### Implementation for User Story 1

- [ ] T007 [US1] Rewrite the `Serialize()` method in `src/Rentier.Infrastructure/Serialization/PpOpoXmlSerializer.cs` to produce ePorezi-compliant XML: (a) declare `XNamespace ns1 = "http://pid.purs.gov.rs"` and use `new XAttribute(XNamespace.Xmlns + "ns1", ns1)` on the root element, (b) construct root as `new XElement(ns1 + "PodaciPoreskeDeklaracije", ...)` with all child elements using `ns1 + "ElementName"`, (c) emit `PodaciOPrijavi` with `VrstaPrijave="1"`, `ObracunskiPeriod` from `filing.IncomeDate.ToString("yyyy-MM")`, `Rok="1"`, (d) emit `PodaciOPoreskomObvezniku` with nested `PoreskiIdentifikacioniBroj > JMBGPodnosiocaPrijave`, `ImePrezimeObveznika`, `UlicaBrojPoreskogObveznika`, `PrebivalisteOpstina`, `TelefonKontaktOsobe` (empty string if null), `ElektronskaPosta` (empty string if null) — all plain text without CDATA, (e) emit `PodaciONacinuOstvarivanjaPrihoda` with `NacinIsplate="3"` and `Ostalo` from `paymentNotes`, (f) emit `PodaciOVrstamaPrihoda` with `RedniBroj="1"`, `SifraVrstePrihoda` (`"111402000"` for Dividend, `"111401000"` for Interest), date fields, all monetary fields formatted `"F2"` via `CultureInfo.InvariantCulture`, and four zero contribution fields, (g) emit `Ukupno` section mirroring the monetary fields from the income row, (h) emit `Kamata` with `PorezZaUplatu="0.00"` and `DoprinosiZaUplatu="0.00"`, (i) emit self-closing `PodaciODodatnojKamati` element, (j) use `XmlWriterSettings` with `Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)` and `OmitXmlDeclaration = false` to ensure uppercase `UTF-8` in the declaration, writing to `MemoryStream` then returning `stream.ToArray()` (depends on T005, T006 asserting failure)

**Checkpoint**: All T005 and T006 tests pass. Exported XML file is accepted by ePorezi portal schema validation (namespace, element names, required sections, encoding all correct).

---

## Phase 4: User Story 2 — Correct Tax Base (OsnovicaZaPorez) Value (Priority: P1)

**Goal**: Fix the data mapping bug so `OsnovicaZaPorez` emits `filing.GrossIncomeRsd` (the gross income tax base) instead of `filing.GrossTaxPayableRsd` (the computed tax). This fix is applied within the serializer rewrite from T007.

**Independent Test**: Create a `Filing` where `GrossIncomeRsd = 100_000.00m` and `GrossTaxPayableRsd = 15_000.00m`, serialize it, parse the XML, and assert `OsnovicaZaPorez` equals `"100000.00"` (not `"15000.00"`); also assert `ObracunatiPorez` equals `"15000.00"`.

### Tests for User Story 2 ⚠️

> **NOTE: Add this test to the existing test class; it MUST FAIL before T009 is applied**

- [ ] T008 [P] [US2] Add a dedicated `OsnovicaZaPorezMapsToGrossIncomeNotGrossTax` test to `tests/Rentier.Infrastructure.Tests/Serialization/PpOpoXmlSerializerTests.cs`: construct a Filing with distinct `GrossIncomeRsd` and `GrossTaxPayableRsd` values, serialize, parse XDocument, assert `//ns1:OsnovicaZaPorez` equals `GrossIncomeRsd.ToString("F2", CultureInfo.InvariantCulture)` and `//ns1:ObracunatiPorez` equals `GrossTaxPayableRsd.ToString("F2", CultureInfo.InvariantCulture)` — also add the same assertions for the `Ukupno` section

### Implementation for User Story 2

- [ ] T009 [US2] Confirm that T007's serializer rewrite uses `filing.GrossIncomeRsd` for both the `PodaciOVrstamaPrihoda/OsnovicaZaPorez` field and the `Ukupno/OsnovicaZaPorez` field in `src/Rentier.Infrastructure/Serialization/PpOpoXmlSerializer.cs`; if T007 was completed first, this task is a focused code-review and test-run step — verify T008 now passes and add the fix if T007 inadvertently kept the old mapping (depends on T007, T008)

**Checkpoint**: T008 passes. `OsnovicaZaPorez` in exported XML always equals the gross income amount for 100% of test cases.

---

## Phase 5: User Story 4 — Ticker Field Propagation + Domain Tests (Priority: P2)

**Goal**: Pass the ticker symbol from brokerage import data through `ProcessReportsCommandHandler` into `Filing.CreateFromIncome()` so the Ticker field is populated for IBKR-sourced filings. Add domain tests to fully cover the Ticker field behavior. (The domain entity and persistence for Ticker were established in Phase 2.)

**Independent Test**: Create a Filing with `ticker = "BABA"`, save to the in-memory test database, reload the Filing, and assert `Ticker == "BABA"`. Also: create a Filing with no ticker, reload, assert `Ticker == null`.

### Tests for User Story 4 ⚠️

> **NOTE: Write domain tests FIRST against the Filing changes from T002**

- [ ] T010 [P] [US4] Add `FilingTickerTests` class to `tests/Rentier.UnitTests/` (create file `tests/Rentier.UnitTests/Domain/FilingTickerTests.cs`): cover (a) `CreateFromIncome` with valid ticker stores trimmed value, (b) `CreateFromIncome` with `ticker = null` stores `null`, (c) `CreateFromIncome` with whitespace-only ticker stores `null`, (d) `CreateFromIncome` with ticker longer than 20 characters throws `DomainException`, (e) `CreateFromIncome` with exactly 20-character ticker succeeds, (f) ticker with leading/trailing whitespace is trimmed and stored
- [ ] T011 [P] [US4] Add a persistence round-trip integration test for `Filing.Ticker` in `tests/Rentier.Infrastructure.Tests/` (create file `tests/Rentier.Infrastructure.Tests/Persistence/FilingTickerPersistenceTests.cs`): save a Filing with `Ticker = "AAPL"` to the SQLite test database, reload it via `FilingRepository`, assert `Ticker == "AAPL"`; also save a Filing with `Ticker = null`, reload, assert `Ticker == null`

### Implementation for User Story 4

- [ ] T012 [US4] Update `ProcessReportsCommandHandler` in `src/Rentier.Application/Handlers/ProcessReportsCommandHandler.cs` to pass `ticker: div.EntityName` (for dividend records) and `ticker: interest.EntityName` (for interest records) as the new optional `ticker` parameter in each `Filing.CreateFromIncome(...)` call; confirm `EntityName` from IBKR CSV records already contains the stripped ticker symbol (e.g., `"AAPL"` not `"AAPL(US0378331005)"`) per existing `StripIsin` logic (depends on T002, T010 asserting failure, T011 asserting failure)

**Checkpoint**: T010 and T011 pass. Ticker is populated for IBKR-sourced filings and survives persistence round-trips. Existing filings load with `Ticker = null`.

---

## Phase 6: User Story 3 — Human-Friendly Export Filename Convention (Priority: P2)

**Goal**: Change the suggested export filename from `PP-OPO_{yyyy-MM}_{JMBG}.xml` to `{yyyy}-{MM}-{Ticker}.xml` when `Ticker` is available, with a fallback to `{yyyy}-{MM}-{SanitizedPayingEntity}.xml`, and a final fallback of `{yyyy}-{MM}-filing.xml`. Apply filename sanitization replacing `\ / : * ? " < > |` with `_`.

**Independent Test**: Build an `ExportFilingCommand` with a Filing whose `IncomeDate = 2025-03-15` and `Ticker = "BABA"`, execute `ExportFilingCommandHandler`, and assert `SuggestedFileName == "2025-03-BABA.xml"`. Also test null Ticker with a PayingEntity of `"ACME Corp"` produces `"2025-03-ACME_Corp.xml"`. Also test both null/empty produces `"2025-03-filing.xml"`.

### Tests for User Story 3 ⚠️

> **NOTE: Update existing handler tests FIRST — they MUST FAIL against the old filename logic**

- [ ] T013 [P] [US3] Update `tests/Rentier.UnitTests/ExportFilingCommandHandlerTests.cs` to cover the new filename convention: (a) Filing with `Ticker = "BABA"` and `IncomeDate = 2025-03-15` → `SuggestedFileName == "2025-03-BABA.xml"`, (b) Filing with `Ticker = null` and `PayingEntity = "ACME Corp"` → `"2025-03-ACME_Corp.xml"`, (c) Filing with `Ticker = null` and `PayingEntity = null`/empty → `"2025-03-filing.xml"`, (d) Filing with `Ticker = "BAD:NAME*"` → unsafe chars replaced with `_` so `"2025-03-BAD_NAME_.xml"`, (e) Filing with `Ticker = "  "` (whitespace-only) normalized to null, fallback to PayingEntity

### Implementation for User Story 3

- [ ] T014 [US3] Update filename generation in `src/Rentier.Application/Handlers/ExportFilingCommandHandler.cs`: replace the existing filename construction with a helper that (a) determines the identifier segment: use `filing.Ticker` if non-null/non-empty, else use `filing.PayingEntity`, else use `"filing"`, (b) sanitizes the identifier by replacing `\ / : * ? " < > |` with `_` using a `Regex.Replace` or `string.Replace` loop, trims underscores, and falls back to `"filing"` if the sanitized result is empty, (c) constructs the final name as `$"{filing.IncomeDate:yyyy-MM}-{sanitizedIdentifier}.xml"` and sets it as `ExportFilingResult.SuggestedFileName` (depends on T002 for `Filing.Ticker`, T013 asserting failure)

**Checkpoint**: T013 passes. Exported filenames follow `{yyyy}-{MM}-{Ticker}.xml` for all filings with a Ticker value. Files are identifiable by year, month, and asset name without opening them.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Update the snapshot test artefact, validate end-to-end, and ensure all quality gates are satisfied before merge.

- [ ] T015 [P] Run the snapshot test in `tests/Rentier.Infrastructure.Tests/Serialization/PpOpoXmlSerializerSnapshotTests.cs` against the rewritten serializer from T007; the test will fail with a diff — accept the new snapshot by updating `PpOpoXmlSerializerSnapshotTests.Serialize_RepresentativeDividendFiling_MatchesSnapshot.verified.xml` with the new ePorezi-compliant XML output; manually verify the accepted snapshot matches the target XML structure from `data-model.md` (namespace, element names, `Ukupno`, `Kamata`, `PodaciODodatnojKamati`, uppercase UTF-8 encoding, `OsnovicaZaPorez = GrossIncomeRsd`) (depends on T007)
- [ ] T016 Run `dotnet test Rentier.slnx` and confirm zero failures; pay particular attention to constitution coverage gates: Domain unit tests cover Ticker (T010), Application tests cover filename logic (T013) and handler propagation, Infrastructure tests cover the full serializer rewrite (T005, T006, T008, T011, T015 snapshot)
- [ ] T017 Follow `quickstart.md` steps end-to-end: build the solution, run EF migrations manually, export a PP-OPO filing from the running app, and visually confirm the generated XML file has the correct filename format and opens in a text editor showing the ePorezi-compliant structure with `xmlns:ns1="http://pid.purs.gov.rs"`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — **BLOCKS** US3 and US4 propagation
- **US1 (Phase 3)**: Can start after Phase 1; does NOT depend on Phase 2 (serializer does not use Ticker)
- **US2 (Phase 4)**: Depends on Phase 3 (same serializer file, fix applied in rewrite); runs immediately after T007
- **US4 (Phase 5)**: Depends on Phase 2 (Ticker entity + migration); can run in parallel with Phase 3/4
- **US3 (Phase 6)**: Depends on Phase 2 (Ticker field) and Phase 5 (T012 confirms propagation) for full behaviour; filename handler change itself only needs T002
- **Polish (Phase 7)**: Depends on all prior phases completing

### User Story Dependencies

```text
Phase 1: Setup (T001)
    ↓
Phase 2: Filing.Ticker Domain + Persistence (T002 → T003+T004)
    ↓                                   ↓
Phase 3: US1 XML Compliance         Phase 5: US4 Ticker Propagation
(T005+T006 tests → T007 impl)       (T010+T011 tests → T012 impl)
    ↓                                   ↓
Phase 4: US2 OsnovicaZaPorez        Phase 6: US3 Filename Convention
(T008 test → T009 confirm)          (T013 test → T014 impl)
    ↓                                   ↓
Phase 7: Polish (T015 snapshot → T016 full test run → T017 quickstart)
```

### Within Each User Story

- Tests (`[P]` test tasks) MUST be written and confirmed **failing** before implementation tasks run
- Domain entity (T002) before EF config (T003) before migration (T004)
- Serializer rewrite (T007) before OsnovicaZaPorez confirmation (T009)
- Ticker entity (T002) before propagation handler (T012)
- Ticker entity (T002) before filename handler (T014)

### Parallel Opportunities

- **Phase 2**: T003 and T004 can run in parallel once T002 is complete
- **Phase 3 tests**: T005 and T006 can be written in parallel (same file, different test methods)
- **Phase 3 vs Phase 5**: After Phase 2 completes, Phase 3 (serializer work) and Phase 5 (ticker domain tests) can proceed in parallel — they touch entirely different files
- **Phase 5 tests**: T010 and T011 can be written in parallel (different test files)
- **Phase 7**: T015 can run in parallel with T016 initial run; T017 follows both

---

## Parallel Example: After Phase 2 Completes

```text
# Stream A — XML compliance (Phase 3 + 4):
T005: Rewrite PpOpoXmlSerializerTests (assert new schema → these fail)
T006: Add encoding assertion test (→ fails)
  ↓ parallel complete
T007: Rewrite PpOpoXmlSerializer.Serialize() (T005+T006 now pass)
  ↓
T008: Add OsnovicaZaPorez mapping test (→ fails if bug not yet fixed)
T009: Confirm fix in rewritten serializer (T008 now passes)

# Stream B — Ticker propagation (Phase 5), in parallel with Stream A:
T010: Add FilingTickerTests domain unit tests (→ fail against old Filing)
T011: Add FilingTickerPersistenceTests integration tests (→ fail without migration)
  ↓ parallel complete (T002 already done in Phase 2)
T012: Update ProcessReportsCommandHandler to pass ticker (T010+T011 now pass)
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 Only — Fix the blocking portal upload issue)

1. Complete Phase 1: Setup (baseline)
2. Complete Phase 2: Foundational (Ticker entity — needed for later stories; does not block serializer work)
3. Complete Phase 3: US1 XML Schema Compliance
4. Complete Phase 4: US2 OsnovicaZaPorez Fix
5. **STOP and VALIDATE**: Run `dotnet test`, export a filing, confirm portal-ready XML structure
6. The two P1 stories are complete — filing upload is unblocked

### Incremental Delivery

1. Phase 1 + Phase 2 → Domain and persistence foundation ready
2. Phase 3 + Phase 4 → Serializer fixed → Portal uploads work (P1 stories done ✅)
3. Phase 5 → Ticker populated from IBKR imports
4. Phase 6 → Filename convention applied → Files identifiable without opening
5. Phase 7 → Snapshot updated, all tests green, quickstart validated

### Parallel Team Strategy

With two developers after Phase 2 completes:

- **Developer A**: Phase 3 (XML compliance) → Phase 4 (OsnovicaZaPorez) → Phase 7 (snapshot)
- **Developer B**: Phase 5 (Ticker propagation) → Phase 6 (filename convention)
- Both stories complete and integrate independently; merge together at Phase 7

---

## Task Summary

| Phase | Tasks | User Story | Priority |
|-------|-------|------------|----------|
| Phase 1: Setup | T001 | — | — |
| Phase 2: Foundational | T002–T004 | US4 foundation | prerequisite |
| Phase 3: XML Compliance | T005–T007 | US1 | P1 |
| Phase 4: OsnovicaZaPorez | T008–T009 | US2 | P1 |
| Phase 5: Ticker Propagation | T010–T012 | US4 | P2 |
| Phase 6: Filename Convention | T013–T014 | US3 | P2 |
| Phase 7: Polish | T015–T017 | — | — |

**Total tasks**: 17  
**P1 tasks** (US1 + US2): 5 implementation/test tasks (T005–T009)  
**P2 tasks** (US3 + US4): 7 implementation/test tasks (T010–T014 + T015 snapshot)  
**Parallel opportunities**: 8 tasks marked `[P]`  
**Suggested MVP scope**: Complete Phases 1–4 (T001–T009) to unblock ePorezi portal uploads

---

## Notes

- `[P]` tasks = different files, no dependencies on incomplete tasks in the same phase
- `[Story]` label maps each task to a specific user story for traceability
- Each user story is independently completable and testable at its checkpoint
- Commit after each task or logical group; use task IDs in commit messages
- Verify tests **fail** before implementing — do not skip this step
- The snapshot test (T015) must be manually reviewed after acceptance to confirm it matches the ePorezi target schema in `data-model.md`, not just auto-accepted blindly
- The serializer rewrite (T007) is the highest-risk task — it changes all element names, adds namespace prefix, and introduces three new sections; budget review time accordingly
- Do NOT expose JMBG in filenames — the new convention intentionally removes it (privacy improvement per research.md RT-004)
