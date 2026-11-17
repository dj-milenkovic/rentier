using Rentier.Domain.Enums;

namespace Rentier.Application.DTOs;

/// <summary>
/// Encapsulates all optional column filter criteria for the Reports page query.
/// All fields are optional (null = no filter for that column).
/// Multiple non-null fields are combined with AND logic.
/// <see cref="ImporterIds"/> restricts results to the listed importers: an empty list matches
/// no report (it is what an <see cref="ImporterContains"/> term matching no importer resolves to).
/// </summary>
public sealed record ReportColumnFilter(
    string? NameContains = null,
    string? ImporterContains = null,
    IReadOnlyList<Guid>? ImporterIds = null,
    string? ImportDateContains = null,
    string? EmailDateContains = null,
    int? FilingCountValue = null,
    IReadOnlySet<ReportStatus>? StatusFilters = null);
