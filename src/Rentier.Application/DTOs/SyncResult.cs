namespace Rentier.Application.DTOs;

public sealed record SyncResult(
    int ReportsCreated,
    int ReportsSkipped,
    int RevisionsCreated,
    int ReportsReprocessed,
    IReadOnlyList<string> Errors)
{
    public SyncResult(int reportsCreated, IReadOnlyList<string> errors)
        : this(reportsCreated, 0, 0, 0, errors) { }
}
