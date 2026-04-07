# Application Contracts: PP-OPO XML Export

**Feature**: 013-pp-opo-xml-export  
**Plan**: [../plan.md](../plan.md)  
**Layer boundary**: Application ↔ Infrastructure ↔ Desktop

---

## Overview

This document defines all public contracts introduced or extended by feature 013. Contracts
are the interfaces, command/result records, and delegate signatures that cross layer
boundaries. Infrastructure and Desktop MUST implement or consume these contracts exactly.

---

## 1. Application Layer Contracts

### 1.1 `ExportFilingCommand`

**File**: `Rentier.Application/Commands/ExportFilingCommand.cs`

```csharp
namespace Rentier.Application.Commands;

/// <summary>
/// Loads a filing and all required context, then serializes it to a PP-OPO XML byte array.
/// Returns the bytes and suggested save filename on success, or a descriptive Error on failure.
/// </summary>
public sealed record ExportFilingCommand(Guid FilingId);
```

### 1.2 `ExportFilingResult`

**File**: `Rentier.Application/Commands/ExportFilingResult.cs`

```csharp
namespace Rentier.Application.Commands;

/// <summary>
/// The successful output of ExportFilingCommand: serialized XML bytes and the pre-computed
/// suggested filename for the native OS save dialog.
/// </summary>
/// <param name="Bytes">UTF-8 encoded PP-OPO XML bytes, ready to write to disk.</param>
/// <param name="SuggestedFileName">Pre-formatted filename, e.g. "PP-OPO_2025-03_1234567890123.xml".</param>
public sealed record ExportFilingResult(byte[] Bytes, string SuggestedFileName);
```

**Filename format**: `PP-OPO_{IncomeDate:yyyy-MM}_{profile.Jmbg}.xml`

### 1.3 `IXmlFilingSerializer`

**File**: `Rentier.Application/Interfaces/IXmlFilingSerializer.cs`

```csharp
using Rentier.Domain.Entities;

namespace Rentier.Application.Interfaces;

/// <summary>
/// Abstracts the serialization of a PP-OPO filing into a UTF-8 XML byte array.
/// Defined in Application so the handler can depend on it; implemented in Infrastructure.
/// </summary>
public interface IXmlFilingSerializer
{
    /// <summary>
    /// Serializes the filing and taxpayer context into a UTF-8 XML byte array conforming
    /// to the ePorezi PP-OPO schema.
    /// </summary>
    /// <param name="filing">The filing aggregate root. Must not be null.</param>
    /// <param name="profile">The taxpayer profile. Must not be null.</param>
    /// <param name="paymentNotes">
    /// Importer payment notes for the <c>Ostalo</c> element.
    /// Pass <see cref="string.Empty"/> when no importer is linked.
    /// </param>
    /// <returns>UTF-8 XML bytes with XML declaration and no BOM.</returns>
    byte[] Serialize(Filing filing, TaxpayerProfile profile, string paymentNotes);
}
```

**Constraints**:
- Returns a UTF-8 encoded byte array with XML declaration (`<?xml version="1.0" encoding="utf-8"?>`).
- No BOM (use `new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)`).
- Method is synchronous — CPU-only serialization, no I/O.
- Must NOT throw for `paymentNotes = null`; treat null as `string.Empty` defensively.

### 1.4 `ExportFilingCommandHandler`

**File**: `Rentier.Application/Handlers/ExportFilingCommandHandler.cs`

```csharp
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

public sealed class ExportFilingCommandHandler
    : ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>
{
    public ExportFilingCommandHandler(
        IFilingRepository filingRepository,
        IReportRepository reportRepository,
        IImporterRepository importerRepository,
        ITaxpayerProfileRepository profileRepository,
        IXmlFilingSerializer serializer) { ... }

    public async Task<Result<ExportFilingResult, Error>> HandleAsync(
        ExportFilingCommand command, CancellationToken ct = default) { ... }
}
```

**Loading chain** (see `data-model.md` for flow diagram):

