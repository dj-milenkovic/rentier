using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Infrastructure.Serialization;
using Xunit;

namespace Rentier.Infrastructure.Tests.Serialization;

[Trait("Category", "Integration")]
public class PpOpoXmlSerializerTests
{
    private readonly PpOpoXmlSerializer _sut = new();

    private static Filing MakeFiling(
        IncomeType incomeType = IncomeType.Dividend,
        DateOnly? incomeDate = null,
        DateOnly? deadline = null,
        decimal grossIncome = 12345.50m,
        decimal whtPaid = 123.45m,
        decimal grossTaxPayable = 1234.55m,
        decimal taxPayable = 1111.10m)
    {
        var date = incomeDate ?? new DateOnly(2025, 3, 15);
        var dl = deadline ?? new DateOnly(2025, 4, 30);
        return Filing.CreateFromIncome(
            Guid.NewGuid(), incomeType, "ACME Corp",
            date, grossIncome, whtPaid, grossTaxPayable, taxPayable, dl);
    }

    private static TaxpayerProfile MakeProfile(
        string jmbg = "1234567890123",
        string fullName = "John Doe",
        string address = "Main St 1, Belgrade",
        string opstinaCode = "11001",
        string? phone = null,
        string? email = null)
        => new TaxpayerProfile(Guid.NewGuid(), jmbg, fullName, address, opstinaCode, phone, email);

    private static XElement ParseRoot(byte[] bytes)
    {
        var xml = Encoding.UTF8.GetString(bytes);
        return XDocument.Parse(xml).Root!;
    }

    [Fact]
    public void Serialize_Dividend_SifraIs111402000()
    {
        var filing = MakeFiling(IncomeType.Dividend);
        var profile = MakeProfile();

        var bytes = _sut.Serialize(filing, profile, string.Empty);

        var root = ParseRoot(bytes);
        root.Descendants("SifraVrstePrihoda").Single().Value.Should().Be("111402000");
    }

    [Fact]
    public void Serialize_Interest_SifraIs111401000()
    {
        var filing = MakeFiling(IncomeType.Interest);
        var profile = MakeProfile();

        var bytes = _sut.Serialize(filing, profile, string.Empty);

        var root = ParseRoot(bytes);
        root.Descendants("SifraVrstePrihoda").Single().Value.Should().Be("111401000");
    }

    [Fact]
    public void Serialize_AllMonetaryFields_FormattedWithTwoDp()
    {
        var filing = MakeFiling(grossIncome: 12345.50m, whtPaid: 123.45m,
            grossTaxPayable: 1234.55m, taxPayable: 1111.10m);
        var profile = MakeProfile();

        var bytes = _sut.Serialize(filing, profile, string.Empty);

        var root = ParseRoot(bytes);
        root.Descendants("BrutoPrihod").Single().Value.Should().Be("12345.50");
        root.Descendants("PorezPlacenDrugojDrzavi").Single().Value.Should().Be("123.45");
        root.Descendants("OsnovicaZaPorez").Single().Value.Should().Be("1234.55");
        root.Descendants("ObracunatiPorez").Single().Value.Should().Be("1234.55");
        root.Descendants("PorezZaUplatu").Single().Value.Should().Be("1111.10");
    }

    [Fact]
    public void Serialize_ZeroAmount_FormattedAs0Dot00()
    {
        var filing = MakeFiling(grossIncome: 0m, whtPaid: 0m, grossTaxPayable: 0m, taxPayable: 0m);
        var profile = MakeProfile();

        var bytes = _sut.Serialize(filing, profile, string.Empty);

        var root = ParseRoot(bytes);
        root.Descendants("BrutoPrihod").Single().Value.Should().Be("0.00");
    }

    [Fact]
    public void Serialize_IncomeDate_FormattedAsYyyyMmDd()
    {
        var filing = MakeFiling(incomeDate: new DateOnly(2025, 3, 15));
        var profile = MakeProfile();

        var bytes = _sut.Serialize(filing, profile, string.Empty);

        var root = ParseRoot(bytes);
        root.Descendants("DatumOstvarivanjaPrihoda").Single().Value.Should().Be("2025-03-15");
    }

