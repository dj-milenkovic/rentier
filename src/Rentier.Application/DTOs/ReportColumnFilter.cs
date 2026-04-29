using Rentier.Application.Enums;
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
    ComparisonOperator ImportDateOperator = ComparisonOperator.Equals,
    DateOnly? ImportDateValue = null,
    ComparisonOperator EmailDateOperator = ComparisonOperator.Equals,
    DateOnly? EmailDateValue = null,
    ComparisonOperator FilingCountOperator = ComparisonOperator.Equals,
    int? FilingCountValue = null,
    ReportStatus? StatusFilter = null);
