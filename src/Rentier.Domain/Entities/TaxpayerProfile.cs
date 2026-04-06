using Rentier.Domain.Exceptions;

namespace Rentier.Domain.Entities;

/// <summary>
/// Represents the Serbian taxpayer whose income is being reported.
/// </summary>
public sealed class TaxpayerProfile
{
    public Guid Id { get; private set; }
    public string Jmbg { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string OpstinaCode { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }

    /// <summary>For EF Core materialization only.</summary>
    private TaxpayerProfile() { }

    public TaxpayerProfile(
        Guid id,
        string jmbg,
        string fullName,
        string address,
        string opstinaCode,
        string? phoneNumber = null,
        string? email = null)
    {
        if (string.IsNullOrWhiteSpace(jmbg) || jmbg.Length != 13 || !jmbg.All(char.IsDigit))
            throw new DomainException("JMBG must be exactly 13 digit characters");
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("FullName must not be null or whitespace");
        if (string.IsNullOrWhiteSpace(address))
            throw new DomainException("Address must not be null or whitespace");
        if (string.IsNullOrWhiteSpace(opstinaCode))
            throw new DomainException("OpstinaCode must not be null or whitespace");

        Id = id;
        Jmbg = jmbg;
        FullName = fullName;
        Address = address;
        OpstinaCode = opstinaCode;
        PhoneNumber = phoneNumber;
        Email = email;
    }
}
