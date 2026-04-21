# Data Model: PP-OPO XML Schema Compliance Fix + Export Filename Convention

**Feature**: 033-ppopo-xml-schema-compliance  
**Date**: 2025-07-22

## Entity Changes

### Filing (Aggregate Root) — Modified

**Location**: `src/Rentier.Domain/Entities/Filing.cs`

| Field | Type | Change | Constraint | Notes |
|-------|------|--------|------------|-------|
| `Id` | `Guid` | Unchanged | PK, not generated | — |
| `TaxpayerProfileId` | `Guid` | Unchanged | FK, required | — |
| `TaxPeriod` | `DateOnly` | Unchanged | Required | — |
| `Status` | `FilingStatus` | Unchanged | Required, enum | Init → Filed → Paid |
| `IncomeType` | `IncomeType` | Unchanged | Required, enum | Dividend, Interest |
| `PayingEntity` | `string` | Unchanged | Required, max 500 | Entity name |
| `IncomeDate` | `DateOnly` | Unchanged | Required | — |
| `GrossIncomeRsd` | `decimal` | Unchanged | ≥ 0, precision(18,2) | — |
| `WhtPaidRsd` | `decimal` | Unchanged | ≥ 0, precision(18,2) | — |
| `GrossTaxPayableRsd` | `decimal` | Unchanged | ≥ 0, precision(18,2) | — |
| `TaxPayableRsd` | `decimal` | Unchanged | ≥ 0, precision(18,2) | — |
| `FilingDeadline` | `DateOnly` | Unchanged | Required | — |
| `ReportId` | `Guid?` | Unchanged | FK, nullable | — |
| `PaymentReference` | `string?` | Unchanged | Max 200 | — |
| `ExchangeRateSourceDate` | `DateOnly?` | Unchanged | Nullable | — |
| `ExchangeRateSourceType` | `ExchangeRateSourceType?` | Unchanged | Nullable | — |
| **`Ticker`** | **`string?`** | **NEW** | **Nullable, max 20** | **Asset short-name (e.g., "BABA")** |

#### Ticker Field Specification

- **Type**: `string?` (nullable)
- **Max Length**: 20 characters (enforced in domain via `DomainException`)
- **Allowed Characters**: Any printable character; filename sanitization is a presentation concern, not a domain constraint
- **Default**: `null` for new filings without ticker data and for all existing filings after migration
- **Populated By**: `Filing.CreateFromIncome()` factory method (new optional parameter `ticker`)
- **Used By**: `ExportFilingCommandHandler` for filename generation

#### Factory Method Change

```csharp
public static Filing CreateFromIncome(
    Guid taxpayerProfileId,
    IncomeType incomeType,
    string payingEntity,
    DateOnly incomeDate,
    decimal grossIncomeRsd,
    decimal whtPaidRsd,
    decimal grossTaxPayableRsd,
    decimal taxPayableRsd,
    DateOnly filingDeadline,
    Guid? reportId = null,
    DateOnly? exchangeRateSourceDate = null,
    ExchangeRateSourceType? exchangeRateSourceType = null,
    string? ticker = null)  // ← NEW optional parameter
```

#### Validation Rules

- If `ticker` is provided and length > 20, throw `DomainException("Ticker must not exceed 20 characters.")`
- If `ticker` is whitespace-only, store as `null` (normalize empty/whitespace to null)
- Trim whitespace before storing

---

### TaxpayerProfile — No Entity Changes

The `TaxpayerProfile` entity schema is unchanged. The serializer's interpretation of `Address` and `OpstinaCode` changes (street-only vs full address, 3-digit vs 5-digit municipality), but these are serialization/presentation concerns, not data model changes.

---

### DividendRecord / InterestRecord — No Changes

These `Application.Parsing` records are unchanged. The `EntityName` field already contains the ticker symbol for IBKR data (via `StripIsin`). The ticker propagation happens in `ProcessReportsCommandHandler`, which passes `div.EntityName` as the new `ticker` parameter.

---

## Database Migration

### Migration: `0013_FilingTicker`

**Table**: `Filings`

| Column | Type | Nullable | Default | Max Length |
|--------|------|----------|---------|------------|
| `Ticker` | `TEXT` | Yes | `NULL` | 20 |

**SQL Equivalent**:
```sql
ALTER TABLE Filings ADD COLUMN Ticker TEXT NULL;
```

**EF Core Configuration Addition** (in `FilingConfiguration`):
```csharp
builder.Property(f => f.Ticker)
    .IsRequired(false)
    .HasMaxLength(20);
```

**Backward Compatibility**: All existing rows retain `Ticker = NULL`. No data migration or backfill needed.

---

## XML Output Schema (ePorezi Compliant)

### Target Structure

