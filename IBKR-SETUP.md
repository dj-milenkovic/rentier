# IBKR Activity Statement Setup

This guide explains how to generate the correct CSV export from Interactive Brokers (IBKR) and how to connect it to Rentier — either by uploading it manually or by letting Rentier fetch it automatically from your email inbox.

---

## What Rentier Needs

Rentier parses the **Activity Statement** CSV format exported from IBKR. It looks for four specific sections:

| Section name (in CSV) | What it contains | Required? |
|---|---|---|
| `Dividends` | Gross dividend payments per stock, date, and currency | Yes (if you have dividends) |
| `Withholding Tax` | Foreign tax already deducted from each dividend | Yes (if withholding was applied) |
| `Interest` | Credit interest (earned) and debit interest (charged) | Yes (if you have interest income) |
| `Base Currency Exchange Rate` | IBKR's own FX rates for the period | Recommended as fallback |

> **Only these sections are read.** All other sections in the Activity Statement (trades, corporate actions, cash balances, etc.) are safely ignored.

---

## Option A — Manual Export (Simplest)

### Step 1 — Log in to IBKR Client Portal

Go to [https://www.ibkr.com/portal](https://www.ibkr.com/portal) and sign in.

### Step 2 — Navigate to Statements

In the menu, go to **Performance & Reports → Statements**.

### Step 3 — Generate an Activity Statement

1. Select **Statement type: Activity**.
2. Set the **date range** — typically a full calendar year (January 1 – December 31) to capture all income events.
3. Set **Format: CSV**.
4. Make sure the following sections are **included**:
   - Dividends
   - Withholding Tax
   - Interest
   - Base Currency Exchange Rate (recommended)
5. Click **Run** or **Create Statement**.

> The download button appears once the statement is generated. IBKR may take up to a minute.

### Step 4 — Upload to Rentier

In Rentier, go to **Importers → [your importer] → Upload Statement**, select the CSV file, and click **Process**.

---

## Option B — Email Automation via IBKR Flex Queries

IBKR can automatically email you a statement on a schedule (daily, weekly, monthly). Rentier monitors your inbox and imports attachments automatically.

### Step 1 — Create a Flex Query in IBKR

1. In Client Portal, go to **Performance & Reports → Flex Queries**.
2. Click **Create** and choose **Activity Flex Query**.
3. Give it a descriptive name, e.g. `Rentier Monthly`.
4. Under **Sections**, enable at minimum:
   - **Dividends**
   - **Withholding Tax**
   - **Interest**
   - **Base Currency Exchange Rate**
5. Set **Format: CSV**.
6. Under **Delivery**, choose **Email** and enter the email address that Rentier will monitor.
7. Set your preferred **schedule** (e.g. monthly on the 1st day of the month, covering the previous month).
8. Save the Flex Query.

> IBKR sends the statement from an `@interactivebrokers.com` address. The subject typically contains "Flex Statement" or "Activity Statement" and the attachment is a `.csv` file.

### Step 2 — Configure a Mailbox in Rentier

See [Getting Started — Step 5](GETTING-STARTED.md#step-5--configure-email-automation-optional) for full mailbox setup instructions.

### Step 3 — Configure Importer Filters

Edit your Importer and set the following filter fields to match the emails IBKR sends:

| Filter field | Recommended value | Notes |
|---|---|---|
| From filter | `interactivebrokers.com` | Substring match on sender; narrows results to IBKR emails |
| Subject filter | `Flex Statement` or `Activity Statement` | Substring match on subject; adjust if your Flex Query uses a custom subject |
| Attachment regex | `.*\.csv` | Regex matched against attachment filename; **must not be empty** |

> **The attachment regex is required.** If it is left empty, no attachments will be imported, even if matching emails are found.

### Step 4 — Run a Sync

In Rentier, go to **Sync → Run Now**. After the first successful sync, subsequent syncs only process emails newer than the last imported message.

---

## Understanding the CSV Format

If you want to verify that your file is correct before importing, open it in a text editor. Rentier looks for rows structured like this:

### Dividends
```
Dividends,Header,Currency,Date,Description,Amount,...
Dividends,Data,USD,2024-03-15,"AAPL(US0378331005) Cash Dividend",48.50,...
Dividends,Data,USD,2024-06-15,"AAPL(US0378331005) Cash Dividend",52.00,...
Dividends,Total,USD,...
```

### Withholding Tax
```
Withholding Tax,Header,Currency,Date,Description,Amount,...
Withholding Tax,Data,USD,2024-03-15,"AAPL(US0378331005) Cash Dividend",-7.28,...
```

> **Note:** Withholding tax amounts are **negative** in IBKR's CSV (they represent money deducted). Rentier handles this automatically.

### Interest
```
Interest,Header,Currency,Date,Description,Amount,...
Interest,Data,USD,2024-01-31,USD Credit Interest for Jan-2024,12.45,...
Interest,Data,USD,2024-01-31,USD Debit Interest for Jan-2024,-3.20,...
```

> Rentier only processes rows whose description contains **"Credit Interest"** or **"Debit Interest"**. Other interest descriptions are ignored.

### Base Currency Exchange Rate
```
Base Currency Exchange Rate,Header,FromCurrency,Date,...,ToCurrency,Rate,...
Base Currency Exchange Rate,Data,USD,2024-03-15,...,EUR,0.91723,...
```

---

## What Rentier Creates vs. Ignores

| Input | What Rentier creates |
|---|---|
| A `Dividends,Data` row | A potential filing with income type **Dividend** |
| A matched `Withholding Tax,Data` row | WHT credit applied to the corresponding dividend filing |
| An `Interest,Data` row with "Credit Interest" | A potential filing with income type **Interest** |
| An `Interest,Data` row with "Debit Interest" | Imported but not used for tax filings (debit interest is not taxable income in Serbia — verify with your advisor) |
| Unmatched WHT row (no corresponding dividend) | Logged as a parse warning; no filing created |
| Any other section | Ignored entirely |

---

## Troubleshooting

### "No recognised IBKR sections found in the CSV"
Your file does not contain any of the four expected section names. This usually means:
- You exported the wrong statement type (e.g. Trade Confirmation instead of Activity Statement)
- The file is an XML export rather than CSV — re-export and choose **CSV format**

### "WHT_UNMATCHED — No dividend found for WHT entry"
The withholding tax row references a stock and date for which no dividend was found in the same file. Possible causes:
- The `Dividends` section was not included in the export — re-export with it enabled
- The date range is too narrow and misses the corresponding dividend

### "WHT_CURRENCY_MISMATCH"
The withholding tax was deducted in a different currency than the dividend. This is unusual but can happen with multi-currency accounts. Review the raw CSV and consult your tax advisor.

### Empty import after email sync
Check the following in order:
1. The **Attachment regex** is not empty on the Importer
2. The **From filter** and **Subject filter** match the actual emails IBKR sends
3. The mailbox credentials are correct (try connecting with a standard IMAP client first)
4. IBKR has actually sent a statement — check the email inbox directly

---

## Next Steps

- [Getting Started](GETTING-STARTED.md) — full setup walkthrough including mailbox and profile configuration
- [Serbian PP-OPO Tax Overview](TAX-OVERVIEW.md) — understand the tax rules Rentier applies
