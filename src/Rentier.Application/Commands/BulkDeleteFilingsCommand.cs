namespace Rentier.Application.Commands;

public sealed record BulkDeleteFilingsCommand(IReadOnlyList<Guid> FilingIds);
