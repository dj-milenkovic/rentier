using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Rentier.Application.Interfaces;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;

namespace Rentier.Infrastructure.Serialization;

public sealed class PpOpoXmlSerializer : IXmlFilingSerializer
{
    public byte[] Serialize(Filing filing, TaxpayerProfile profile, string paymentNotes)
    {
        var sifra = MapIncomeType(filing.IncomeType);

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("PodaciOPrijavi",
                new XElement("VrstaPrijave", "1"),
                new XElement("ObracunskiPeriod", filing.IncomeDate.ToString("yyyy-MM")),
                new XElement("DatumOstvarivanjaPrihoda", filing.IncomeDate.ToString("yyyy-MM-dd")),
                new XElement("DatumDospelostiObaveze", filing.FilingDeadline.ToString("yyyy-MM-dd")),
                new XElement("PodaciOPoreskomObvezniku",
                    new XElement("JMBG", profile.Jmbg),
                    new XElement("Ime", new XCData(profile.FullName)),
                    new XElement("Adresa", new XCData(profile.Address)),
                    new XElement("SifraOpstine", profile.OpstinaCode),
                    new XElement("Telefon", profile.PhoneNumber ?? string.Empty),
                    new XElement("Email", profile.Email ?? string.Empty)),
                new XElement("PodaciONacinuOstvarivanjaPrihoda",
                    new XElement("NacinIsplate", "3"),
                    new XElement("Ostalo", paymentNotes ?? string.Empty)),
                new XElement("DeklarisaniPodaciOVrstamaPrihoda",
                    new XElement("SifraVrstePrihoda", sifra),
                    new XElement("BrutoPrihod", Fmt(filing.GrossIncomeRsd)),
                    new XElement("OsnovicaZaPorez", Fmt(filing.GrossTaxPayableRsd)),
                    new XElement("ObracunatiPorez", Fmt(filing.GrossTaxPayableRsd)),
                    new XElement("PorezPlacenDrugojDrzavi", Fmt(filing.WhtPaidRsd)),
                    new XElement("PorezZaUplatu", Fmt(filing.TaxPayableRsd)))));

        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        doc.Save(writer);
        writer.Flush();
        return ms.ToArray();
    }

    private static string Fmt(decimal d) =>
        d.ToString("F2", CultureInfo.InvariantCulture);

    private static string MapIncomeType(IncomeType t) => t switch
    {
        IncomeType.Interest => "111401000",
        IncomeType.Dividend => "111402000",
        _ => throw new ArgumentOutOfRangeException(nameof(t), t, null)
    };
}
