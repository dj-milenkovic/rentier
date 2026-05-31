using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;

namespace Rentier.Tests.Common.Builders;

/// <summary>
/// Builds realistic, deterministic entity graphs for migration and integration tests.
/// All monetary values use decimal. All dates use DateOnly.
/// Entities are built using only public domain APIs (constructors + factory methods).
/// </summary>
public static class SeedDataBuilder
{
    // ── Deterministic IDs (for entities whose constructors accept an ID) ────

    public static readonly Guid PrimaryProfileId   = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    public static readonly Guid SecondaryProfileId = new("b2c3d4e5-f6a7-8901-bcde-f12345678901");
    public static readonly Guid MailboxId          = new("c3d4e5f6-a7b8-9012-cdef-123456789012");

    // ── TaxpayerProfiles ────────────────────────────────────────────────────

    public static TaxpayerProfile PrimaryProfile() =>
        new(PrimaryProfileId, "1234567890123", "Petar Petrović",
            "Bulevar Kralja Aleksandra 123, Beograd", "11001",
            phoneNumber: "+381111234567", email: "petar@example.com");

    public static TaxpayerProfile SecondaryProfile() =>
        new(SecondaryProfileId, "9876543210987", "Marija Marković",
            "Knez Mihailova 45, Beograd", "11000");

    // ── Holidays ────────────────────────────────────────────────────────────

    public static IReadOnlyList<PublicHoliday> SerbianHolidays2024() =>
    [
        PublicHoliday.Create(new DateOnly(2024,  1,  1), "Nova Godina"),
        PublicHoliday.Create(new DateOnly(2024,  1,  2), "Nova Godina (drugi dan)"),
        PublicHoliday.Create(new DateOnly(2024,  1,  7), "Božić"),
        PublicHoliday.Create(new DateOnly(2024,  2, 15), "Dan državnosti"),
        PublicHoliday.Create(new DateOnly(2024,  2, 16), "Dan državnosti (drugi dan)"),
        PublicHoliday.Create(new DateOnly(2024,  5,  1), "Praznik rada"),
        PublicHoliday.Create(new DateOnly(2024,  5,  2), "Praznik rada (drugi dan)"),
        PublicHoliday.Create(new DateOnly(2024, 11, 11), "Dan primirja"),
    ];

    public static HolidayYearRange HolidayYearRange2024To2026() => new(2024, 2026);

    // ── Mailbox ─────────────────────────────────────────────────────────────

    public static Mailbox Mailbox() =>
        new(MailboxId, "imap.example.com", 993, "user@example.com",
            new MailboxCursor.SyncedTo(new DateOnly(2024, 1, 1), 42L));

    // ── Importers ───────────────────────────────────────────────────────────

    /// <summary>Creates a fully configured importer linked to a profile and mailbox.</summary>
    public static Importer LinkedImporter(Guid taxpayerProfileId, Guid mailboxId)
    {
        var imp = Importer.Create("IBKR Primary Account");
        imp.UpdateDetails("IBKR Primary Account", ReportType.IbkrCsv,
            taxpayerProfileId, mailboxId,
            "no-reply@ibkr.com", "Activity Statement",
            @"U\d{8}\.csv", "Standard payment notes");
        return imp;
    }

    /// <summary>Creates an importer with no FK links (standalone manual import).</summary>
    public static Importer StandaloneImporter()
    {
        var imp = Importer.Create("Manual Import");
        imp.UpdateDetails("Manual Import", ReportType.IbkrCsv,
            taxpayerProfileId: null, mailboxId: null,
            fromFilter: "", subjectFilter: "", attachmentRegex: "", paymentNotes: "");
        return imp;
    }

    // ── ExchangeRates ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns EUR/USD/CHF rates for 10 consecutive days with 6-decimal-place values
    /// chosen to expose floating-point precision issues.
    /// </summary>
    public static IReadOnlyList<ExchangeRate> ExchangeRates()
    {
        var rates = new List<ExchangeRate>();
        var currencies = new[] { ("EUR", 117.123456m), ("USD", 108.654321m), ("CHF", 122.987654m) };
        var startDate = new DateOnly(2024, 3, 1);
        for (var i = 0; i < 10; i++)
        {
            foreach (var (ccy, baseRate) in currencies)
                rates.Add(new ExchangeRate(startDate.AddDays(i), ccy, baseRate + i * 0.001234m));
        }
        return rates;
    }

    // ── Reports ──────────────────────────────────────────────────────────────

