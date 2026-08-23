# Serbian PP-OPO Tax Overview

This document explains the Serbian passive income tax rules that Rentier applies. It is provided for informational purposes only.

> **Disclaimer:** This is not tax advice. Serbian tax law is subject to change. Always verify your obligations with a licensed Serbian tax advisor or the Tax Administration (Poreska uprava).

---

## What Is PP-OPO?

**PP-OPO** is the tax filing used by Serbian residents to self-report and pay tax on passive income received from abroad. The abbreviation refers to "Poreska prijava za porez po odbitku" — the document filed by the income recipient when the foreign payer (e.g. a broker) does not calculate and withhold Serbian tax.

This filing applies when Serbian residents receive:
- **Dividends** from foreign companies (e.g. via Interactive Brokers)
- **Interest income** from foreign financial institutions

Every individual payment — whether a dividend or an interest credit — requires a separate PP-OPO filing.

---

## Income Types Rentier Handles

| Income type | IBKR CSV section | Serbian tax treatment |
|---|---|---|
| **Dividends** | `Dividends` | Taxable at 15%; foreign WHT reduces the Serbian tax due |
| **Credit interest** | `Interest` (Credit Interest rows) | Taxable at 15%; foreign WHT may apply |

> **Debit interest** (interest you pay to IBKR on margin, for example) is imported from the statement but does not create a tax filing — it is not taxable income. Confirm this with your tax advisor.

---

## Tax Rate

The Serbian tax on passive income from capital is **15%** of the gross amount, converted to Serbian dinars (RSD).

```
Serbian tax = gross income (RSD) × 15%
```

---

## Foreign Withholding Tax Credit (WHT)

Most countries withhold a **withholding tax (WHT)** at source before the funds reach your account. Serbia lets you credit this foreign tax against your domestic tax liability, so you don't pay tax twice on the same income.

Credit formula:

```
Serbian tax due = max(gross Serbian tax − WHT in RSD, 0)
```

- The credit cannot exceed the computed Serbian tax.
- If the foreign rate is ≥ 15%, no additional Serbian tax is due.
- If it is lower (e.g. 10% on US dividends under the Serbia–US treaty), you pay the difference in Serbia.

**Example:**

| Item | Amount |
|---|---|
| Gross dividend | $100 USD |
| NBS rate (USD→RSD) | 108.50 |
| Gross income | 10,850 RSD |
| Serbian tax (15%) | 1,627.50 RSD |
| Foreign WHT (10%) | 1,085 RSD |
| **Serbian tax due** | **542.50 RSD** |

---

## Exchange Rates

All amounts are reported in **RSD**. Rentier uses the **NBS middle rate** for the income date.

- If a rate is not published for that day (weekend/holiday), the last prior business day's rate is used.
- Rates are cached in the local SQLite database for efficiency.
- IBKR's own rates (`Base Currency Exchange Rate`) are used only as a fallback for currencies NBS doesn't cover.

---

## Filing Deadline

PP-OPO must be filed within **30 days** of the income date. If the deadline falls on a non-business day or a holiday, it shifts to the next business day.

**Example:**

| Income date | Deadline (+30) | Adjusted deadline |
|---|---|---|
| 2024-03-01 | 2024-03-31 (Sunday) | 2024-04-01 (Monday) |
| 2024-04-15 | 2024-05-15 (Wednesday) | 2024-05-15 |
| 2024-04-30 | 2024-05-30 (Thursday) | 2024-05-31 (Friday, holiday) |

---

## Serbian Public Holidays

Rentier uses a configured holiday calendar (`HolidayConf`). Standard holidays:

| Date | Holiday |
|---|---|
| January 1–2 | New Year |
| January 7 | Orthodox Christmas |
| February 15–16 | Statehood Day |
| May 1–2 | Labour Day |
| November 11 | Armistice Day |

> Orthodox Easter is a moveable holiday and must be updated in the configuration every year. Check the official calendar at [www.gov.rs](https://www.gov.rs).

---

## Filing Lifecycle

Each income event goes through the following stages:

```
Init → Filed → Paid
```

| Status | Meaning |
|---|---|
| **Init** | Rentier has calculated the filing; the XML is ready for export |
| **Filed** | The XML has been submitted to the ePorezi portal |
| **Paid** | The tax has been paid |

---

## PP-OPO Submission

Rentier generates an XML file that conforms to the schema published by the Serbian Tax Administration (`http://pid.purs.gov.rs`). To submit:

1. Export the XML (**Export PP-OPO XML**).
2. Log in to [ePorezi](https://www.purs.gov.rs/e-porezi.html).
3. Select **PP-OPO → Nova prijava** (New filing).
4. Upload the XML.
5. Confirm and submit.

Each income event requires its own separate submission.

---

## Frequently Asked Questions

**Do I have to file PP-OPO if the foreign WHT is already ≥ 15%?**
Yes, filing is mandatory even for informational purposes.

**What if I received dividends in multiple currencies?**
Each one is converted independently at the NBS rate.

**What if NBS doesn't have a rate for a currency?**
The IBKR-provided rate is used instead; if that's not available either, the filing reports an error and the rate must be entered manually.

**Is there a Serbia–US tax treaty?**
Yes, the rate is typically 10% (instead of 30%), which is credited in Serbia.

**What if I miss a filing deadline?**
Rentier does not block late filing; penalties are determined by the Tax Administration.

**What if my broker corrects a dividend?**
Rentier detects the correction and flags it in the log; the filing must be adjusted manually. Duplicates are ignored.

---

## Further Reading

- [Serbian Tax Administration — PP-OPO form](https://www.purs.gov.rs) (search for "PP-OPO")
- [ePorezi portal](https://www.purs.gov.rs/e-porezi.html)
- [NBS exchange rates](https://www.nbs.rs/kurs-liste/kursna-lista)
- [Serbia–US Double Taxation Treaty](https://www.mfin.gov.rs) — search bilateral treaties

---

## See Also

- [Getting Started](GETTING-STARTED.md) — set up Rentier and run your first import
- [IBKR Activity Statement Setup](IBKR-SETUP.md) — generate the right CSV from Interactive Brokers