```xml
<?xml version="1.0" encoding="UTF-8"?>
<ns1:PodaciPoreskeDeklaracije xmlns:ns1="http://pid.purs.gov.rs">
  <ns1:PodaciOPrijavi>
    <ns1:VrstaPrijave>1</ns1:VrstaPrijave>
    <ns1:ObracunskiPeriod>{yyyy-MM}</ns1:ObracunskiPeriod>
    <ns1:Rok>1</ns1:Rok>
  </ns1:PodaciOPrijavi>
  <ns1:PodaciOPoreskomObvezniku>
    <ns1:PoreskiIdentifikacioniBroj>
      <ns1:JMBGPodnosiocaPrijave>{JMBG}</ns1:JMBGPodnosiocaPrijave>
    </ns1:PoreskiIdentifikacioniBroj>
    <ns1:ImePrezimeObveznika>{FullName}</ns1:ImePrezimeObveznika>
    <ns1:UlicaBrojPoreskogObveznika>{Address}</ns1:UlicaBrojPoreskogObveznika>
    <ns1:PrebivalisteOpstina>{OpstinaCode}</ns1:PrebivalisteOpstina>
    <ns1:TelefonKontaktOsobe>{PhoneNumber}</ns1:TelefonKontaktOsobe>
    <ns1:ElektronskaPosta>{Email}</ns1:ElektronskaPosta>
  </ns1:PodaciOPoreskomObvezniku>
  <ns1:PodaciONacinuOstvarivanjaPrihoda>
    <ns1:NacinIsplate>3</ns1:NacinIsplate>
    <ns1:Ostalo>{PaymentNotes}</ns1:Ostalo>
  </ns1:PodaciONacinuOstvarivanjaPrihoda>
  <ns1:PodaciOVrstamaPrihoda>
    <ns1:RedniBroj>1</ns1:RedniBroj>
    <ns1:SifraVrstePrihoda>{SifraVrstePrihoda}</ns1:SifraVrstePrihoda>
    <ns1:DatumOstvarivanjaPrihoda>{yyyy-MM-dd}</ns1:DatumOstvarivanjaPrihoda>
    <ns1:DatumDospelostiObaveze>{yyyy-MM-dd}</ns1:DatumDospelostiObaveze>
    <ns1:BrutoPrihod>{GrossIncomeRsd:F2}</ns1:BrutoPrihod>
    <ns1:NormaraniTroskovi>0.00</ns1:NormaraniTroskovi>
    <ns1:OsnovicaZaPorez>{GrossIncomeRsd:F2}</ns1:OsnovicaZaPorez>
    <ns1:ObracunatiPorez>{GrossTaxPayableRsd:F2}</ns1:ObracunatiPorez>
    <ns1:PorezPlacenDrugojDrzavi>{WhtPaidRsd:F2}</ns1:PorezPlacenDrugojDrzavi>
    <ns1:PorezZaUplatu>{TaxPayableRsd:F2}</ns1:PorezZaUplatu>
    <ns1:OsnovicaZaDoprinose>0.00</ns1:OsnovicaZaDoprinose>
    <ns1:ObracunatiDoprinosi>0.00</ns1:ObracunatiDoprinosi>
    <ns1:DoprinosiPlaceniDrugojDrzavi>0.00</ns1:DoprinosiPlaceniDrugojDrzavi>
    <ns1:DoprinosiZaUplatu>0.00</ns1:DoprinosiZaUplatu>
  </ns1:PodaciOVrstamaPrihoda>
  <ns1:Ukupno>
    <ns1:BrutoPrihod>{GrossIncomeRsd:F2}</ns1:BrutoPrihod>
    <ns1:NormaraniTroskovi>0.00</ns1:NormaraniTroskovi>
    <ns1:OsnovicaZaPorez>{GrossIncomeRsd:F2}</ns1:OsnovicaZaPorez>
    <ns1:ObracunatiPorez>{GrossTaxPayableRsd:F2}</ns1:ObracunatiPorez>
    <ns1:PorezPlacenDrugojDrzavi>{WhtPaidRsd:F2}</ns1:PorezPlacenDrugojDrzavi>
    <ns1:PorezZaUplatu>{TaxPayableRsd:F2}</ns1:PorezZaUplatu>
    <ns1:OsnovicaZaDoprinose>0.00</ns1:OsnovicaZaDoprinose>
    <ns1:ObracunatiDoprinosi>0.00</ns1:ObracunatiDoprinosi>
    <ns1:DoprinosiPlaceniDrugojDrzavi>0.00</ns1:DoprinosiPlaceniDrugojDrzavi>
    <ns1:DoprinosiZaUplatu>0.00</ns1:DoprinosiZaUplatu>
  </ns1:Ukupno>
  <ns1:Kamata>
    <ns1:PorezZaUplatu>0.00</ns1:PorezZaUplatu>
    <ns1:DoprinosiZaUplatu>0.00</ns1:DoprinosiZaUplatu>
  </ns1:Kamata>
  <ns1:PodaciODodatnojKamati />
</ns1:PodaciPoreskeDeklaracije>
```

### Monetary Field Mapping (Corrected)

| XML Element | Source | Notes |
|-------------|--------|-------|
| `BrutoPrihod` | `filing.GrossIncomeRsd` | Unchanged |
| `NormaraniTroskovi` | `0.00` (constant) | New field |
| `OsnovicaZaPorez` | **`filing.GrossIncomeRsd`** | **BUG FIX** — was `GrossTaxPayableRsd` |
| `ObracunatiPorez` | `filing.GrossTaxPayableRsd` | Unchanged |
| `PorezPlacenDrugojDrzavi` | `filing.WhtPaidRsd` | Unchanged |
| `PorezZaUplatu` | `filing.TaxPayableRsd` | Unchanged |
| `OsnovicaZaDoprinose` | `0.00` (constant) | New field |
| `ObracunatiDoprinosi` | `0.00` (constant) | New field |
| `DoprinosiPlaceniDrugojDrzavi` | `0.00` (constant) | New field |
| `DoprinosiZaUplatu` | `0.00` (constant) | New field |
