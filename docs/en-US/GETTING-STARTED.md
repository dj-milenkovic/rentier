# Getting Started with Rentier

This guide walks you through installing Rentier, configuring your taxpayer profile, and importing your first IBKR Activity Statement.

---

## Prerequisites

| Requirement | Details |
|---|---|
| Operating system | Windows 10, Ubuntu 20.04, or macOS 12 (or later) |
| IBKR account | With dividends or interest activity to report |
| Serbian JMBG | Your 13-digit personal identification number |

---

## Step 1 — Download and Install

Download Rentier from the project's [GitHub Releases](https://github.com/dj-milenkovic/rentier/releases) page. Every package includes everything Rentier needs to run, so you do not need to install .NET.

| Operating system | File | How to install |
|---|---|---|
| Windows (installer) | `Rentier-<version>-win-x64-setup.exe` | Run it and follow the prompts. It installs Rentier for your account only (by default in `%LocalAppData%\Programs\Rentier`) and needs no administrator rights. |
| Windows (portable) | `Rentier-<version>-win-x64.zip` | Extract it into a new, empty folder that you can write to (for example in your user folder) and start `Rentier.exe`. No setup program runs. |
| macOS, Apple silicon (M1 and later) | `Rentier-<version>-osx-arm64.dmg` | Open the DMG and drag `Rentier.app` into **Applications**. Start it from Applications, not from the DMG. |
| macOS, Intel | `Rentier-<version>-osx-x64.dmg` | As above. |
| Linux x64 | `Rentier-<version>-linux-x64.AppImage` | Make it executable (`chmod +x Rentier-<version>-linux-x64.AppImage`) and run it. Keep it in a folder you can write to. It may need `libfuse2`, and it needs ICU (`libicu`), which most distributions already include. |

If you prefer not to run an installer, use the Windows ZIP, the macOS DMG or the Linux AppImage: none of them runs a setup program, and they update themselves just like the installer.

### Updates

The packages above update themselves. Packages of **v1.3.0 and earlier**, and the **v1.3.1** packages without `-velopack` in their name, **do not**: see [Switching from a package that does not update itself](#switching-from-a-package-that-does-not-update-itself) below.

Each time Rentier starts, it checks the Releases page for a newer version. If there is one, a bar appears at the top of the window:

1. **"Update v<version> available"**: choose **Update Now** to download it, or **Later** to hide the bar. Rentier asks again the next time it starts.
2. **"Downloading update... <n>%"** shows the download progress.
3. **"Update ready. Restart to apply."**: choose **Restart Now** to close Rentier, install the update and start it again, or **Later** to keep working. If you choose Later, the update is installed the next time you start Rentier.

If the download or the installation fails, a red bar shows **"Update failed: <reason>"** with **Retry** (try the download again) and **Dismiss**. If Rentier cannot reach the Releases page, for example when you are offline, it shows no bar and simply checks again on the next start.

### Switching from a package that does not update itself

Packages of v1.3.0 and earlier, and the v1.3.1 packages without `-velopack` in their name, cannot update themselves. Switch once; after that Rentier updates from inside the app. If you installed v1.3.1 from a `-velopack` package, there is nothing to do.

| Operating system | What to do |
|---|---|
| Windows, installed | Run `Rentier-<version>-win-x64-setup.exe` once. If Rentier was installed for your account, the installer upgrades it in place. If it was installed **for all users**, the installer tells you so and stops: uninstall Rentier in **Settings → Apps** first, then run the installer again. |
| Windows, portable ZIP | Extract the new ZIP into a new, empty folder and start `Rentier.exe`. Then delete the old folder and any shortcuts to its `Rentier.Desktop.exe`. |
| macOS | Open the new DMG and drag `Rentier.app` into **Applications**, replacing the old one. |
| Linux | Use the AppImage instead of the `.deb` or `.tar.gz` package. If you installed the `.deb`, remove it with `sudo apt remove rentier` so the old menu entry no longer starts the old version. |

**Your data is not affected.** Rentier keeps its database, settings and logs in your user profile, not in the program folder: `%LocalAppData%\Rentier` on Windows, `~/Library/Application Support/Rentier` on macOS, and `~/.local/share/Rentier` on Linux. Switching packages, updating or uninstalling the program leaves that folder alone.

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

### Diagnostic logs

Rentier writes one log file per day (for example `rentier-20260115.log`) and deletes logs older than 30 days. Logs record what the app did and any errors, with timestamps. Rentier avoids writing personal or financial data to them (no names, email addresses, account numbers or amounts), but skim a log before sharing it.

- **Find them:** open **Settings → Diagnostics → Open logs folder**. On Windows the folder is `%LOCALAPPDATA%\Rentier\logs`, on macOS `~/Library/Application Support/Rentier/logs`, and on Linux `~/.local/share/Rentier/logs`.
- **More detail:** if you are asked to reproduce a problem, turn on **Detailed (debug) logging** on the same page. It takes effect immediately and is remembered until you turn it off.
- **Report a problem:** attach the log for the day it happened to your [GitHub issue](https://github.com/dj-milenkovic/rentier/issues).

---

## Next Steps

- [IBKR Activity Statement Setup](IBKR-SETUP.md) — detailed instructions for generating the right CSV
- [Serbian PP-OPO Tax Overview](TAX-OVERVIEW.md) — understand what Rentier calculates and why
