using System.Globalization;
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
    private static readonly XNamespace Ns1 = "http://pid.purs.gov.rs";

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

    // ── T005: Root element, namespace, section structure ─────────────────────

    [Fact]
    public void Serialize_RootElement_IsNs1PodaciPoreskeDeklaracije()
    {
        var bytes = _sut.Serialize(MakeFiling(), MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);

        root.Name.LocalName.Should().Be("PodaciPoreskeDeklaracije");
        root.Name.Namespace.NamespaceName.Should().Be("http://pid.purs.gov.rs");
    }

    [Fact]
    public void Serialize_RootElement_HasXmlnsNs1Attribute()
    {
        var bytes = _sut.Serialize(MakeFiling(), MakeProfile(), string.Empty);
        var xml = Encoding.UTF8.GetString(bytes);

        xml.Should().Contain("xmlns:ns1=\"http://pid.purs.gov.rs\"");
    }

    [Fact]
    public void Serialize_AllTopLevelChildren_HaveNs1Prefix()
    {
        var bytes = _sut.Serialize(MakeFiling(), MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);

        foreach (var child in root.Elements())
        {
            child.Name.Namespace.Should().Be(Ns1,
                $"element <{child.Name.LocalName}> must use the ns1 namespace");
        }
    }

    [Fact]
    public void Serialize_PodaciOPrijavi_ContainsVrstaPrijaveObracunskiPeriodAndRok()
    {
        var bytes = _sut.Serialize(MakeFiling(incomeDate: new DateOnly(2025, 3, 15)), MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);
        var section = root.Element(Ns1 + "PodaciOPrijavi")!;

        section.Should().NotBeNull();
        section.Element(Ns1 + "VrstaPrijave")!.Value.Should().Be("1");
        section.Element(Ns1 + "ObracunskiPeriod")!.Value.Should().Be("2025-03");
        section.Element(Ns1 + "Rok")!.Value.Should().Be("1");
    }

    [Fact]
    public void Serialize_PodaciOPoreskomObvezniku_ContainsNestedJmbgAndAllFields()
    {
        var profile = MakeProfile(
            jmbg: "1234567890123",
            fullName: "John Doe",
            address: "Main St 1",
            opstinaCode: "110",
            phone: "0611234567",
            email: "john@example.com");
        var bytes = _sut.Serialize(MakeFiling(), profile, string.Empty);
        var root = ParseRoot(bytes);
        var section = root.Element(Ns1 + "PodaciOPoreskomObvezniku")!;

        section.Should().NotBeNull();
        // Nested JMBG
        var pid = section.Element(Ns1 + "PoreskiIdentifikacioniBroj")!;
        pid.Should().NotBeNull();
        pid.Element(Ns1 + "JMBGPodnosiocaPrijave")!.Value.Should().Be("1234567890123");
        // Plain-text fields (no CDATA)
        section.Element(Ns1 + "ImePrezimeObveznika")!.Value.Should().Be("John Doe");
        section.Element(Ns1 + "UlicaBrojPoreskogObveznika")!.Value.Should().Be("Main St 1");
        section.Element(Ns1 + "PrebivalisteOpstina")!.Value.Should().Be("110");
        section.Element(Ns1 + "TelefonKontaktOsobe")!.Value.Should().Be("0611234567");
        section.Element(Ns1 + "ElektronskaPosta")!.Value.Should().Be("john@example.com");
    }

    [Fact]
    public void Serialize_PodaciOPoreskomObvezniku_NoPlainTextCdataInRawXml()
    {
        var profile = MakeProfile(fullName: "Jovan Jovanovic & Co", address: "Knez Mihailova 1 <Belgrade>");
        var bytes = _sut.Serialize(MakeFiling(), profile, string.Empty);
        var xml = Encoding.UTF8.GetString(bytes);

        xml.Should().NotContain("<![CDATA[");
    }

    [Fact]
    public void Serialize_PodaciOVrstamaPrihoda_ContainsRedniBrojAndSifra()
    {
        var bytes = _sut.Serialize(MakeFiling(IncomeType.Dividend), MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);
        var section = root.Element(Ns1 + "PodaciOVrstamaPrihoda")!;

        section.Should().NotBeNull();
        section.Element(Ns1 + "RedniBroj")!.Value.Should().Be("1");
        section.Element(Ns1 + "SifraVrstePrihoda")!.Value.Should().Be("111402000");
    }

    [Fact]
    public void Serialize_UkupnoSection_Exists()
    {
        var bytes = _sut.Serialize(MakeFiling(), MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);

        root.Element(Ns1 + "Ukupno").Should().NotBeNull();
    }

    [Fact]
    public void Serialize_UkupnoSection_ContainsAllMonetaryFields()
    {
        var filing = MakeFiling(grossIncome: 12345.50m, whtPaid: 123.45m, grossTaxPayable: 1234.55m, taxPayable: 1111.10m);
        var bytes = _sut.Serialize(filing, MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);
        var ukupno = root.Element(Ns1 + "Ukupno")!;

        ukupno.Element(Ns1 + "BrutoPrihod")!.Value.Should().Be("12345.50");
        ukupno.Element(Ns1 + "OsnovicaZaPorez")!.Value.Should().Be("12345.50");
        ukupno.Element(Ns1 + "ObracunatiPorez")!.Value.Should().Be("1234.55");
        ukupno.Element(Ns1 + "PorezPlacenDrugojDrzavi")!.Value.Should().Be("123.45");
        ukupno.Element(Ns1 + "PorezZaUplatu")!.Value.Should().Be("1111.10");
    }

    [Fact]
    public void Serialize_KamataSection_ExistsWithZeroValues()
    {
        var bytes = _sut.Serialize(MakeFiling(), MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);
        var kamata = root.Element(Ns1 + "Kamata")!;

        kamata.Should().NotBeNull();
        kamata.Element(Ns1 + "PorezZaUplatu")!.Value.Should().Be("0.00");
        kamata.Element(Ns1 + "DoprinosiZaUplatu")!.Value.Should().Be("0.00");
    }

    [Fact]
    public void Serialize_PodaciODodatnojKamati_ExistsAsEmptyElement()
    {
        var bytes = _sut.Serialize(MakeFiling(), MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);
        var dodKamata = root.Element(Ns1 + "PodaciODodatnojKamati");

        dodKamata.Should().NotBeNull();
        dodKamata!.HasElements.Should().BeFalse();
        dodKamata.Value.Should().BeEmpty();
    }

    // ── T006: Encoding declaration ────────────────────────────────────────────

    [Fact]
    public void Serialize_EncodingDeclaration_IsUppercaseUtf8()
    {
        var bytes = _sut.Serialize(MakeFiling(), MakeProfile(), string.Empty);
        var xml = Encoding.UTF8.GetString(bytes);

        xml.Should().Contain("encoding=\"UTF-8\"");
    }

    [Fact]
    public void Serialize_Output_IsUtf8WithoutBom()
    {
        var bytes = _sut.Serialize(MakeFiling(), MakeProfile(), string.Empty);

        bytes[0].Should().NotBe(0xEF);
        var xml = Encoding.UTF8.GetString(bytes);
        xml.Should().StartWith("<?xml");
    }

    // ── T008: OsnovicaZaPorez maps to GrossIncomeRsd (bug fix) ───────────────

    [Fact]
    public void OsnovicaZaPorezMapsToGrossIncomeNotGrossTax()
    {
        var filing = MakeFiling(grossIncome: 100_000.00m, grossTaxPayable: 15_000.00m);
        var bytes = _sut.Serialize(filing, MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);

        var incomeRow = root.Element(Ns1 + "PodaciOVrstamaPrihoda")!;
        incomeRow.Element(Ns1 + "OsnovicaZaPorez")!.Value
            .Should().Be(100_000.00m.ToString("F2", CultureInfo.InvariantCulture));
        incomeRow.Element(Ns1 + "ObracunatiPorez")!.Value
            .Should().Be(15_000.00m.ToString("F2", CultureInfo.InvariantCulture));

        var ukupno = root.Element(Ns1 + "Ukupno")!;
        ukupno.Element(Ns1 + "OsnovicaZaPorez")!.Value
            .Should().Be(100_000.00m.ToString("F2", CultureInfo.InvariantCulture));
        ukupno.Element(Ns1 + "ObracunatiPorez")!.Value
            .Should().Be(15_000.00m.ToString("F2", CultureInfo.InvariantCulture));
    }

    // ── Legacy / retained tests ───────────────────────────────────────────────

    [Fact]
    public void Serialize_Dividend_SifraIs111402000()
    {
        var bytes = _sut.Serialize(MakeFiling(IncomeType.Dividend), MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);
        root.Descendants(Ns1 + "SifraVrstePrihoda").Single().Value.Should().Be("111402000");
    }

    [Fact]
    public void Serialize_Interest_SifraIs111401000()
    {
        var bytes = _sut.Serialize(MakeFiling(IncomeType.Interest), MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);
        root.Descendants(Ns1 + "SifraVrstePrihoda").Single().Value.Should().Be("111401000");
    }

    [Fact]
    public void Serialize_AllMonetaryFields_FormattedWithTwoDp()
    {
        var filing = MakeFiling(grossIncome: 12345.50m, whtPaid: 123.45m,
            grossTaxPayable: 1234.55m, taxPayable: 1111.10m);
        var bytes = _sut.Serialize(filing, MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);
        var incomeRow = root.Element(Ns1 + "PodaciOVrstamaPrihoda")!;

        incomeRow.Element(Ns1 + "BrutoPrihod")!.Value.Should().Be("12345.50");
        incomeRow.Element(Ns1 + "PorezPlacenDrugojDrzavi")!.Value.Should().Be("123.45");
        // OsnovicaZaPorez now = GrossIncomeRsd per bug fix
        incomeRow.Element(Ns1 + "OsnovicaZaPorez")!.Value.Should().Be("12345.50");
        incomeRow.Element(Ns1 + "ObracunatiPorez")!.Value.Should().Be("1234.55");
        incomeRow.Element(Ns1 + "PorezZaUplatu")!.Value.Should().Be("1111.10");
    }

    [Fact]
    public void Serialize_ZeroAmount_FormattedAs0Dot00()
    {
        var filing = MakeFiling(grossIncome: 0m, whtPaid: 0m, grossTaxPayable: 0m, taxPayable: 0m);
        var bytes = _sut.Serialize(filing, MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);
        root.Descendants(Ns1 + "BrutoPrihod").First().Value.Should().Be("0.00");
    }

    [Fact]
    public void Serialize_IncomeDate_FormattedAsYyyyMmDd()
    {
        var filing = MakeFiling(incomeDate: new DateOnly(2025, 3, 15));
        var bytes = _sut.Serialize(filing, MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);
        root.Descendants(Ns1 + "DatumOstvarivanjaPrihoda").Single().Value.Should().Be("2025-03-15");
    }

    [Fact]
    public void Serialize_FilingDeadline_FormattedAsYyyyMmDd()
    {
        var filing = MakeFiling(deadline: new DateOnly(2025, 4, 30));
        var bytes = _sut.Serialize(filing, MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);
        root.Descendants(Ns1 + "DatumDospelostiObaveze").Single().Value.Should().Be("2025-04-30");
    }

    [Fact]
    public void Serialize_ObracunskiPeriod_IsYyyyMm()
    {
        var filing = MakeFiling(incomeDate: new DateOnly(2025, 3, 15));
        var bytes = _sut.Serialize(filing, MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);
        root.Descendants(Ns1 + "ObracunskiPeriod").Single().Value.Should().Be("2025-03");
    }

    [Fact]
    public void Serialize_NullPhoneNumber_EmptyElement()
    {
        var profile = MakeProfile(phone: null);
        var bytes = _sut.Serialize(MakeFiling(), profile, string.Empty);
        var root = ParseRoot(bytes);
        root.Descendants(Ns1 + "TelefonKontaktOsobe").Single().Value.Should().BeEmpty();
    }

    [Fact]
    public void Serialize_PaymentNotes_AppearsInOstalo()
    {
        var bytes = _sut.Serialize(MakeFiling(), MakeProfile(), "IBAN: RS12345 REF: 12345");
        var root = ParseRoot(bytes);
        root.Descendants(Ns1 + "Ostalo").Single().Value.Should().Be("IBAN: RS12345 REF: 12345");
    }

    [Fact]
    public void Serialize_NacinIsplate_AlwaysThree()
    {
        var bytes = _sut.Serialize(MakeFiling(), MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);
        root.Descendants(Ns1 + "NacinIsplate").Single().Value.Should().Be("3");
    }

    [Fact]
    public void Serialize_VrstaPrijave_AlwaysOne()
    {
        var bytes = _sut.Serialize(MakeFiling(), MakeProfile(), string.Empty);
        var root = ParseRoot(bytes);
        root.Descendants(Ns1 + "VrstaPrijave").Single().Value.Should().Be("1");
    }
}