1. `IFilingRepository.GetByIdAsync(command.FilingId, ct)` — failure: `Error.NotFound("Filing not found.")`
2. `ITaxpayerProfileRepository.GetAsync(ct)` — failure: `Error.Domain("Taxpayer profile is required before exporting.")`
3. If `filing.ReportId` is not null: `IReportRepository.GetByIdAsync(filing.ReportId.Value, ct)` (null is non-fatal)
4. If report is not null: `IImporterRepository.GetByIdAsync(report.ImporterId, ct)` (null is non-fatal)
5. `paymentNotes = importer?.PaymentNotes ?? string.Empty`
6. `bytes = _serializer.Serialize(filing, profile, paymentNotes)`
7. `suggestedName = $"PP-OPO_{filing.IncomeDate:yyyy-MM}_{profile.Jmbg}.xml"`
8. Return `Result<ExportFilingResult, Error>.Success(new ExportFilingResult(bytes, suggestedName))`

---

## 2. Infrastructure Contract

### 2.1 `PpOpoXmlSerializer`

**File**: `Rentier.Infrastructure/Serialization/PpOpoXmlSerializer.cs`

```csharp
using Rentier.Application.Interfaces;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Rentier.Infrastructure.Serialization;

public sealed class PpOpoXmlSerializer : IXmlFilingSerializer
{
    public byte[] Serialize(Filing filing, TaxpayerProfile profile, string paymentNotes) { ... }

    private static string MapIncomeType(IncomeType type) => type switch
    {
        IncomeType.Interest => "111401000",
        IncomeType.Dividend => "111402000",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    private static string Fmt(decimal value) =>
        value.ToString("F2", CultureInfo.InvariantCulture);
}
```

**XML document skeleton** (produced by `Serialize`):

```xml
<?xml version="1.0" encoding="utf-8"?>
<PodaciOPrijavi>
  <VrstaPrijave>1</VrstaPrijave>
  <ObracunskiPeriod>{filing.IncomeDate:yyyy-MM}</ObracunskiPeriod>
  <DatumOstvarivanjaPrihoda>{filing.IncomeDate:yyyy-MM-dd}</DatumOstvarivanjaPrihoda>
  <DatumDospelostiObaveze>{filing.FilingDeadline:yyyy-MM-dd}</DatumDospelostiObaveze>

  <PodaciOPoreskomObvezniku>
    <JMBG>{profile.Jmbg}</JMBG>
    <Ime><![CDATA[{profile.FullName}]]></Ime>
    <Adresa><![CDATA[{profile.Address}]]></Adresa>
    <SifraOpstine>{profile.OpstinaCode}</SifraOpstine>
    <Telefon>{profile.PhoneNumber ?? ""}</Telefon>
    <Email>{profile.Email ?? ""}</Email>
  </PodaciOPoreskomObvezniku>

  <PodaciONacinuOstvarivanjaPrihoda>
    <NacinIsplate>3</NacinIsplate>
    <Ostalo>{paymentNotes}</Ostalo>
  </PodaciONacinuOstvarivanjaPrihoda>

  <DeklarisaniPodaciOVrstamaPrihoda>
    <SifraVrstePrihoda>{MapIncomeType(filing.IncomeType)}</SifraVrstePrihoda>
    <BrutoPrihod>{Fmt(filing.GrossIncomeRsd)}</BrutoPrihod>
    <OsnovicaZaPorez>{Fmt(filing.GrossTaxPayableRsd)}</OsnovicaZaPorez>
    <ObracunatiPorez>{Fmt(filing.GrossTaxPayableRsd)}</ObracunatiPorez>
    <PorezPlacenDrugojDrzavi>{Fmt(filing.WhtPaidRsd)}</PorezPlacenDrugojDrzavi>
    <PorezZaUplatu>{Fmt(filing.TaxPayableRsd)}</PorezZaUplatu>
  </DeklarisaniPodaciOVrstamaPrihoda>

</PodaciOPrijavi>
```

**CDATA implementation** (XDocument pattern):

```csharp
new XElement("Ime", new XCData(profile.FullName)),
new XElement("Adresa", new XCData(profile.Address)),
```

**Byte array serialization** (no BOM):

