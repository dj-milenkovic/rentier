using Rentier.Domain.Enums;

namespace Rentier.Application.DTOs;

/// <summary>Read-model projection of a Report for list display.</summary>
public sealed record ReportRowDto(
    Guid         Id,
    string       ReportName,
    DateOnly     ImportDate,
    string       ImporterName,
    ReportStatus Status,
    int          FilingCount);
