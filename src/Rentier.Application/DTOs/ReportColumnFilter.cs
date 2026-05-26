using Rentier.Domain.Enums;

namespace Rentier.Application.DTOs;

/// <summary>
/// Encapsulates all optional column filter criteria for the Reports page query.
/// All fields are optional (null = no filter for that column).
/// Multiple non-null fields are combined with AND logic.
/// </summary>
public sealed record ReportColumnFilter(
    string? NameContains = null,
    string? ImporterContains = null,
    IReadOnlyList<Guid>? ImporterIds = null,
    string? ImportDateContains = null,
    string? EmailDateContains = null,
    int? FilingCountValue = null,
    IReadOnlySet<ReportStatus>? StatusFilters = null);
