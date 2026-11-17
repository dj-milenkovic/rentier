namespace Rentier.Desktop.Services;

/// <summary>The report file and importer the user chose in the import dialog.</summary>
public sealed record ReportImportSelection(Guid ImporterId, string FileName, byte[] Content);

/// <summary>Runs the "pick a CSV file, then pick an importer" dialog flow for report import.</summary>
public interface IReportImportDialogService
{
    /// <summary>Returns the user's selection, or <c>null</c> when they cancel at any step.</summary>
    Task<ReportImportSelection?> PickReportAsync();
}
