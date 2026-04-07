namespace Rentier.Application.Queries;

/// <summary>
/// Returns all Report records as display rows with resolved importer name and filing count.
/// No pagination — all reports returned in a single call.
/// </summary>
public sealed record GetReportsQuery;