```csharp
using var ms = new MemoryStream();
using var writer = new StreamWriter(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
doc.Save(writer);
writer.Flush();
return ms.ToArray();
```

### 2.2 Registration in `InfrastructureServiceExtensions`

```csharp
services.AddTransient<IXmlFilingSerializer, PpOpoXmlSerializer>();
```

Added inside `AddInfrastructureServices(...)` alongside other transient infrastructure services.

---

## 3. Desktop Contracts

### 3.1 `FilingsViewModel` — new constructor parameter and command

**Extended constructor** (additions in bold):

```csharp
public FilingsViewModel(
    IQueryHandler<GetFilingsQuery, Result<FilingsPageResult, Error>> getFilings,
    ICommandHandler<UpdateFilingStatusCommand, Result<VoidResult, Error>> updateStatus,
    ICommandHandler<UpdatePaymentReferenceCommand, Result<VoidResult, Error>> updateReference,
    ICommandHandler<DeleteFilingCommand, Result<VoidResult, Error>> deleteFiling,
    Func<string, Task<bool>> confirmDelete,
    // NEW ↓
    ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>> exportFiling,
    Func<ExportFilingResult, Task> exportFileWriter,
    // END NEW ↑
    IScheduler? scheduler = null)
```

**New public command**:

```csharp
public ReactiveCommand<Guid, Unit> ExportCommand { get; }
```

**Command initialization**:

```csharp
ExportCommand = ReactiveCommand.CreateFromTask<Guid>(
    async (filingId, ct) =>
    {
        var result = await _exportFiling.HandleAsync(new ExportFilingCommand(filingId), ct);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error.Message;
            return;
        }
        await _exportFileWriter(result.Value);
    },
    outputScheduler: _scheduler);
```

> The `exportFileWriter` delegate owns the Avalonia `StorageProvider` interaction. On dialog
> cancellation (user dismisses without saving), the delegate returns silently without setting
> `ErrorMessage`. On write failure, the delegate should propagate an exception which the
> ReactiveCommand infrastructure surfaces (or the delegate can catch and set `ErrorMessage`
> via a shared callback — implementor's choice; prefer delegate-catches to avoid exception
> flow across async boundaries).

### 3.2 `exportFileWriter` delegate

**Type**: `Func<ExportFilingResult, Task>`  
**Registered in**: `CompositionRoot.AddDesktopServices()`

**Contract behaviour**:
- Opens the native OS save dialog via `StorageProvider.SaveFilePickerAsync(...)`.
- Pre-populates `SuggestedFileName` from `result.SuggestedFileName`.
- Sets file type filter to `*.xml`.
- If user cancels (returns `null`): returns `Task.CompletedTask` — **no error shown**.
- If user confirms: writes `result.Bytes` to the selected `IStorageFile` via
  `await file.OpenWriteAsync()` and `await stream.WriteAsync(result.Bytes)`.
- If write fails: re-throws (or surfaces via `ErrorMessage` — see note above).

**Registration skeleton**:

```csharp
services.AddTransient<Func<ExportFilingResult, Task>>(provider => async exportResult =>
{
    var topLevel = /* resolve from app lifetime / main window */;
    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
    {
        Title = Strings.Filings_Export_SaveDialog_Title,
        SuggestedFileName = exportResult.SuggestedFileName,
        FileTypeChoices = new[]
        {
            new FilePickerFileType(Strings.Filings_Export_FileType_Xml)
            {
                Patterns = new[] { "*.xml" }
            }
        }
    });

    if (file is null) return; // user cancelled

    await using var stream = await file.OpenWriteAsync();
    await stream.WriteAsync(exportResult.Bytes);
});
```

> **Implementation note on `TopLevel` resolution**: The delegate registered in
> `CompositionRoot` must obtain a `TopLevel` (or `Window`) reference at call time, not at
> registration time. Recommended approach: inject `Func<TopLevel>` or resolve the main window
> from a thin wrapper registered as a singleton. The exact pattern is implementation detail
> left to the implementor; it must not introduce a scoped/singleton lifetime mismatch.

### 3.3 `FilingsView.axaml` — Export column

**Position**: Insert before the existing Delete column.

**Preferred binding** (command binding through DataContext):

