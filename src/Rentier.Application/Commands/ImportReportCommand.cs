namespace Rentier.Application.Commands;

/// <summary>
/// Imports a CSV brokerage statement manually.
/// CsvContent is the raw file bytes read by the Desktop layer via Avalonia StorageProvider
/// BEFORE this command is dispatched — the handler never touches the file system.
/// </summary>
public sealed record ImportReportCommand(
    Guid   ImporterId,
    string FileName,
    byte[] CsvContent);
