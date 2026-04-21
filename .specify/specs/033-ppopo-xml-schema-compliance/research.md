# Research: PP-OPO XML Schema Compliance Fix + Export Filename Convention

**Feature**: 033-ppopo-xml-schema-compliance  
**Date**: 2025-07-22  
**Status**: Complete

## Research Tasks

### RT-001: Current XML Output vs ePorezi Schema Requirements

**Decision**: Complete rewrite of `PpOpoXmlSerializer` to match the ePorezi portal schema.

**Rationale**: The current serializer generates XML that does not comply with the ePorezi portal schema in multiple critical ways. A targeted patch approach would be more error-prone than a clean rewrite of the `Serialize` method, given the number of structural differences.

**Current vs Required Element Mapping**:

| Aspect | Current Output | Required Output |
|--------|---------------|-----------------|
| Root element | `<PodaciOPrijavi>` | `<ns1:PodaciPoreskeDeklaracije xmlns:ns1="http://pid.purs.gov.rs">` |
| Namespace prefix | None | `ns1:` on every child element |
| Encoding declaration | `utf-8` (lowercase) | `UTF-8` (uppercase) |
| Filing period indicator | Missing | `<ns1:Rok>1</ns1:Rok>` in PodaciOPrijavi section |
| Taxpayer JMBG | `<JMBG>` | `<ns1:PoreskiIdentifikacioniBroj><ns1:JMBGPodnosiocaPrijave>` |
| Taxpayer name | `<Ime>` with CDATA | `<ns1:ImePrezimeObveznika>` (plain text) |
| Taxpayer address | `<Adresa>` with CDATA | `<ns1:UlicaBrojPoreskogObveznika>` (plain text) |
| Municipality | `<SifraOpstine>` | `<ns1:PrebivalisteOpstina>` |
| Telephone | `<Telefon>` | `<ns1:TelefonKontaktOsobe>` |
| Email | `<Email>` | `<ns1:ElektronskaPosta>` |
| Income rows wrapper | `<DeklarisaniPodaciOVrstamaPrihoda>` | `<ns1:PodaciOVrstamaPrihoda>` with `<ns1:RedniBroj>` |
| OsnovicaZaPorez source | Maps to `GrossTaxPayableRsd` (**BUG**) | Must map to `GrossIncomeRsd` |
| Ukupno section | Missing | Required — aggregated monetary totals + zero contributions |
| Kamata section | Missing | Required — all-zero values |
| PodaciODodatnojKamati | Missing | Required — empty element |
| CDATA wrapping | Name and Address use CDATA | ePorezi schema uses plain text (no CDATA) |

**Alternatives Considered**:
- *Patch individual elements*: Rejected — too many structural changes make patching fragile and harder to review.
- *XSL transform post-processing*: Rejected — adds unnecessary complexity; better to generate correct output directly.

---

### RT-002: OsnovicaZaPorez Data Mapping Bug

**Decision**: Fix the mapping so `OsnovicaZaPorez` reads `filing.GrossIncomeRsd` instead of `filing.GrossTaxPayableRsd`.

**Rationale**: `OsnovicaZaPorez` means "tax base" — the gross income amount before deductions. The current code incorrectly maps it to `GrossTaxPayableRsd` (the computed gross tax), which is a different monetary value. This produces incorrect tax forms.

**Impact Analysis**:
- `OsnovicaZaPorez` → `filing.GrossIncomeRsd` (the tax base = gross income)
- `ObracunatiPorez` → `filing.GrossTaxPayableRsd` (computed tax — this was correct)
- No domain entity change needed; the fix is entirely in the serializer mapping.

**Alternatives Considered**:
- *Rename GrossTaxPayableRsd to OsnovicaZaPorez*: Rejected — the domain field name correctly describes the C# property; the mapping to XML element names is a serialization concern.

---

### RT-003: Ticker Field Design on Filing Entity

**Decision**: Add an optional `string? Ticker` property to the `Filing` entity with a max length of 20 characters.

**Rationale**: 
- The `PayingEntity` field currently stores the IBKR-parsed entity name (e.g., "AAPL"), which for IBKR dividends IS the ticker. However, `PayingEntity` semantically means "name of the paying entity" and could differ from the ticker in manual entries or other data sources.
- A separate `Ticker` field gives clear semantics: it is the asset short-name used for filename generation and asset traceability.
- Max length of 20 characters is sufficient for all major exchange ticker formats (NYSE: 1-5 chars, NASDAQ: 1-5 chars, international: up to ~12 chars).

