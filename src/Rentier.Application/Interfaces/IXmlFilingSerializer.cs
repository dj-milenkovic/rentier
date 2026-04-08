using Rentier.Domain.Entities;

namespace Rentier.Application.Interfaces;

/// <summary>
/// Serializes a PP-OPO filing and taxpayer context into a UTF-8 XML byte array
/// conforming to the ePorezi PP-OPO schema.
/// Output is UTF-8 with XML declaration and no BOM.
/// If <paramref name="paymentNotes"/> is null, it is treated as <see cref="string.Empty"/> defensively.
/// </summary>
public interface IXmlFilingSerializer
{
    /// <summary>Returns the serialized XML bytes for the given filing context.</summary>
    /// <param name="filing">The filing aggregate root.</param>
    /// <param name="profile">The taxpayer profile (must not be null).</param>
    /// <param name="paymentNotes">Importer payment notes; empty string if unavailable.</param>
    byte[] Serialize(Filing filing, TaxpayerProfile profile, string paymentNotes);
}
