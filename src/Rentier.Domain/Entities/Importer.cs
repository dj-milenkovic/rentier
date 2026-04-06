using Rentier.Domain.Exceptions;

namespace Rentier.Domain.Entities;

/// <summary>
/// Represents a named import configuration for filtering and reading IBKR CSV activity statements.
/// </summary>
public sealed class Importer
{
    public Guid Id { get; }
    public string DisplayName { get; }
    public string FilterExpression { get; }

    public Importer(Guid id, string displayName, string filterExpression = "")
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("DisplayName must not be null or whitespace");

        Id = id;
        DisplayName = displayName;
        FilterExpression = filterExpression ?? string.Empty;
    }
}