```xml
<DataGridTemplateColumn Width="80">
  <DataGridTemplateColumn.CellTemplate>
    <DataTemplate>
      <Button Content="{x:Static res:Strings.Filings_Export_Action_Button}"
              Command="{Binding DataContext.ExportCommand,
                        RelativeSource={RelativeSource AncestorType=DataGrid}}"
              CommandParameter="{Binding Id}"
              HorizontalAlignment="Center" />
    </DataTemplate>
  </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

**Fallback** (code-behind click handler, mirroring existing `DeleteButton_Click`):

```xml
<Button Content="{x:Static res:Strings.Filings_Export_Action_Button}"
        Tag="{Binding Id}"
        Click="ExportButton_Click"
        HorizontalAlignment="Center" />
```

```csharp
// FilingsView.axaml.cs
private void ExportButton_Click(object? sender, RoutedEventArgs e)
{
    if (sender is Button { Tag: Guid id } && DataContext is FilingsViewModel vm)
        vm.ExportCommand.Execute(id).Subscribe();
}
```

### 3.4 `Strings.resx` — new keys

| Key | Default English Value |
|---|---|
| `Filings_Export_Action_Button` | `Export` |
| `Filings_Export_SaveDialog_Title` | `Save PP-OPO XML` |
| `Filings_Export_FileType_Xml` | `XML Files` |

---

## 4. DI Registration Summary

| Service | Implementation | Lifetime | Registered in |
|---|---|---|---|
| `IXmlFilingSerializer` | `PpOpoXmlSerializer` | Transient | `InfrastructureServiceExtensions` |
| `ICommandHandler<ExportFilingCommand, Result<ExportFilingResult, Error>>` | `ExportFilingCommandHandler` | Transient | `CompositionRoot.AddDesktopServices()` |
| `Func<ExportFilingResult, Task>` | lambda (StorageProvider delegate) | Transient | `CompositionRoot.AddDesktopServices()` |

---

## 5. Error Catalogue

| Error factory | Code | Message | Raised by |
|---|---|---|---|
| `Error.NotFound(...)` | `NOT_FOUND` | `"Filing not found."` | Handler — filing lookup |
| `Error.Domain(...)` | `DOMAIN_ERROR` | `"Taxpayer profile is required before exporting."` | Handler — profile check |
| (write failure) | `INFRASTRUCTURE_ERROR` | `"Could not write export file."` | Desktop delegate (optional) |

---

## 6. Test Contract Stubs

### `ExportFilingCommandHandlerTests`

```csharp
// [Fact] Filing_NotFound_ReturnsNotFoundError()
// [Fact] Profile_Missing_ReturnsDomainError()
// [Fact] ReportId_Null_PaymentNotesEmpty_ReturnsSuccess()
// [Fact] FullChain_Dividend_ReturnsCorrectBytes()
// [Fact] FullChain_Interest_ReturnsCorrectBytes()
```

Mocks: `IFilingRepository`, `IReportRepository`, `IImporterRepository`,
`ITaxpayerProfileRepository`, `IXmlFilingSerializer` (all via NSubstitute).

### `PpOpoXmlSerializerTests`

```csharp
// [Fact] Dividend_SifraVrstePrihoda_Is_111402000()
// [Fact] Interest_SifraVrstePrihoda_Is_111401000()
// [Fact] FullName_WrappedInCDATA()
// [Fact] Address_WrappedInCDATA()
// [Fact] MonetaryValues_FormattedAs_TwoDecimalPlaces_InvariantCulture()
// [Fact] Zero_Amounts_FormattedAs_0_00()
// [Fact] PhoneNumber_Null_TelefonElement_IsEmpty()
// [Fact] Email_Null_EmailElement_IsEmpty()
// [Fact] PaymentNotes_Empty_OstaloElement_IsEmpty()
// [Fact] ObracunskiPeriod_DerivedFrom_IncomeDate_YearMonth()
// [Fact] XmlDeclaration_IsUtf8()
// [Fact] Output_HasNoByteOrderMark()
```

No mocks required — `PpOpoXmlSerializer` is a pure function of its inputs.