**Data Flow**:
1. IBKR CSV → `StripIsin("AAPL(US0378331005)")` → `"AAPL"` → `DividendRecord.EntityName`
2. `ProcessReportsCommandHandler` → `Filing.CreateFromIncome(ticker: div.EntityName)`
3. The ticker parameter is optional; when not available, `Ticker` is null.

**Migration Strategy**:
- Add nullable `Ticker` column to `Filings` table via EF Core migration.
- Existing rows get `NULL` — backward compatible, no data loss.
- No backfill needed; existing filings without tickers remain functional.

**Alternatives Considered**:
- *Reuse PayingEntity as ticker*: Rejected — different semantics; PayingEntity could be a full company name from non-IBKR sources.
- *Store ticker in a separate lookup table*: Rejected — over-engineered for a simple nullable string field on an existing entity.

---

### RT-004: Export Filename Convention

**Decision**: Change suggested filename from `PP-OPO_{yyyy-MM}_{JMBG}.xml` to `{yyyy}-{MM}-{Ticker}.xml` when Ticker is available, with fallback to `{yyyy}-{MM}-{PayingEntity}.xml`.

**Rationale**: The new convention makes files immediately identifiable by tax period and asset. JMBG is private information that shouldn't be in filenames, and it's the same for all filings from one taxpayer, providing no disambiguation value.

**Filename Sanitization**:
- Replace characters invalid for Windows/macOS filenames: `\ / : * ? " < > |`
- Replace with underscore `_`
- Trim result; if empty after sanitization, use `"filing"` as fallback.

**Alternatives Considered**:
- *Keep JMBG in filename*: Rejected — privacy concern and no disambiguation value.
- *Use Filing GUID in filename*: Rejected — not human-readable.
- *Use PayingEntity always*: Rejected — Ticker is shorter and preferred when available.

---

### RT-005: XDocument Namespace Handling with System.Xml.Linq

**Decision**: Use `XNamespace` with prefix via `XAttribute` for `ns1:` prefix on all elements.

**Rationale**: `System.Xml.Linq` supports namespace prefixes by declaring the namespace and using it in element construction:

```csharp
XNamespace ns1 = "http://pid.purs.gov.rs";
var root = new XElement(ns1 + "PodaciPoreskeDeklaracije",
    new XAttribute(XNamespace.Xmlns + "ns1", ns1),
    new XElement(ns1 + "PodaciOPrijavi", ...));
```

This produces `<ns1:PodaciPoreskeDeklaracije xmlns:ns1="http://pid.purs.gov.rs">` as required.

**Encoding Handling**: To produce uppercase `UTF-8` in the XML declaration, use `XmlWriterSettings` with explicit `Encoding = new UTF8Encoding(false)` and manually control the declaration output since `XDocument.Save` with `StreamWriter` produces lowercase `utf-8`.

**Alternatives Considered**:
- *String-based XML construction*: Rejected — error-prone, no validation.
- *XmlSerializer with attributes*: Rejected — would require DTO mapping classes; XDocument is already in use and sufficient.

---

### RT-006: CDATA Removal Decision

**Decision**: Remove CDATA wrapping from taxpayer name and address elements.

**Rationale**: The ePorezi schema expects plain text elements, not CDATA sections. The current CDATA wrapping was a defensive measure against special characters, but XDocument's built-in XML escaping handles `&`, `<`, `>` correctly in plain text nodes. The ePorezi portal may reject or misinterpret CDATA sections.

**Alternatives Considered**:
- *Keep CDATA for safety*: Rejected — ePorezi schema doesn't use CDATA; portal may not handle it correctly.

---

### RT-007: Contribution and Kamata Sections

**Decision**: Hard-code Ukupno contribution fields and all Kamata fields to `"0.00"`.

**Rationale**: PP-OPO passive income filings (dividends and interest from foreign sources) do not have social contribution obligations. The Kamata (interest/penalty) section is zero for initial filings. The `PodaciODodatnojKamati` element is always empty. These are constants per the tax filing rules, not derived from Filing data.

**Alternatives Considered**:
- *Make contributions configurable*: Rejected — PP-OPO passive income never has contributions; configurability adds unused complexity.
