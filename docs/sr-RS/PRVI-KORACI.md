# Prvi koraci sa Rentier-om

Ovaj vodič vas provodi kroz instalaciju Rentier-a, podešavanje profila poreskog obveznika i uvoz prve IBKR izjave o aktivnosti.

---

## Preduslovi

| Zahtev | Detalji |
|---|---|
| Operativni sistem | Windows 10, Ubuntu 20.04 ili macOS 12 (ili noviji) |
| .NET Runtime | [.NET 10.0](https://dotnet.microsoft.com/download) |
| IBKR nalog | Sa dividendama ili kamatama koje treba prijaviti |
| Srpski JMBG | Vaš 13-cifreni jedinstveni matični broj građana |

---

## Korak 1 — Preuzimanje i instalacija

Rentier se preuzima sa [GitHub Releases](https://github.com/dj-milenkovic/rentier/releases) stranice projekta, gde su dostupne i instalacione i portabilne verzije za Windows, macOS i Linux.

1. Otvorite Releases stranicu i preuzmite paket za svoj operativni sistem.
2. Za instalacionu verziju, pokrenite instaler i pratite uputstva.
3. Za portabilnu verziju, raspakujte arhivu na željenu lokaciju i pokrenite izvršni fajl direktno — instalacija nije potrebna.

---

## Korak 2 — Kreiranje profila poreskog obveznika

Pri prvom pokretanju otvorite **Podešavanja → Profil** i popunite sledeća polja:

| Polje | Opis | Primer |
|---|---|---|
| JMBG | 13-cifreni jedinstveni matični broj | `0101990710125` |
| Puno ime | Kako se pojavljuje na vašoj poreskoj identifikaciji | `Petar Petrović` |
| Adresa | Ulica i broj | `Knez Mihailova 1` |
| Šifra opštine | Šifra vaše opštine sa zvaničnog šifrarnika | `70092` |
| Telefon (opciono) | Kontakt broj za poresku prijavu | `+381 11 1234567` |
| Email (opciono) | Kontakt email za poresku prijavu | `petar@example.com` |

> **Šifre opština** objavljuje Poreska uprava Republike Srbije. Pretražite "šifrarnik opština" da pronađete šifru svoje opštine.

---

## Korak 3 — Podešavanje Importer-a

**Uvoznici** povezuje izvor izjave sa vašim profilom poreskog obveznika. Idite na **Uvoznici → Dodaj Novi**:

| Polje | Opis |
|---|---|
| Prikazni naziv | Prepoznatljiva labela, npr. `IBKR – Dividende 2024` |
| Tip izveštaja | Izaberite **IBKR CSV** |
| Profil poreskog obveznika | Izaberite profil kreiran u Koraku 2 |
| Poštansko sanduče (opciono) | Povežite IMAP sanduče za automatsku obradu e-pošte (videti Korak 5) |

### Polja filtera e-pošte (opciono — potrebna samo za automatsku obradu)

| Polje | Kako funkcioniše | Primer |
|---|---|---|
| Filter pošiljaoca | Podudaranje podniza u adresi pošiljaoca | `interactivebrokers.com` |
| Filter predmeta | Podudaranje podniza u predmetu e-pošte | `Activity Statement` |
| Regex za prilog | Regularni izraz koji se poredi sa nazivom fajla priloga email-a | `.*\.csv` |

> **Važno:** Ako uključite automatsku obradu e-pošte, polje **Regex za prilog** ne sme ostati prazno — bez njega se nijedan privitak neće uvesti. Bezbedna podrazumevana vrednost je `.*\.csv`, koja prihvata bilo koji CSV fajl.

---

## Korak 4 — Ručni uvoz izjave

Ako ne želite automatsku obradu e-pošte, izjavu možete otpremiti direktno:

1. Izvezite **Activity Statement CSV** iz IBKR-a — tačna uputstva potražite u [IBKR vodiču za podešavanje](IBKR-INSTALACIJA.md).
2. U Rentier-u otvorite **Izveštaji → [vaš izveštaj] → Uvezi...**.
3. Izaberite preuzeti CSV fajl.
4. Rentier će parsirati fajl, preuzeti NBS kurseve za svaki datum dohotka i kreirati pojedinačne **Prijave**.

---

## Korak 5 — Podešavanje automatske obrade e-pošte (opciono)

Rentier može pratiti IMAP sanduče i automatski uvoziti nove IBKR izjave.

### 5a — Dodavanje sandučeta

Idite na **Podešavanja → Poštanski sandučići → Dodaj novi** i unesite:

| Polje | Opis | Tipična IMAP vrednost |
|---|---|---|
| Host | Adresa IMAP servera | `imap.gmail.com` |
| Port | IMAP SSL port | `993` |
| Korisničko ime / Email | Vaša email adresa | `vi@gmail.com` |
| Lozinka | Vaša aplikacijska lozinka | xxxxxxxx |

Kredencijali se čuvaju isključivo u Credential Manager-u operativnog sistema, nikada u bazi podataka aplikacije.

> **Aplikacijske lozinke:** Ako vaš email provajder koristi dvofaktorsku autentifikaciju (Gmail, Outlook itd.), potrebno je da generišete **aplikacijsku lozinku** umesto lozinke za nalog. Uputstva potražite u dokumentaciji svog provajdera.

### 5b — Povezivanje sandučeta sa Uvoznikom

Izmenite svoj Uvoznik i postavite polje **Poštansko sanduče** na sanduče koje ste upravo kreirali. Proverite da su sva tri polja filtera popunjena.

### 5c — Pokretanje sinhronizacije

Idite na **Sinhronizacija → Pokreni sinhronizaciju**. Rentier se povezuje sa sandučetom, pretražuje e-poštu koja odgovara vašim filterima, preuzima odgovarajuće CSV privitke i stavlja ih u red za obradu kao izveštaje.

Kursor sinhronizacije se pomera nakon svake uspešne obrade, tako da naredne sinhronizacije razmatraju samo nove poruke.

---

## Korak 6 — Pregled Prijava

Nakon obrade izveštaja, Rentier kreira po jednu **Prijavu** za svaki događaj dohotka. Idite na **Prijave** da ih pregledate:

| Kolona | Značenje |
|---|---|
| Status | Init / Filed / Paid |
| Tip prihoda | Dividenda ili kamata |
| Isplatilac | Simbol akcije ili naziv institucije |
| Rok za podnošenje | 30 kalendarskih dana od datuma dohotka, pomeren na naredni radni dan ako pada na neradni |
| Porez za uplatu (RSD) | Srpska poreska obaveza nakon kreditiranja poreza po odbitku |
| Referenca plaćanja | Identifikacioni broj prijave sa portala ePorezi  |

---

## Korak 7 — Izvoz i podnošenje

Za svaki filing:

1. Kliknite **Izvezi PP-OPO XML** da generišete fajl za podnošenje.
2. Prijavite se na portal [ePorezi](https://www.purs.gov.rs/e-porezi.html) Poreske uprave Srbije.
3. Otpremite XML fajl u **PP-OPO → Nova prijava**.
4. Nakon podnošenja, vratite se u Rentier i kliknite **Označi kao podneto** na odgovarajućoj prijavi.
5. Kada porez bude plaćen, kliknite **Označi kao plaćeno**.

> Prijavi prolazi kroz strogo definisan redosled statusa: **Init → Filed → Paid**. Preskakanje koraka nije moguće.

---

## Rešavanje čestih problema

| Simptom | Verovatan uzrok | Rešenje |
|---|---|---|
| Nijedna pijava nije kreiran nakon obrade | CSV ne sadrži sekcije `Dividends` ili `Interest` | Proverite [IBKR vodič za podešavanje](IBKR-INSTALACIJA.md) i uverite se da su te sekcije uključene |
| Iznos poreza po odbitku prikazuje 0 iako je porez naplaćen | Sekcija `Withholding Tax` nedostaje u CSV-u | Ponovo izvezite izveštaj sa uključenom tom sekcijom |
| Kurs nije pronađen | NBS nije objavio kurs za taj datum (praznik/vikend) | Rentier automatski koristi poslednji prethodni radni dan; ako problem ostane, proverite internet konekciju |
| Sinhronizacija sandučeta uvozi 0 izveštaja | Attachment regex je prazan ili su filteri previše strogi | Proverite sva tri polja filtera na importer-u; testirajte sa `.*\.csv` kao attachment regex |
| Greška "IMAP sync failed" | Pogrešni kredencijali, pogrešan port ili je potrebna aplikacijska lozinka | Ponovo unesite kredencijale i koristite port 993 sa SSL-om |

---

## Sledeći koraci

- [IBKR Activity Statement instalacija](IBKR-INSTALACIJA.md) — detaljno uputstvo za generisanje ispravnog CSV-a
- [Pregled srpskog PP-OPO poreza](PREGLED-POREZA.md) — objašnjenje šta Rentier izračunava i zašto
