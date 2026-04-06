using Rentier.Domain.Exceptions;

namespace Rentier.Domain.Entities;

/// <summary>
/// Represents a parsed activity statement produced from an imported IBKR CSV file.
/// ImportDate is DateOnly per constitution Principle III.
/// </summary>
public sealed class Report
{
    public Guid Id { get; }
    public DateOnly ImportDate { get; }
    public Guid ImporterId { get; }

    public Report(Guid id, DateOnly importDate, Guid importerId)
    {
        if (importDate > DateOnly.FromDateTime(DateTime.UtcNow.Date))
            throw new DomainException("ImportDate must not be in the future");

        Id = id;
        ImportDate = importDate;
        ImporterId = importerId;
    }
}
