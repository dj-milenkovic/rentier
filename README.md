# Rentier

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Active Development](https://img.shields.io/badge/Status-Active%20Development-brightgreen.svg)](#project-status)

**Rentier** is a cross-platform desktop application that helps Serbian taxpayers prepare **PP-OPO tax filings** for passive income (dividends and interest) received through foreign brokers such as Interactive Brokers (IBKR).

It reads your IBKR Activity Statement, fetches NBS exchange rates for each income date, calculates the 15% Serbian income tax, applies any foreign withholding tax credit, and produces a ready-to-submit PP-OPO XML file — one per income event.

> **Disclaimer:** Rentier is a productivity tool, not a licensed tax advisory service. Always verify your filings with a certified Serbian tax advisor.

---

## Documentation

### English
| Guide | Description |
|---|---|
| [Getting Started](docs/en-US/GETTING-STARTED.md) | Install the app, create your taxpayer profile, and import your first statement |
| [IBKR Activity Statement Setup](docs/en-US/IBKR-SETUP.md) | How to generate the right CSV from IBKR and connect it to Rentier |
| [Serbian PP-OPO Tax Overview](docs/en-US/TAX-OVERVIEW.md) | How the Serbian passive income tax works and what Rentier calculates |

### Srpski (Serbian)
| Vodič | Opis |
|---|---|
| [Prvi koraci](docs/sr-RS/PRVI-KORACI.md) | Instalirajte aplikaciju, kreirajte svoj profil poreskog obveznika i uvezite svoju prvu IBKR izjavu |
| [IBKR Activity Statement instalacija](docs/sr-RS/IBKR-INSTALACIJA.md) | Kako da generišete ispravan CSV iz IBKR-a i da ga povežete sa Rentier-om |
| [Pregled srpskog PP-OPO poreza](docs/sr-RS/PREGLED-POREZA.md) | Kako funkcioniše srpski porez na pasivni dohodak i šta Rentier izračunava |

### Screenshots

| Dashboard | Filings | Reports |
|---|---|---|
| ![Dashboard](docs/en-US/screenshots/Dashboard.png) | ![Filings](docs/en-US/screenshots/Filings.png) | ![Reports](docs/en-US/screenshots/Reports.png) |

Want to contribute code? See [CONTRIBUTING.md](CONTRIBUTING.md).

---

## How It Works

```
IBKR Activity Statement (CSV)
        │
        ▼
  Rentier parses Dividends,
  Interest & Withholding Tax rows
        │
        ▼
  NBS exchange rate fetched
  for each income date
        │
        ▼
  Tax calculated: 15% of gross
  income minus foreign WHT credit
        │
        ▼
  PP-OPO XML exported
  (one file per income event)
        │
        ▼
  You upload to ePorezi portal
  and mark Filing as Filed/Paid
```

---

## Features

✅ **IBKR CSV Import** – Parses Activity Statements: dividends, interest, and withholding tax
✅ **Tax Calculation** – 15% Serbian income tax with automatic foreign withholding credit
✅ **NBS Exchange Rates** – Auto-fetches and caches Serbian National Bank mid-rates per date
✅ **PP-OPO XML Export** – Generates submission-ready XML for the ePorezi portal
✅ **Filing Lifecycle** – Tracks each filing through Init → Filed → Paid
✅ **Deadline Calculation** – 30-day deadline adjusted for weekends and Serbian public holidays
✅ **Email Automation** – Monitors an IMAP mailbox for new IBKR statements and imports automatically
✅ **Secure Credentials** – OS-level credential store; no passwords stored in SQLite
✅ **Multi-Year Support** – Manage filings across multiple tax years

---

## Prerequisites

- **Windows 10 / Ubuntu 20.04 / macOS 12 or later**
- **.NET 10.0 Runtime** — [download](https://dotnet.microsoft.com/download)
- An **Interactive Brokers** account with activity to report
- A **Serbian taxpayer identification number** (JMBG)

---

## Quick Start

1. **Download and install** the application (see [Getting Started](docs/en-US/GETTING-STARTED.md))
2. **Create your taxpayer profile** — enter your JMBG, full name, address, and municipality code
3. **Configure an Importer** — link it to your profile and choose how statements arrive (manual upload or email)
4. **Import a statement** — upload an IBKR CSV or trigger a mailbox sync
5. **Process the report** — Rentier calculates taxes and generates filings
6. **Export PP-OPO XML** for each filing and submit via [ePorezi](https://www.purs.gov.rs/e-porezi.html)
7. **Mark filings as Filed**, then **Paid** once the tax payment clears

---

## Technology Stack

| Area | Technology |
|---|---|
| Language | C# 14 |
| Framework | .NET 10.0 |
| UI | Avalonia (MVVM, cross-platform) |
| Database | SQLite via Entity Framework Core |
| Architecture | Clean Architecture + CQRS |
| Testing | xUnit, FluentAssertions, NSubstitute |
| Email | MailKit (IMAP) |
| HTTP | HttpClient typed client pattern |

See [CONTRIBUTING.md](CONTRIBUTING.md) for the architecture deep-dive, build/test commands, and coding conventions.

---

## Roadmap

- [ ] Multi-account support (multiple IBKR accounts / taxpayer profiles)
- [ ] Bulk filing export (all filings for a tax year in one action)
- [ ] Alternative statement providers (Revolut, Wise, etc.)
- [ ] Linux/macOS Avalonia UX improvements (theming, system integration)

---

## Support & Issues

- **Bug reports** — Open an [issue](../../issues) with reproduction steps and the relevant CSV section
- **Feature requests** — Use [discussions](../../discussions) or file an issue with the `enhancement` label
- **Questions** — Check existing issues and discussions first

---

## License

This project is licensed under the **Apache License 2.0** — see [LICENSE](LICENSE) for details.

## Authors

- **Djordje Milenkovic**

## Acknowledgments

- [Avalonia](https://avaloniaui.net/) for the cross-platform MVVM UI framework
- [MailKit](https://github.com/jstedfast/MailKit) for IMAP email sync
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/) for data access
- [Serbian National Bank (NBS)](https://www.nbs.rs/) for exchange rate services
