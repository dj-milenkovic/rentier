# Data Model: PP-OPO XML Export

**Feature**: 013-pp-opo-xml-export  
**Plan**: [plan.md](plan.md)

---

## Summary

This feature is **read-only with respect to the database**. No Domain entities are modified,
no new EF Core migration is required. The handler reads four existing entities to populate the
PP-OPO XML document.

---

## Entities Used (read-only)

### `Filing` — aggregate root

| Property | Type | Used in XML |
|---|---|---|
| `Id` | `Guid` | Command parameter; `SuggestedFileName` |
| `TaxpayerProfileId` | `Guid` | (used for profile load if needed, not in XML) |
| `IncomeType` | `IncomeType` (enum) | `SifraVrstePrihoda` |
| `IncomeDate` | `DateOnly` | `ObracunskiPeriod`, `DatumOstvarivanjaPrihoda` |
| `FilingDeadline` | `DateOnly` | `DatumDospelostiObaveze` |
| `GrossIncomeRsd` | `decimal` | `BrutoPrihod` |
| `WhtPaidRsd` | `decimal` | `PorezPlacenDrugojDrzavi` |
| `GrossTaxPayableRsd` | `decimal` | `OsnovicaZaPorez`, `ObracunatiPorez` |
| `TaxPayableRsd` | `decimal` | `PorezZaUplatu` |
| `ReportId` | `Guid?` | Used to resolve `Report`; nullable — see null path below |

**Not used in XML**: `Status`, `PayingEntity`, `TaxPeriod`, `PaymentReference`.

### `TaxpayerProfile` — singleton entity

| Property | Type | Used in XML |
|---|---|---|
| `Jmbg` | `string` | `JMBG`, `SuggestedFileName` |
| `FullName` | `string` | `Ime` (CDATA) |
| `Address` | `string` | `Adresa` (CDATA) |
| `OpstinaCode` | `string` | `SifraOpstine` |
| `PhoneNumber` | `string?` | `Telefon` (empty string if null) |
| `Email` | `string?` | `Email` (empty string if null) |

> ⚠️ **Field name correction**: The spec and architecture rules reference `profile.Phone`, but
> the actual domain entity property is `TaxpayerProfile.PhoneNumber` (nullable `string?`).
> The serializer must use `profile.PhoneNumber ?? string.Empty` for the `<Telefon>` element.

**Failure condition**: If `ITaxpayerProfileRepository.GetAsync()` returns `null`, the handler
returns `Error.Domain("Taxpayer profile is required before exporting.")` and no XML is produced.

### `Report` — intermediate entity (nullable path)

| Property | Type | Used by handler |
|---|---|---|
| `Id` | `Guid` | (key for lookup) |
| `ImporterId` | `Guid` | Used to resolve `Importer` |

**Not used in XML**: `ReportName`, `Status`, `AttachmentContent`, `MailboxMessageId`, `ImportDate`.

**Null path**: If `Filing.ReportId` is `null`, or if `IReportRepository.GetByIdAsync` returns
`null`, skip the Report and Importer loads; set `paymentNotes = string.Empty`.

### `Importer` — leaf entity (nullable path)

| Property | Type | Used in XML |
|---|---|---|
| `PaymentNotes` | `string` | `Ostalo` |

**Not used in XML**: `DisplayName`, `ReportType`, `TaxpayerProfileId`, `MailboxId`,
`FromFilter`, `SubjectFilter`, `AttachmentRegex`.

**Null path**: If `IImporterRepository.GetByIdAsync` returns `null`, set
`paymentNotes = string.Empty`.

> `Importer.PaymentNotes` is declared as non-nullable `string` (defaults to `string.Empty`)
> per static analysis of `Importer.cs`.

---

## New Application Types

### `ExportFilingResult` record

```csharp
// Rentier.Application/Commands/ExportFilingResult.cs
namespace Rentier.Application.Commands;

/// <summary>
/// Holds the serialized PP-OPO XML bytes and the pre-computed suggested filename
/// for the native save dialog.
/// </summary>
public sealed record ExportFilingResult(byte[] Bytes, string SuggestedFileName);
```

