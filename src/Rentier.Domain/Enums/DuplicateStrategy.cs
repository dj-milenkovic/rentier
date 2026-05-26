namespace Rentier.Domain.Enums;

/// <summary>
/// Determines what happens when a replay run encounters a report that already exists in the database.
/// </summary>
public enum DuplicateStrategy
{
    /// <summary>Skip the duplicate silently. Safe and idempotent — existing data is unchanged.</summary>
    SkipExisting = 0,
    /// <summary>Create a new <see cref="Rentier.Domain.Entities.Report"/> linked to the original via <c>OriginalReportId</c>.</summary>
    CreateNewRevision = 1,
    /// <summary>
    /// Overwrite the existing report's content and delete its filings so it is re-processed.
    /// Automatically falls back to <see cref="CreateNewRevision"/> when any filing has a status other than Init
    /// (i.e., has been filed or paid), to preserve official submission records.
    /// </summary>
    ReprocessInPlace = 2
}
