# Contract: PP-OPO XML Export

**Feature**: 033-ppopo-xml-schema-compliance  
**Type**: File export (XML document)  
**Consumer**: ePorezi portal (Serbian Tax Administration)

## Interface: `IXmlFilingSerializer`

**Location**: `src/Rentier.Application/Interfaces/IXmlFilingSerializer.cs`

```csharp
public interface IXmlFilingSerializer
{
    byte[] Serialize(Filing filing, TaxpayerProfile profile, string paymentNotes);
}
```

**Contract**: Unchanged — same method signature, different output format.

---

## Output Contract: XML Document

### Encoding
- XML declaration: `<?xml version="1.0" encoding="UTF-8"?>` (uppercase `UTF-8`)
- Byte encoding: UTF-8 without BOM

### Namespace
- URI: `http://pid.purs.gov.rs`
- Prefix: `ns1`
- Applied to: root element and every child element

### Root Element
```xml
<ns1:PodaciPoreskeDeklaracije xmlns:ns1="http://pid.purs.gov.rs">
```

### Required Sections (in order)

1. **PodaciOPrijavi** — Filing metadata
   - `VrstaPrijave`: always `"1"`
   - `ObracunskiPeriod`: `{IncomeDate:yyyy-MM}`
   - `Rok`: always `"1"`

2. **PodaciOPoreskomObvezniku** — Taxpayer identification
   - `PoreskiIdentifikacioniBroj > JMBGPodnosiocaPrijave`: profile JMBG
   - `ImePrezimeObveznika`: profile FullName (plain text, no CDATA)
   - `UlicaBrojPoreskogObveznika`: profile Address (plain text, no CDATA)
   - `PrebivalisteOpstina`: profile OpstinaCode
   - `TelefonKontaktOsobe`: profile PhoneNumber (empty string if null)
   - `ElektronskaPosta`: profile Email (empty string if null)

3. **PodaciONacinuOstvarivanjaPrihoda** — Payment method
   - `NacinIsplate`: always `"3"`
   - `Ostalo`: paymentNotes parameter

4. **PodaciOVrstamaPrihoda** — Income rows (one per filing)
   - `RedniBroj`: `"1"` (single income row per filing)
   - `SifraVrstePrihoda`: `"111402000"` (Dividend) or `"111401000"` (Interest)
   - `DatumOstvarivanjaPrihoda`: `{IncomeDate:yyyy-MM-dd}`
   - `DatumDospelostiObaveze`: `{FilingDeadline:yyyy-MM-dd}`
   - `BrutoPrihod`: `{GrossIncomeRsd:F2}`
   - `NormaraniTroskovi`: `"0.00"`
   - `OsnovicaZaPorez`: `{GrossIncomeRsd:F2}` (**tax base = gross income**)
   - `ObracunatiPorez`: `{GrossTaxPayableRsd:F2}`
   - `PorezPlacenDrugojDrzavi`: `{WhtPaidRsd:F2}`
   - `PorezZaUplatu`: `{TaxPayableRsd:F2}`
   - `OsnovicaZaDoprinose`: `"0.00"`
   - `ObracunatiDoprinosi`: `"0.00"`
   - `DoprinosiPlaceniDrugojDrzavi`: `"0.00"`
   - `DoprinosiZaUplatu`: `"0.00"`

5. **Ukupno** — Totals (mirrors income row for single-row filings)
   - Same monetary fields as PodaciOVrstamaPrihoda (minus RedniBroj, SifraVrstePrihoda, dates)

6. **Kamata** — Interest/penalties
   - `PorezZaUplatu`: `"0.00"`
   - `DoprinosiZaUplatu`: `"0.00"`

7. **PodaciODodatnojKamati** — Additional interest (empty self-closing element)

### Monetary Formatting
- All monetary values: `decimal.ToString("F2", CultureInfo.InvariantCulture)`
- Examples: `"12345.50"`, `"0.00"`, `"100000.00"`

---

## Output Contract: Export Filename

### Interface

Filename is returned via `ExportFilingResult.SuggestedFileName`.

### Convention

| Condition | Pattern | Example |
|-----------|---------|---------|
| Ticker available | `{yyyy}-{MM}-{Ticker}.xml` | `2025-03-BABA.xml` |
| Ticker null, PayingEntity available | `{yyyy}-{MM}-{SanitizedPayingEntity}.xml` | `2025-03-ACME_Corp.xml` |
| Both null/empty | `{yyyy}-{MM}-filing.xml` | `2025-03-filing.xml` |

### Sanitization Rules
- Replace `\ / : * ? " < > |` with `_`
- Trim leading/trailing whitespace and underscores
- If result is empty after sanitization, use `"filing"`
