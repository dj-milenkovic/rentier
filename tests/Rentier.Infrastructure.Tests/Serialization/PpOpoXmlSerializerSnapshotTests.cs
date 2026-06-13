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
            address: "Knez Mihailova 1",
            opstinaCode: "018",
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

    // ── T009: Interest income type snapshot ───────────────────────────────────

    /// <summary>
    /// Verifies that serializing an Interest-type filing produces XML where
    /// SifraVrstePrihoda contains the interest code (111401000) rather than
    /// the dividend code (111402000).
    /// </summary>
    [Fact]
    public async Task Serialize_InterestIncomeFiling_MatchesSnapshot()
    {
        var filing = Filing.CreateFromIncome(
            taxpayerProfileId: new Guid("00000000-0000-0000-0000-000000000003"),
            incomeType: IncomeType.Interest,
            payingEntity: "First Bank",
            incomeDate: new DateOnly(2025, 5, 10),
            grossIncomeRsd: 5000.00m,
            whtPaidRsd: 0m,
            grossTaxPayableRsd: 750.00m,
            taxPayableRsd: 750.00m,
            filingDeadline: new DateOnly(2025, 6, 15));

        var bytes = _sut.Serialize(filing, MakeProfile(), "Interest payment reference").Value;
        var xml = Encoding.UTF8.GetString(bytes);

        await Verify(xml, "xml");
    }

    // ── T010: Payment reference snapshot ─────────────────────────────────────

    /// <summary>
    /// Verifies that a filing with a payment reference string produces XML where
    /// the reference appears in the correct element (Ostalo).
    /// </summary>
    [Fact]
    public async Task Serialize_FilingWithPaymentReference_MatchesSnapshot()
    {
        var filing = Filing.CreateFromIncome(
            taxpayerProfileId: new Guid("00000000-0000-0000-0000-000000000001"),
            incomeType: IncomeType.Dividend,
            payingEntity: "ACME Corp",
            incomeDate: new DateOnly(2025, 3, 15),
            grossIncomeRsd: 12345.50m,
            whtPaidRsd: 123.45m,
            grossTaxPayableRsd: 1234.55m,
            taxPayableRsd: 1111.10m,
            filingDeadline: new DateOnly(2025, 4, 30));

        filing.SetPaymentReference("REF-12345");

        var bytes = _sut.Serialize(filing, MakeProfile(), "REF-12345").Value;
        var xml = Encoding.UTF8.GetString(bytes);

        await Verify(xml, "xml");
    }

    // ── T011: Fully-populated filing snapshot ─────────────────────────────────

    /// <summary>
    /// Regression baseline for a fully-populated filing (all optional fields set,
    /// Interest income type, non-trivial amounts).
    /// </summary>
    [Fact]
    public async Task Serialize_FullyPopulatedFilingDetails_MatchesSnapshot()
    {
        var filing = Filing.CreateFromIncome(
            taxpayerProfileId: new Guid("00000000-0000-0000-0000-000000000001"),
            incomeType: IncomeType.Interest,
            payingEntity: "Premium Bank d.o.o.",
            incomeDate: new DateOnly(2025, 2, 28),
            grossIncomeRsd: 98765.43m,
            whtPaidRsd: 987.65m,
            grossTaxPayableRsd: 14814.81m,
            taxPayableRsd: 13827.16m,
            filingDeadline: new DateOnly(2025, 4, 15));

        filing.SetPaymentReference("IBAN: RS35170006000987654321 Model: 97 Poziv: 2025-0042");

        var bytes = _sut.Serialize(filing, MakeProfile(), "IBAN: RS35170006000987654321 Model: 97 Poziv: 2025-0042").Value;
        var xml = Encoding.UTF8.GetString(bytes);

        await Verify(xml, "xml");
    }
}
