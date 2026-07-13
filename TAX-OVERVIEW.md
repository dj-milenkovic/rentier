# Serbian PP-OPO Tax Overview

This document explains the Serbian passive income tax rules that Rentier applies. It is provided for informational purposes only.

> **Disclaimer:** This is not tax advice. Serbian tax law is subject to change. Always verify your obligations with a licensed Serbian tax advisor or the Tax Administration (Poreska uprava).

---

## What Is PP-OPO?

**PP-OPO** is the Serbian tax form used by individual taxpayers to self-report and pay tax on passive income received from foreign sources. The abbreviation refers to the "Poreska prijava za porez po odbitku" — the withholding tax return filed by the recipient themselves when the paying entity (a foreign broker) does not withhold Serbian tax.

This applies to Serbian tax residents who receive:
- **Dividends** from foreign companies (e.g. via Interactive Brokers)
- **Interest income** from foreign financial institutions

Each income event — every individual dividend payment or interest credit — requires a **separate PP-OPO filing**.

---

## Income Types Rentier Handles

| Income type | IBKR CSV section | Serbian tax treatment |
|---|---|---|
| Dividends | `Dividends` | Taxable at 15%; foreign WHT reduces Serbian tax due |
| Credit interest | `Interest` (Credit Interest rows) | Taxable at 15%; foreign WHT may apply |

> **Debit interest** (interest you pay to IBKR on margin, for example) is imported from the statement but does not create a tax filing — it is not taxable income. Confirm this with your tax advisor.

---

## Tax Rate

The Serbian personal income tax on passive income from capital is **15%** of the gross income amount in Serbian dinars (RSD).

```
Serbian tax = gross income (RSD) × 15%
```

---

## Foreign Withholding Tax Credit

Most countries that pay dividends deduct a **withholding tax (WHT)** at source before the funds reach your account. Serbia allows you to credit this foreign tax against your Serbian tax obligation, so you do not pay tax twice on the same income.

The credit works as follows:

```
Serbian tax due = max(gross Serbian tax − WHT already paid in RSD, 0)
```

**The credit cannot exceed the computed Serbian tax.** If the foreign WHT rate is equal to or higher than 15%, no additional Serbian tax is owed. If it is lower (for example, 10% US withholding on dividends under the US–Serbia tax treaty), you pay the difference to Serbia.

**Example:**

| | Amount |
|---|---|
| Gross dividend received | $100 USD |
| NBS exchange rate (USD→RSD) | 108.50 |
| Gross income in RSD | 10,850 RSD |
| Serbian gross tax (15%) | 1,627.50 RSD |
| Foreign WHT paid (10% × $100 = $10 USD) | 1,085 RSD |
| **Serbian tax due** | **542.50 RSD** |

---

## Exchange Rates

All amounts must be reported in **Serbian dinars (RSD)**. Rentier converts foreign currency amounts using the **Serbian National Bank (NBS) middle exchange rate** for the income date.

- Rentier fetches rates from the NBS website automatically for each income date.
- If the NBS did not publish a rate for that exact date (weekends, holidays), Rentier falls back to the **most recent prior business day** rate.
- Fetched rates are cached in the local SQLite database to avoid repeated network requests.

> IBKR includes its own exchange rates in the Activity Statement (`Base Currency Exchange Rate` section). Rentier uses these as a fallback only, for currencies where NBS does not publish a direct rate. The primary source is always NBS.

---

## Filing Deadline

The PP-OPO form must be filed within **30 calendar days** of the date the income was received.

If the 30th day falls on a weekend or a Serbian public holiday, the deadline shifts to the **next business day**.

**Example:**

| Income date | Raw deadline (+30 days) | Adjusted deadline |
|---|---|---|
| 2024-03-01 (Friday) | 2024-03-31 (Sunday) | 2024-04-01 (Monday) |
| 2024-04-15 (Monday) | 2024-05-15 (Wednesday) | 2024-05-15 (no adjustment needed) |
| 2024-04-30 (Tuesday) | 2024-05-30 (Thursday) | 2024-05-31 (Friday — May 30 is Serbian holiday) |

Rentier calculates this automatically using the configured Serbian public holiday calendar.

---

## Serbian Public Holidays

Rentier uses a configurable holiday calendar (`HolidayConf`) for deadline calculations. The standard Serbian public holidays include:

| Date | Holiday |
|---|---|
| January 1–2 | New Year |
| January 7 | Orthodox Christmas |
| February 15–16 | Statehood Day |
| May 1–2 | Labour Day |
| November 11 | Armistice Day |

> Orthodox Easter (Pravoslavni Uskrs) is a moveable holiday and must be updated in the configuration each year. Check the official calendar at [www.gov.rs](https://www.gov.rs).

---

## Filing Lifecycle

Each income event goes through a three-step lifecycle in Rentier:

```
Init → Filed → Paid
```

| Status | Meaning |
|---|---|
| **Init** | Rentier has calculated the filing; PP-OPO XML can be exported |
| **Filed** | You have submitted the XML to the ePorezi portal |
| **Paid** | You have paid the tax due |

Steps are sequential — you cannot mark a filing as Paid without first marking it as Filed.

---

## PP-OPO Submission

Rentier generates a PP-OPO XML file that conforms to the schema published by the Serbian Tax Administration (`http://pid.purs.gov.rs`). To submit:

1. Export the XML from Rentier (**Export PP-OPO XML** button on any filing).
2. Log in to [ePorezi](https://www.purs.gov.rs/e-porezi.html) — the Serbian Tax Administration online portal.
3. Select **PP-OPO → Nova prijava** (New filing).
4. Upload the XML file.
5. Confirm and submit.

Each income event requires its own separate submission. If you had 20 dividend payments in a year, you submit 20 PP-OPO forms.

---

## Frequently Asked Questions

**Do I have to file PP-OPO if all my foreign tax was withheld at source?**  
Yes — even if the foreign WHT equals or exceeds 15% and no additional Serbian tax is owed, you are still required to file the PP-OPO form for informational purposes. Confirm this with your tax advisor.

**What if I received dividends in multiple currencies?**  
Each dividend is converted independently using the NBS rate for its currency on the income date. Rentier handles multiple currencies automatically.

**What if the NBS rate is not available for a specific currency?**  
Rentier will attempt to fall back to the IBKR-provided exchange rate from the statement. If no rate is available at all, the report will show a processing error and you must enter the rate manually.

**What about US dividends — is there a tax treaty?**  
Yes, Serbia and the United States have a tax treaty. The standard US withholding rate for Serbian residents is typically 10% (reduced from 30%). The treaty rate reduces your Serbian tax due by the withheld amount. Rentier applies the WHT credit mechanically based on what is in your statement — always verify treaty applicability with your tax advisor.

**What if I missed a filing deadline?**  
Rentier calculates deadlines for reference, but does not prevent you from filing late. Contact the Serbian Tax Administration or your tax advisor for guidance on late filing penalties.

**What if my broker corrects a dividend after I already imported it?**  
Brokers occasionally re-issue a dividend at a corrected amount (a reversal plus a re-booked payment in a later statement). Rentier detects this: if an imported statement contains an income event for the same company and payment date as an existing filing but with a different gross amount, no second filing is created. Instead the sync log shows a **"Broker correction detected"** error with both amounts, and you should review and adjust the existing filing manually. Importing the same statement (or an identical income event) twice is safe — exact duplicates are skipped silently.

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