**Rationale**: Returning the suggested filename from the handler (where JMBG and IncomeDate
are available) keeps filename derivation in the Application layer and the Desktop delegate
simple.

**Suggested filename format**: `PP-OPO_{IncomeDate:yyyy-MM}_{profile.Jmbg}.xml`  
Example: `PP-OPO_2025-03_1234567890123.xml`

### `ExportFilingCommand` record

```csharp
// Rentier.Application/Commands/ExportFilingCommand.cs
namespace Rentier.Application.Commands;

public sealed record ExportFilingCommand(Guid FilingId);
```

---

## XML Field Mapping Table

The PP-OPO XML document structure and field sources are defined below. The root element is
`<PodaciOPrijavi>` containing three child section elements.

### Root element — `<PodaciOPrijavi>`

| XML Element | Source | Format | Notes |
|---|---|---|---|
| `VrstaPrijave` | Fixed: `"1"` | string | Always `1` (initial filing) |
| `ObracunskiPeriod` | `filing.IncomeDate` | `yyyy-MM` | Accounting period |
| `DatumOstvarivanjaPrihoda` | `filing.IncomeDate` | `yyyy-MM-dd` | Income date |
| `DatumDospelostiObaveze` | `filing.FilingDeadline` | `yyyy-MM-dd` | Filing deadline |

### Section 1 — `<PodaciOPoreskomObvezniku>` (Taxpayer data)

| XML Element | Source | Format | Notes |
|---|---|---|---|
| `JMBG` | `profile.Jmbg` | string (13 digits) | |
| `Ime` | `profile.FullName` | CDATA | XCData wrapping required |
| `Adresa` | `profile.Address` | CDATA | XCData wrapping required |
| `SifraOpstine` | `profile.OpstinaCode` | string | |
| `Telefon` | `profile.PhoneNumber ?? ""` | string | Property is `PhoneNumber` not `Phone` |
| `Email` | `profile.Email ?? ""` | string | |

### Section 2 — `<PodaciONacinuOstvarivanjaPrihoda>` (Payment method data)

| XML Element | Source | Format | Notes |
|---|---|---|---|
| `NacinIsplate` | Fixed: `"3"` | string | Always `3` (out of scope to vary) |
| `Ostalo` | `paymentNotes` | string | Empty string if no importer; element always present |

### Section 3 — `<DeklarisaniPodaciOVrstamaPrihoda>` (Income type data)

| XML Element | Source | Format | Notes |
|---|---|---|---|
| `SifraVrstePrihoda` | `filing.IncomeType` | integer string | `Interest → 111401000`; `Dividend → 111402000` |
| `BrutoPrihod` | `filing.GrossIncomeRsd` | `"F2"` InvariantCulture | e.g., `"12345.50"` |
| `OsnovicaZaPorez` | `filing.GrossTaxPayableRsd` | `"F2"` InvariantCulture | Tax base |
| `ObracunatiPorez` | `filing.GrossTaxPayableRsd` | `"F2"` InvariantCulture | Calculated tax = tax base |
| `PorezPlacenDrugojDrzavi` | `filing.WhtPaidRsd` | `"F2"` InvariantCulture | WHT paid at source |
| `PorezZaUplatu` | `filing.TaxPayableRsd` | `"F2"` InvariantCulture | Tax due for payment |

---

## `IncomeType` Enum Mapping

```csharp
// Domain/Enums/IncomeType.cs  (unchanged — read-only)
public enum IncomeType { Dividend, Interest }
```

| Enum Value | `SifraVrstePrihoda` XML value |
|---|---|
| `IncomeType.Interest` | `111401000` |
| `IncomeType.Dividend` | `111402000` |

---

## XML Document Structure (annotated)

