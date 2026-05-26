using Rentier.Domain.Exceptions;

namespace Rentier.Domain.Entities;

public sealed class PublicHoliday
{
    public Guid Id { get; private set; }
    public DateOnly Date { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Year { get; private set; }

    private PublicHoliday() { }

    private PublicHoliday(Guid id, DateOnly date, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Holiday name must not be empty.");
        Id = id;
        Date = date;
        Name = name;
        Year = date.Year;
    }

    public static PublicHoliday Create(DateOnly date, string name) => new(Guid.NewGuid(), date, name);
}
