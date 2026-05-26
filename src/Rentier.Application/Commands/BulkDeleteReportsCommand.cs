namespace Rentier.Application.Commands;

public sealed record BulkDeleteReportsCommand(IReadOnlyList<Guid> ReportIds);