```xml
<?xml version="1.0" encoding="utf-8"?>
<PodaciOPrijavi>
  <VrstaPrijave>1</VrstaPrijave>
  <ObracunskiPeriod>2025-03</ObracunskiPeriod>
  <DatumOstvarivanjaPrihoda>2025-03-15</DatumOstvarivanjaPrihoda>
  <DatumDospelostiObaveze>2025-04-14</DatumDospelostiObaveze>

  <PodaciOPoreskomObvezniku>
    <JMBG>1234567890123</JMBG>
    <Ime><![CDATA[Petar Petrović]]></Ime>
    <Adresa><![CDATA[Ulica Mira 42, Beograd]]></Adresa>
    <SifraOpstine>70157</SifraOpstine>
    <Telefon>+38111234567</Telefon>
    <Email>petar@example.com</Email>
  </PodaciOPoreskomObvezniku>

  <PodaciONacinuOstvarivanjaPrihoda>
    <NacinIsplate>3</NacinIsplate>
    <Ostalo>Wire transfer from IBKR</Ostalo>
  </PodaciONacinuOstvarivanjaPrihoda>

  <DeklarisaniPodaciOVrstamaPrihoda>
    <SifraVrstePrihoda>111402000</SifraVrstePrihoda>  <!-- Dividend -->
    <BrutoPrihod>12345.50</BrutoPrihod>
    <OsnovicaZaPorez>12345.50</OsnovicaZaPorez>
    <ObracunatiPorez>1234.55</ObracunatiPorez>
    <PorezPlacenDrugojDrzavi>308.64</PorezPlacenDrugojDrzavi>
    <PorezZaUplatu>925.91</PorezZaUplatu>
  </DeklarisaniPodaciOVrstamaPrihoda>

</PodaciOPrijavi>
```

---

## Handler Loading Chain (flow diagram)

```
ExportFilingCommand(FilingId)
        │
        ▼
IFilingRepository.GetByIdAsync(FilingId)
        │
        ├─ null ──────────────────────────► Error.NotFound("Filing not found.")
        │
        ▼
ITaxpayerProfileRepository.GetAsync()
        │
        ├─ null ──────────────────────────► Error.Domain("Taxpayer profile is required before exporting.")
        │
        ▼
filing.ReportId is null?
        │
        ├─ YES ───────────────────────────► paymentNotes = ""
        │
        ▼
IReportRepository.GetByIdAsync(filing.ReportId.Value)
        │
        ├─ null ──────────────────────────► paymentNotes = ""
        │
        ▼
IImporterRepository.GetByIdAsync(report.ImporterId)
        │
        ├─ null ──────────────────────────► paymentNotes = ""
        │
        ▼
paymentNotes = importer.PaymentNotes  (string, never null)
        │
        ▼
_serializer.Serialize(filing, profile, paymentNotes)
        │
        ▼
Result<ExportFilingResult, Error>.Success(
    new ExportFilingResult(bytes, $"PP-OPO_{period}_{profile.Jmbg}.xml"))
```

---

## Validation Rules

| Condition | Error Code | Message |
|---|---|---|
| Filing not found by ID | `NOT_FOUND` | `"Filing not found."` |
| TaxpayerProfile not configured | `DOMAIN_ERROR` | `"Taxpayer profile is required before exporting."` |
| User cancels save dialog | — | No error; silent return |
| Write permission denied | `INFRASTRUCTURE_ERROR` | `"Could not write export file: {path}"` (Desktop layer) |

---

## Testing Surface

| Test Class | Scenarios |
|---|---|
| `ExportFilingCommandHandlerTests` | Filing not found; Profile missing; ReportId null (PaymentNotes empty); Full success — Dividend; Full success — Interest |
| `PpOpoXmlSerializerTests` | Dividend — all XML elements; Interest — SifraVrstePrihoda = 111401000; CDATA on FullName + Address; Decimal formatting (zero, large, two-decimal); PhoneNumber null → empty `<Telefon>`; Email null → empty `<Email>`; PaymentNotes empty → empty `<Ostalo>` |
