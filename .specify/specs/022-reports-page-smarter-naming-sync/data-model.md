# Data Model: Reports Page – Smarter Naming and Sync Clarification

**Feature**: `003-reports-naming-sync` | **Date**: 2025-07-09

## Existing Entities (no changes)

### Report

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Primary key (client-generated) |
| `ImportDate` | `DateOnly` | Date the report was imported (UTC today) |
| `ImporterId` | `Guid` | FK → Importer.Id |
| `Status` | `ReportStatus` (enum) | Init → Processed / PartialError / Error |
| `ReportName` | `string` (max 500) | Original file name (e.g., `IBKR_Activity_2024-03-15.csv`) |
| `AttachmentContent` | `byte[]?` | Raw file content |
| `MailboxMessageId` | `long?` | Source IMAP message ID |
| `OriginalReportId` | `Guid?` | FK → Report.Id (for revisions) |

**Relationships**:
- Report → Importer (many-to-one via ImporterId)
- Report → Filing (one-to-many via Filing.ReportId)
- Report → Report (self-referencing via OriginalReportId for revisions)

**Indexes**: `ImporterId`, `(ImporterId, ReportName)` UNIQUE, `OriginalReportId`

### Filing

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Primary key |
| `TaxpayerProfileId` | `Guid` | FK → TaxpayerProfile.Id |
| `TaxPeriod` | `DateOnly` | Tax period (= IncomeDate) |
| `Status` | `FilingStatus` (enum) | Init → Filed → Paid |
| `IncomeType` | `IncomeType` (enum) | Dividend / Interest |
| `PayingEntity` | `string` (max 500) | Who paid the income |
| **`IncomeDate`** | **`DateOnly`** | **← Used by this feature to derive statement date** |
| `GrossIncomeRsd` | `decimal(18,2)` | Gross income in RSD |
| `WhtPaidRsd` | `decimal(18,2)` | Withholding tax paid in RSD |
| `GrossTaxPayableRsd` | `decimal(18,2)` | Gross tax payable |
| `TaxPayableRsd` | `decimal(18,2)` | Net tax payable |
| `FilingDeadline` | `DateOnly` | Computed deadline |
| `ReportId` | `Guid?` | FK → Report.Id (nullable, SET NULL on delete) |
| `PaymentReference` | `string?` (max 200) | Payment reference |
| `ExchangeRateSourceDate` | `DateOnly?` | Exchange rate date |
| `ExchangeRateSourceType` | `ExchangeRateSourceType?` | Source type enum |

**Relationships**:
- Filing → Report (many-to-one via ReportId, nullable)
- Filing → TaxpayerProfile (many-to-one via TaxpayerProfileId)

**Indexes**: `TaxpayerProfileId`, `ReportId`

### Importer

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Primary key |
| **`DisplayName`** | **`string` (max 200)** | **← Used by this feature as importer portion of display name** |
| `ReportType` | `ReportType` (enum) | e.g., IbkrCsv |
| `TaxpayerProfileId` | `Guid?` | FK → TaxpayerProfile.Id |
| `MailboxId` | `Guid?` | FK → Mailbox.Id |
| `FromFilter` | `string` (max 500) | Email sender filter |
| `SubjectFilter` | `string` (max 500) | Email subject filter |
| `AttachmentRegex` | `string` (max 1000) | Attachment filename regex |
| `PaymentNotes` | `string` (max 4000) | Payment notes template |

---

## Modified DTO

### ReportRowDto (Application layer)

**Current** (6 positional parameters):
```csharp
public sealed record ReportRowDto(
    Guid         Id,
    string       ReportName,
    DateOnly     ImportDate,
    string       ImporterName,
    ReportStatus Status,
    int          FilingCount);
```

**New** (8 positional parameters — 2 added):
```csharp
public sealed record ReportRowDto(
    Guid         Id,
    string       ReportName,       // Original file name (preserved for tooltip)
    string       DisplayName,      // NEW: Friendly label "<ImporterName> – <Date>"
    DateOnly     ImportDate,
    string       ImporterName,
    ReportStatus Status,
    int          FilingCount,
    DateOnly?    EarliestIncomeDate);  // NEW: null when no filings
```

**Validation rules**:
- `DisplayName` MUST NOT be null or empty — the handler always computes a value.
- `ReportName` retains its original semantics (raw file name).
- `EarliestIncomeDate` is null when the report has zero filings (fallback to ImportDate is done at computation time, not stored as null).

---

## New Repository Method

### IFilingRepository

**New method signature**:
```csharp
/// <summary>
/// Returns the earliest IncomeDate among all filings linked to the given report.
/// Returns null when no filings exist for the report.
/// </summary>
Task<DateOnly?> GetEarliestIncomeDateByReportIdAsync(
    Guid reportId,
    CancellationToken ct = default);
```

**EF Core implementation** (Infrastructure layer):
```csharp
public async Task<DateOnly?> GetEarliestIncomeDateByReportIdAsync(
    Guid reportId, CancellationToken ct = default)
{
    return await _db.Filings
        .AsNoTracking()
        .Where(f => f.ReportId == reportId)
        .Select(f => (DateOnly?)f.IncomeDate)
        .MinAsync(ct);
}
```

**Generated SQL** (approximate):
```sql
SELECT MIN("f"."IncomeDate") FROM "Filings" AS "f" WHERE "f"."ReportId" = @reportId
```

---

## Display Name Derivation Logic

**Location**: `GetReportsQueryHandler.HandleAsync`

**Algorithm**:
```
For each report:
  1. importerName = importerNames.GetValueOrDefault(report.ImporterId, "Unknown")
  2. earliestDate = await _filings.GetEarliestIncomeDateByReportIdAsync(report.Id, ct)
  3. effectiveDate = earliestDate ?? report.ImportDate
  4. displayName = $"{importerName} – {effectiveDate:yyyy-MM-dd}"   // en dash U+2013
```

**Format**: `"{ImporterDisplayName} – {yyyy-MM-dd}"` using en dash (–), not hyphen (-).

**Fallback chain**:
1. Importer not found → use "Unknown"
2. No filings for report → use Report.ImportDate
3. Both available → use Importer.DisplayName + earliest Filing.IncomeDate

---

## State Transitions

No state transitions are introduced or modified by this feature. The display name is a pure derived value computed at read time.

---

## String Resources (new keys)

| Key | Value (en) | Used by |
|-----|-----------|---------|
| `Reports_Sync_Subtitle` | "Sync downloads new statements from your configured mailboxes and creates reports. For per-mailbox status and history, use the Sync page." | ReportsView.axaml subtitle near Sync button |
| `Reports_Col_DisplayName` | "Report" | DataGrid column header (replaces "Report Name") |

**Note**: The existing `Reports_Col_Name` key ("Report Name") will be replaced by `Reports_Col_DisplayName` ("Report") as the column header, since the column now shows the friendly display name rather than the raw file name.
