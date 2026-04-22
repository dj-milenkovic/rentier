using System.Text;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Serialization;

namespace Rentier.Infrastructure.Tests.Serialization;

/// <summary>
/// Snapshot tests for PpOpoXmlSerializer using Verify.Xunit.
/// These tests verify the complete XML output structure remains stable.
/// </summary>
public class PpOpoXmlSerializerSnapshotTests
{
    private readonly PpOpoXmlSerializer _sut = new();

    // IMPORTANT: All test data must be deterministic (no random GUIDs, fixed dates)
    private static Filing MakeRepresentativeFiling() =>
        Filing.CreateFromIncome(
            taxpayerProfileId: new Guid("00000000-0000-0000-0000-000000000001"),
            incomeType: IncomeType.Dividend,
            payingEntity: "ACME Corp",
            incomeDate: new DateOnly(2025, 3, 15),
            grossIncomeRsd: 12345.50m,
            whtPaidRsd: 123.45m,
            grossTaxPayableRsd: 1234.55m,
            taxPayableRsd: 1111.10m,
            filingDeadline: new DateOnly(2025, 4, 30));

    private static TaxpayerProfile MakeProfile() =>
        new(
            id: new Guid("00000000-0000-0000-0000-000000000002"),
            jmbg: "1234567890123",
            fullName: "Jovan Jovanovic",
            address: "Knez Mihailova 1, Beograd",
            opstinaCode: "11001",
            phoneNumber: "0612345678",
            email: "jovan@example.com");

    [Fact]
    public async Task Serialize_RepresentativeDividendFiling_MatchesSnapshot()
    {
        var bytes = _sut.Serialize(
            MakeRepresentativeFiling(),
            MakeProfile(),
            "IBAN: RS35170006000123456789 Model: 97 Poziv: 2025-0001").Value;

        var xml = Encoding.UTF8.GetString(bytes);

        await Verify(xml, "xml");
    }
}
