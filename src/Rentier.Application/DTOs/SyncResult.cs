namespace Rentier.Application.DTOs;

public sealed record SyncResult(int ReportsCreated, IReadOnlyList<string> Errors);
