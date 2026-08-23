# Getting Started with Rentier

This guide walks you through installing Rentier, configuring your taxpayer profile, and importing your first IBKR Activity Statement.

---

## Prerequisites

| Requirement | Details |
|---|---|
| Operating system | Windows 10, Ubuntu 20.04, or macOS 12 (or later) |
| .NET Runtime | [.NET 10.0](https://dotnet.microsoft.com/download) |
| IBKR account | With dividends or interest activity to report |
| Serbian JMBG | Your 13-digit personal identification number |

---

## Step 1 — Download and Install

Rentier is downloaded from the project's [GitHub Releases](https://github.com/dj-milenkovic/rentier/releases) page, where both installer and portable builds are available for Windows, macOS, and Linux.

1. Open the Releases page and download the package for your operating system.
2. For the installer build, run the installer and follow the prompts.
3. For the portable build, extract the archive to your preferred location and run the executable directly — no installation required.

---

## Step 2 — Create Your Taxpayer Profile

On first launch, open **Settings → Profile** and fill in:

| Field | Description | Example |
|---|---|---|
| JMBG | 13-digit unique citizen number | `0101990710125` |
| Full name | As it appears on your tax ID | `Petar Petrović` |
| Address | Street and number | `Knez Mihailova 1` |
| Municipality code | Your Opstina code from the official table | `70092` |
| Phone (optional) | Contact number for the filing form | `+381 11 1234567` |
| Email (optional) | Contact email for the filing form | `petar@example.com` |

> **Municipality codes** are published by the Serbian Tax Administration. Search for "šifrarnik opština" to find the code for your municipality.

---

## Step 3 — Configure an Importer

An **Importer** links a statement source to your taxpayer profile. Go to **Importers → Add New**:

| Field | Description |
|---|---|
| Display name | A friendly label, e.g. `IBKR – Dividends 2024` |
| Report type | Select **IBKR CSV** |
| Taxpayer profile | Select the profile you created in Step 2 |
| Mailbox (optional) | Link an IMAP mailbox for email automation (see Step 5) |

### Email filter fields (optional — required only for email automation)

| Field | How it works | Example |
|---|---|---|
| From filter | Substring match on sender address | `interactivebrokers.com` |
| Subject filter | Substring match on email subject | `Activity Statement` |
| Attachment regex | Regular expression matched against the attachment filename | `.*\.csv` |

> **Important:** If you enable email automation, the **Attachment regex** field must not be empty. Without it, no attachments will be imported. A safe default is `.*\.csv` to accept any CSV file.

---

## Step 4 — Import a Statement Manually

If you do not want email automation, you can upload a statement directly:

1. Export an **Activity Statement CSV** from IBKR — see [IBKR Activity Statement Setup](IBKR-SETUP.md) for exact steps.
2. In Rentier, open **Reports → [your report] → Import...**.
3. Select the CSV file you downloaded.
4. Rentier will parse the file, fetch NBS exchange rates for each income date, and create individual **Filings**.

---

## Step 5 — Configure Email Automation (Optional)

Rentier can monitor an IMAP mailbox and import new IBKR statements automatically.

### 5a — Add a Mailbox

Go to **Settings → Mailboxes → Add New** and enter:

| Field | Description | Typical IMAP value |
|---|---|---|
| Host | IMAP server address | `imap.gmail.com` |
| Port | IMAP SSL port | `993` |
| Username / Email | Your email address | `you@gmail.com` |
| Password | Your app password | xxxxxxxx |

Credentials are stored exclusively in the operating system's Credential Manager, never in the application database.

> **App passwords:** If your email provider uses two-factor authentication (Gmail, Outlook, etc.), you must generate an **app password** instead of using your account password. Check your provider's documentation for instructions.

### 5b — Link the Mailbox to Your Importer

Edit your Importer and set the **Mailbox** field to the mailbox you just created. Make sure all three filter fields are filled in.

### 5c — Run a Sync

Go to **Sync → Run Sync**. Rentier connects to the mailbox, searches for emails matching your filters, downloads matching CSV attachments, and queues them as reports for processing.

The sync cursor advances after each successful run, so subsequent syncs only consider new messages.

---

## Step 6 — Review Filings

After a report is processed, Rentier creates one **Filing** per income event. Go to **Filings** to review them:

| Column | Meaning |
|---|---|
| Status | Init / Filed / Paid |
| Income type | Dividend or Interest |
| Payer | Stock ticker or institution name |
| Filing deadline | 30 calendar days after the income date, shifted to the next business day if it falls on a non-business day |
| Tax payable (RSD) | Serbian tax liability after the withholding tax credit |
| Payment reference | Filing identification number from the ePorezi portal |

---

## Step 7 — Export and Submit

For each filing:

1. Click **Export PP-OPO XML** to generate the submission file.
2. Log in to the [ePorezi portal](https://www.purs.gov.rs/e-porezi.html) of the Serbian Tax Administration.
3. Upload the XML file under **PP-OPO → New Filing**.
4. After submitting, return to Rentier and click **Mark as Filed** on the corresponding filing.
5. Once the tax is paid, click **Mark as Paid**.

> A filing advances through a strictly defined status order: **Init → Filed → Paid**. Steps cannot be skipped.

---

## Common Issues

| Symptom | Likely cause | Fix |
|---|---|---|
| No filings created after processing | CSV does not contain the `Dividends` or `Interest` sections | Check the [IBKR setup guide](IBKR-SETUP.md) and make sure those sections are included |
| WHT amount shows 0 even though tax was withheld | The `Withholding Tax` section is missing from your CSV | Re-export the report with that section enabled |
| Exchange rate not found | NBS did not publish a rate for that date (holiday/weekend) | Rentier automatically falls back to the last prior business day; if the problem persists, check your internet connection |
| Mailbox sync imports 0 reports | Attachment regex is empty, or filters are too strict | Verify all three importer filter fields; test with `.*\.csv` as the attachment regex |
| "IMAP sync failed" error | Wrong credentials, wrong port, or an app password is needed | Re-enter your credentials and use port 993 with SSL |

---

## Next Steps

- [IBKR Activity Statement Setup](IBKR-SETUP.md) — detailed instructions for generating the right CSV
- [Serbian PP-OPO Tax Overview](TAX-OVERVIEW.md) — understand what Rentier calculates and why
