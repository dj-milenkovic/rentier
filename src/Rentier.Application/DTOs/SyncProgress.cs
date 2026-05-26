namespace Rentier.Application.DTOs;

public sealed record SyncProgress(int Total, int Processed, string? CurrentFile, bool IsComplete);