    public static IReadOnlyList<Report> Reports(Guid importerId)
    {
        var content = "Id,ActivityCode,Symbol,Date,Description,Amount,Currency\n"u8.ToArray();
        return
        [
            Report.Create(importerId, "U12345678_20240301.csv", content, 1001,
                emailDate: new DateOnly(2024, 3, 1)),
            Report.Create(importerId, "U12345678_20240401.csv", content, 1002,
                emailDate: new DateOnly(2024, 4, 1)),
            Report.Create(importerId, "U12345678_20240501.csv", content, 1003,
                emailDate: null),
        ];
    }

    // ── Filings ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns filings across both taxpayer profiles covering all status values and
    /// decimal amounts chosen to detect precision corruption after schema migration.
    /// </summary>
    public static IReadOnlyList<Filing> Filings(Guid primaryProfileId, Guid secondaryProfileId)
    {
        var incomeDate = new DateOnly(2024, 3, 15);
        return
        [
            Filing.CreateFromIncome(primaryProfileId, IncomeType.Dividend,
                "Apple Inc", incomeDate,
                grossIncomeRsd: 123456.78m, whtPaidRsd: 18518.52m,
                grossTaxPayableRsd: 18518.52m, taxPayableRsd: 0m,
                filingDeadline: incomeDate.AddDays(30)),

            FiledFiling(primaryProfileId, "Microsoft Corp",
                new DateOnly(2024, 4, 10), 87654.32m),

            PaidFiling(primaryProfileId, "Alphabet Inc",
                new DateOnly(2024, 5, 20), 56789.01m),

            Filing.CreateFromIncome(secondaryProfileId, IncomeType.Interest,
                "Interactive Brokers LLC", new DateOnly(2024, 6, 1),
                grossIncomeRsd: 9876.54m, whtPaidRsd: 0m,
                grossTaxPayableRsd: 1481.48m, taxPayableRsd: 1481.48m,
                filingDeadline: new DateOnly(2024, 7, 1)),

            FilingWithReference(primaryProfileId, "Amazon.com Inc",
                new DateOnly(2024, 7, 5), 44321.67m, "97-123456789-12"),

            Filing.CreateFromIncome(secondaryProfileId, IncomeType.Dividend,
                "NIS AD Novi Sad", new DateOnly(2024, 8, 15),
                grossIncomeRsd: 33000.00m, whtPaidRsd: 0m,
                grossTaxPayableRsd: 4950.00m, taxPayableRsd: 4950.00m,
                filingDeadline: new DateOnly(2024, 9, 14)),
        ];
    }

    /// <summary>
    /// The exact GrossIncomeRsd values seeded by <see cref="Filings"/>, sorted ascending.
    /// Used by upgrade tests to verify no data loss or precision corruption.
    /// </summary>
    public static IReadOnlyList<decimal> KnownGrossAmountsSorted() =>
    [
        9876.54m, 33000.00m, 44321.67m, 56789.01m, 87654.32m, 123456.78m,
    ];

    // ── UserPreferences ──────────────────────────────────────────────────────

    public static IReadOnlyList<UserPreference> UserPreferences() =>
    [
        new("Language", "sr-Latn"),
        new("Theme", "Dark"),
        new("LastOpenedTab", "Filings"),
    ];

    // ── Private helpers ──────────────────────────────────────────────────────

    private static Filing FiledFiling(Guid profileId, string entity, DateOnly date, decimal gross)
    {
        var filing = Filing.CreateFromIncome(profileId, IncomeType.Dividend, entity, date,
            gross, gross * 0.15m, gross * 0.15m, 0m, date.AddDays(30));
        filing.AdvanceStatus(FilingStatus.Filed);
        return filing;
    }

    private static Filing PaidFiling(Guid profileId, string entity, DateOnly date, decimal gross)
    {
        var filing = Filing.CreateFromIncome(profileId, IncomeType.Dividend, entity, date,
            gross, gross * 0.15m, gross * 0.15m, 0m, date.AddDays(30));
        filing.AdvanceStatus(FilingStatus.Filed);
        filing.AdvanceStatus(FilingStatus.Paid);
        return filing;
    }

    private static Filing FilingWithReference(
        Guid profileId, string entity, DateOnly date, decimal gross, string reference)
    {
        var filing = Filing.CreateFromIncome(profileId, IncomeType.Dividend, entity, date,
            gross, gross * 0.15m, gross * 0.15m, 0m, date.AddDays(30));
        filing.SetPaymentReference(reference);
        return filing;
    }
}
