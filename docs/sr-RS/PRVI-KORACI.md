# Prvi koraci sa Rentier-om

Ovaj vodič vas vodi kroz instalaciju Rentier-a, konfiguraciju profila poreskog obveznika i uvoz prve IBKR izjave o aktivnosti.

---

## Preduslov

| Zahtev | Detalji |
|---|---|
| Operativni sistem | Windows 10 ili noviji |
| .NET Runtime | [.NET 8.0+](https://dotnet.microsoft.com/download) |
| IBKR račun | Sa delatnostima od dividendi ili kamata za prijavu |
| Srpski JMBG | Vaš 13-cifreni broj lične identifikacije |

---

## Korak 1 — Kompajliranje i pokretanje

Dok pakovani instalater nije dostupan, pokrenite Rentier iz izvornog koda:

```bash
git clone https://github.com/djordje.milenkovic96/rentier.git
cd rentier
dotnet restore
dotnet run --project src/Rentier.Desktop/Rentier.Desktop.csproj
```

---

## Korak 2 — Kreirajte profil poreskog obveznika

Pri prvom pokretanju, otvorite **Settings → Taxpayer Profile** i popunite:

| Polje | Opis | Primer |
|---|---|---|
| JMBG | 13-cifreni jedinstveni broj meštana | `0101990710125` |
| Puno ime | Kako se pojavljuje na vašoj ID kartici za poreze | `Petar Petrović` |
| Adresa | Ulica i broj | `Knez Mihailova 1` |
| Šifra opštine | Vaša Opština šifra sa zvanične liste | `70092` |
| Telefon (opciono) | Kontakt broj za poresku prijavu | `+381 11 1234567` |
| Email (opciono) | Kontakt email za poresku prijavu | `petar@example.com` |

> **Šifre opština** se objavljuju od strane Poreske uprave Republike Srbije. Pretražite "šifrarnik opština" da pronađete šifru vaše opštine.

---

## Korak 3 — Konfigurajte Importer

**Importer** povezuje izvor izjave sa vašim profilom poreskog obveznika. Idite na **Importers → New Importer**:

| Polje | Opis |
|---|---|
| Prikazni naziv | Lak labela, na primer `IBKR – Dividende 2024` |
| Tip izveštaja | Izaberite **IBKR CSV** |
| Profil poreskog obveznika | Izaberite profil koji ste kreirali u Koraku 2 |
| Poštanski sandučić (opciono) | Povežite IMAP sandučić za automatsku obradu e-pošte (videti Korak 5) |

### Polja filtera e-pošte (opciono — potrebna samo za automatsku obradu e-pošte)

| Polje | Kako radi | Primer |
|---|---|---|
| Filter pošiljaoca | Podudaranje podstringa na adresi pošiljaoca | `interactivebrokers.com` |
| Filter predmeta | Podudaranje podstringa na predmetu e-pošte | `Activity Statement` |
| Regex privitka | Regularni izraz upoređen sa nazivom datoteke privitka | `.*\.csv` |

> **Važno:** Ako omogućite automatsku obradu e-pošte, polje **Attachment regex** ne sme biti prazno. Bez njega, nijedan privitак neće biti uvezen. Bezbedan podrazumevani je `.*\.csv` da prihvatite bilo koju CSV datoteku.

---

## Korak 4 — Ručno uvezte izjavu

Ako ne želite automatsku obradu e-pošte, možete direktno otpremiti izjavu:

1. Izvezite **Activity Statement CSV** iz IBKR-a — vidite [IBKR Activity Statement Setup](IBKR-SETUP.md) za tačne korake.
2. U Rentier-u, idite na **Importers → [vaš importer] → Upload Statement**.
3. Izaberite CSV datoteku koju ste preuzeli.
4. Kliknite **Process** — Rentier će parsirati datoteku, preuzeti NBS vrednosti za svaki datum dohodka i kreirajti pojedinačne **Filings**.

---

## Korak 5 — Konfigurajte automatsku obradu e-pošte (Opciono)

Rentier može pratiti IMAP sandučić i automatski uveći nove IBKR izjave.

### 5a — Dodajte sandučić

Idite na **Settings → Mailboxes → New Mailbox** i unesite:

| Polje | Opis | Tipična IMAP vrednost |
|---|---|---|
| Host | IMAP adresa servera | `imap.gmail.com` |
| Port | IMAP SSL port | `993` |
| Korisničko ime | Vaša email adresa | `vi@gmail.com` |

Nakon čuvanja, Rentier će vas tražiti da unesete **lozinku** ili **aplikacijsku lozinku**. Akreditivu se čuvaju u Upravniku kredencijala Windows-a — nikada u bazi podataka.

> **Aplikacijske lozinke:** Ako vaš email pružaoc koristi dvofaktorsku autentifikaciju (Gmail, Outlook, itd.), morate generisati **aplikacijsku lozinku** umesto da koristite lozinku svog računa. Proverite dokumentaciju vašeg pružaoca servisa za uputstva.

### 5b — Povežite sandučić sa vašim Importer-om

Uređujte vaš Importer i postavite polje **Mailbox** na sandučić koji ste upravo kreirali. Pazite da su sva tri polja filtera (From, Subject, Attachment regex) konfigurirana.

### 5c — Pokrenite sinhronizaciju

Idite na **Sync → Run Now**. Rentier se povezuje sa sandučićem, traži e-poštu koja se poklapa sa vašim filterima, preuzima odgovarajuće CSV privitke i stavlja ih u red čekanja kao Izveštaje za obradu.

Kursor sinhronizacije se pomiče nakon svake uspešne obrade, tako da kasnije sinhronizacije razmatraju samo nove e-poruke.

---

## Korak 6 — Pregledajte Filings

Nakon obrade izjave, Rentier kreira jedan **Filing** po događaju dohodka. Idite na **Filings** da ih vidite:

| Kolona | Značenje |
|---|---|
| Datum dohodka | Kada je dividenda/kamata isplaćena |
| Plaćajući subjekt | Simbol akcije ili naziv institucije |
| Vrsta dohodka | Dividenda ili Kamata |
| Bruto dohodak (RSD) | Inostrani dohodak pretvoren po NBS kursu |
| Odbijeni porez (RSD) | Inostrani porez na izvor već odbijen |
| Porez na plaćanje (RSD) | Srpski porez obaveza nakon kreditiranja poreza na izvor |
| Rok prijave | 30 kalendarski dana nakon dohodka, pomeren na sledeći radni dan |
| Status | Init / Filed / Paid |

---

## Korak 7 — Izvezi i podnesi

Za svaki filing:

1. Kliknite **Export PP-OPO XML** da generišete datoteku za podnošenje.
2. Prijavite se na portal [ePorezi](https://www.purs.gov.rs/e-porezi.html) (Poreska uprava Srbije).
3. Otpremite XML datoteku u **PP-OPO → Nova prijava**.
4. Nakon podnošenja, vratite se u Rentier i kliknite **Mark as Filed** na filing-u.
5. Kada ste platili porez, kliknite **Mark as Paid**.

> Filings prolaze kroz jedne po jedne korake: **Init → Filed → Paid**. Ne možete preskakati korake.

---

## Česti problemi

| Simptom | Verovatni uzrok | Ispravka |
|---|---|---|
| Nema kreiranih filing-a nakon obrade | CSV ne sadrži `Dividends` ili `Interest` sekcije | Proverite [IBKR vodič za instalaciju](IBKR-SETUP.md) — pazite da su te sekcije omogućene |
| Iznos WHT pokazuje 0 čak i kada je porez odbijen | Sekcija `Withholding Tax` nedostaje iz vašeg CSV-a | Ponovno izvezite sa tom sekcijom omogućenom |
| Kurs nije pronađen | NBS nije objavio kurs za taj datum (praznik/vikend) | Rentier se vraća na poslednji prethodni radni dan; ako i dalje ne uspe, proverite vašu internet konekciju |
| Mailbox sinhronizacija uvezi 0 izveštaja | Attachment regex je prazan ili su filteri previše stroogi | Proverite sva tri polja filtera importer-a; testirajte sa `.*\.csv` kao attachment regex |
| "IMAP sync failed" greška | Pogrešne akreditive, pogrešan port, ili je potrebna aplikacijska lozinka | Ponovno unesite akreditive; koristite port 993 sa SSL |

---

## Sledeći koraci

- [IBKR Activity Statement Setup](IBKR-SETUP.md) — detaljne uputstvo za generisanje ispravnog CSV-a
- [Pregled srpskog PP-OPO poreza](TAX-OVERVIEW.md) — razumevanje šta Rentier izračunava i zašto
