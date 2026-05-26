using Rentier.Domain.Exceptions;

namespace Rentier.Domain.Entities;

public sealed class UserPreference
{
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;

    /// <summary>For EF Core materialization only.</summary>
    private UserPreference() { }

    public UserPreference(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("Preference key must not be empty.");
        if (key.Length > 100)
            throw new DomainException("Preference key must not exceed 100 characters.");
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 500)
            throw new DomainException("Preference value must not exceed 500 characters.");
        Key = key.Trim();
        Value = value;
    }

    public void UpdateValue(string newValue)
    {
        ArgumentNullException.ThrowIfNull(newValue);
        if (newValue.Length > 500)
            throw new DomainException("Preference value must not exceed 500 characters.");
        Value = newValue;
    }
}
