namespace Rentier.Application.Commands;

/// <summary>
/// Deletes a Report and all linked Filings.
/// Cascade deletion is performed at the application layer — not via DB FK cascade.
/// </summary>
public sealed record DeleteReportCommand(Guid ReportId);