    [Fact]
    public void Serialize_FilingDeadline_FormattedAsYyyyMmDd()
    {
        var filing = MakeFiling(deadline: new DateOnly(2025, 4, 30));
        var profile = MakeProfile();

        var bytes = _sut.Serialize(filing, profile, string.Empty);

        var root = ParseRoot(bytes);
        root.Descendants("DatumDospelostiObaveze").Single().Value.Should().Be("2025-04-30");
    }

    [Fact]
    public void Serialize_ObracunskiPeriod_IsYyyyMm()
    {
        var filing = MakeFiling(incomeDate: new DateOnly(2025, 3, 15));
        var profile = MakeProfile();

        var bytes = _sut.Serialize(filing, profile, string.Empty);

        var root = ParseRoot(bytes);
        root.Descendants("ObracunskiPeriod").Single().Value.Should().Be("2025-03");
    }

    [Fact]
    public void Serialize_FullName_WrappedInCdata()
    {
        var filing = MakeFiling();
        var profile = MakeProfile(fullName: "Jovan Jovanovic & Co");

        var bytes = _sut.Serialize(filing, profile, string.Empty);

        // CDATA round-trips to same text content when parsed; verify the raw bytes contain CDATA
        var xml = Encoding.UTF8.GetString(bytes);
        xml.Should().Contain("<![CDATA[Jovan Jovanovic & Co]]>");
    }

    [Fact]
    public void Serialize_Address_WrappedInCdata()
    {
        var filing = MakeFiling();
        var profile = MakeProfile(address: "Knez Mihailova 1 <Belgrade>");

        var bytes = _sut.Serialize(filing, profile, string.Empty);

        var xml = Encoding.UTF8.GetString(bytes);
        xml.Should().Contain("<![CDATA[Knez Mihailova 1 <Belgrade>]]>");
    }

    [Fact]
    public void Serialize_NullPhoneNumber_EmptyElement()
    {
        var filing = MakeFiling();
        var profile = MakeProfile(phone: null);

        var bytes = _sut.Serialize(filing, profile, string.Empty);

        var root = ParseRoot(bytes);
        root.Descendants("Telefon").Single().Value.Should().BeEmpty();
    }

    [Fact]
    public void Serialize_GrossTaxPayableRsd_MapsToOsnovicaAndObracunati()
    {
        var filing = MakeFiling(grossTaxPayable: 999.99m);
        var profile = MakeProfile();

        var bytes = _sut.Serialize(filing, profile, string.Empty);

        var root = ParseRoot(bytes);
        root.Descendants("OsnovicaZaPorez").Single().Value.Should().Be("999.99");
        root.Descendants("ObracunatiPorez").Single().Value.Should().Be("999.99");
    }

    [Fact]
    public void Serialize_Output_IsUtf8WithoutBom()
    {
        var filing = MakeFiling();
        var profile = MakeProfile();

        var bytes = _sut.Serialize(filing, profile, string.Empty);

        // UTF-8 BOM is EF BB BF — must not be present
        bytes[0].Should().NotBe(0xEF);

        // Must begin with the XML declaration or '<'
        var xml = Encoding.UTF8.GetString(bytes);
        xml.Should().StartWith("<?xml");
    }

    [Fact]
    public void Serialize_PaymentNotes_AppearsInOstalo()
    {
        var filing = MakeFiling();
        var profile = MakeProfile();

        var bytes = _sut.Serialize(filing, profile, "IBAN: RS12345 REF: 12345");

        var root = ParseRoot(bytes);
        root.Descendants("Ostalo").Single().Value.Should().Be("IBAN: RS12345 REF: 12345");
    }

    [Fact]
    public void Serialize_NacinIsplate_AlwaysThree()
    {
        var filing = MakeFiling();
        var profile = MakeProfile();

        var bytes = _sut.Serialize(filing, profile, string.Empty);

        var root = ParseRoot(bytes);
        root.Descendants("NacinIsplate").Single().Value.Should().Be("3");
    }

    [Fact]
    public void Serialize_VrstaPrijave_AlwaysOne()
    {
        var filing = MakeFiling();
        var profile = MakeProfile();

        var bytes = _sut.Serialize(filing, profile, string.Empty);

        var root = ParseRoot(bytes);
        root.Descendants("VrstaPrijave").Single().Value.Should().Be("1");
    }
}
