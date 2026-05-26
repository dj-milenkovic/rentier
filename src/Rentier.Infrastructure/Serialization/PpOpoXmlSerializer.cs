using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;

namespace Rentier.Infrastructure.Serialization;

public sealed class PpOpoXmlSerializer : IXmlFilingSerializer
{
    private static readonly XNamespace Ns1 = "http://pid.purs.gov.rs";

    /// <summary>
    /// Custom UTF-8 encoding that reports "UTF-8" (uppercase) in the XML declaration.
    /// </summary>
    private sealed class UppercaseUtf8Encoding : UTF8Encoding
    {
        internal UppercaseUtf8Encoding() : base(encoderShouldEmitUTF8Identifier: false) { }
        public override string WebName => "UTF-8";
        public override string HeaderName => "UTF-8";
        public override string BodyName => "UTF-8";
    }

    public Result<byte[], Error> Serialize(Filing filing, TaxpayerProfile profile, string paymentNotes)
    {
        var sifra = MapIncomeType(filing.IncomeType);
        if (sifra is null)
            return Result<byte[], Error>.Failure(
                new Error("UNSUPPORTED_INCOME_TYPE",
                    $"Income type '{filing.IncomeType}' is not supported by the PP-OPO serializer."));

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Ns1 + "PodaciPoreskeDeklaracije",
                new XAttribute(XNamespace.Xmlns + "ns1", Ns1),
                new XElement(Ns1 + "PodaciOPrijavi",
                    new XElement(Ns1 + "VrstaPrijave", "1"),
                    new XElement(Ns1 + "ObracunskiPeriod", filing.IncomeDate.ToString("yyyy-MM")),
                    new XElement(Ns1 + "Rok", "1")),
                new XElement(Ns1 + "PodaciOPoreskomObvezniku",
                    new XElement(Ns1 + "PoreskiIdentifikacioniBroj",
                        new XElement(Ns1 + "JMBGPodnosiocaPrijave", profile.Jmbg)),
                    new XElement(Ns1 + "ImePrezimeObveznika", profile.FullName),
                    new XElement(Ns1 + "UlicaBrojPoreskogObveznika", profile.Address),
                    new XElement(Ns1 + "PrebivalisteOpstina", profile.OpstinaCode),
                    new XElement(Ns1 + "TelefonKontaktOsobe", profile.PhoneNumber ?? string.Empty),
                    new XElement(Ns1 + "ElektronskaPosta", profile.Email ?? string.Empty)),
                new XElement(Ns1 + "PodaciONacinuOstvarivanjaPrihoda",
                    new XElement(Ns1 + "NacinIsplate", "3"),
                    new XElement(Ns1 + "Ostalo", paymentNotes ?? string.Empty)),
                new XElement(Ns1 + "PodaciOVrstamaPrihoda",
                    new XElement(Ns1 + "RedniBroj", "1"),
                    new XElement(Ns1 + "SifraVrstePrihoda", sifra),
                    new XElement(Ns1 + "DatumOstvarivanjaPrihoda", filing.IncomeDate.ToString("yyyy-MM-dd")),
                    new XElement(Ns1 + "DatumDospelostiObaveze", filing.FilingDeadline.ToString("yyyy-MM-dd")),
                    new XElement(Ns1 + "BrutoPrihod", Fmt(filing.GrossIncomeRsd)),
                    new XElement(Ns1 + "NormaraniTroskovi", "0.00"),
                    new XElement(Ns1 + "OsnovicaZaPorez", Fmt(filing.GrossIncomeRsd)),
                    new XElement(Ns1 + "ObracunatiPorez", Fmt(filing.GrossTaxPayableRsd)),
                    new XElement(Ns1 + "PorezPlacenDrugojDrzavi", Fmt(filing.WhtPaidRsd)),
                    new XElement(Ns1 + "PorezZaUplatu", Fmt(filing.TaxPayableRsd)),
                    new XElement(Ns1 + "OsnovicaZaDoprinose", "0.00"),
                    new XElement(Ns1 + "ObracunatiDoprinosi", "0.00"),
                    new XElement(Ns1 + "DoprinosiPlaceniDrugojDrzavi", "0.00"),
                    new XElement(Ns1 + "DoprinosiZaUplatu", "0.00")),
                new XElement(Ns1 + "Ukupno",
                    new XElement(Ns1 + "BrutoPrihod", Fmt(filing.GrossIncomeRsd)),
                    new XElement(Ns1 + "NormaraniTroskovi", "0.00"),
                    new XElement(Ns1 + "OsnovicaZaPorez", Fmt(filing.GrossIncomeRsd)),
                    new XElement(Ns1 + "ObracunatiPorez", Fmt(filing.GrossTaxPayableRsd)),
                    new XElement(Ns1 + "PorezPlacenDrugojDrzavi", Fmt(filing.WhtPaidRsd)),
                    new XElement(Ns1 + "PorezZaUplatu", Fmt(filing.TaxPayableRsd)),
                    new XElement(Ns1 + "OsnovicaZaDoprinose", "0.00"),
                    new XElement(Ns1 + "ObracunatiDoprinosi", "0.00"),
                    new XElement(Ns1 + "DoprinosiPlaceniDrugojDrzavi", "0.00"),
                    new XElement(Ns1 + "DoprinosiZaUplatu", "0.00")),
                new XElement(Ns1 + "Kamata",
                    new XElement(Ns1 + "PorezZaUplatu", "0.00"),
                    new XElement(Ns1 + "DoprinosiZaUplatu", "0.00")),
                new XElement(Ns1 + "PodaciODodatnojKamati")));

        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UppercaseUtf8Encoding(),
            OmitXmlDeclaration = false,
            Indent = true,
        };
        using (var writer = XmlWriter.Create(ms, settings))
        {
            doc.Save(writer);
        }
        return Result<byte[], Error>.Success(ms.ToArray());
    }

    private static string Fmt(decimal d) =>
        d.ToString("F2", CultureInfo.InvariantCulture);

    private static string? MapIncomeType(IncomeType t) => t switch
    {
        IncomeType.Interest => "111401000",
        IncomeType.Dividend => "111402000",
        _ => null
    };
}

