# Prvi koraci sa Rentier-om

Ovaj vodič vas provodi kroz instalaciju Rentier-a, podešavanje profila poreskog obveznika i uvoz prve IBKR izjave o aktivnosti.

---

## Preduslovi

| Zahtev | Detalji |
|---|---|
| Operativni sistem | Windows 10, Ubuntu 20.04 ili macOS 12 (ili noviji) |
| IBKR nalog | Sa dividendama ili kamatama koje treba prijaviti |
| Srpski JMBG | Vaš 13-cifreni jedinstveni matični broj građana |

---

## Korak 1 — Preuzimanje i instalacija

Rentier preuzmite sa [GitHub Releases](https://github.com/dj-milenkovic/rentier/releases) stranice projekta. Svaki paket sadrži sve što je Rentier-u potrebno za rad, pa .NET ne morate da instalirate.

| Operativni sistem | Fajl | Kako se instalira |
|---|---|---|
| Windows (instaler) | `Rentier-<verzija>-win-x64-setup.exe` | Pokrenite ga i pratite uputstva. Instalira Rentier samo za vaš nalog (podrazumevano u `%LocalAppData%\Programs\Rentier`) i ne traži administratorska prava. |
| Windows (portabilna verzija) | `Rentier-<verzija>-win-x64.zip` | Raspakujte ga u novu, praznu fasciklu u koju možete da upisujete (na primer u svojoj korisničkoj fascikli) i pokrenite `Rentier.exe`. Ne pokreće se nikakav instalacioni program. |
| macOS, Apple silicon (M1 i noviji) | `Rentier-<verzija>-osx-arm64.dmg` | Otvorite DMG i prevucite `Rentier.app` u **Applications**. Pokrećite ga iz Applications, ne iz DMG-a. |
| macOS, Intel | `Rentier-<verzija>-osx-x64.dmg` | Isto kao iznad. |
| Linux x64 | `Rentier-<verzija>-linux-x64.AppImage` | Učinite ga izvršnim (`chmod +x Rentier-<verzija>-linux-x64.AppImage`) i pokrenite ga. Držite ga u fascikli u koju možete da upisujete. Možda će mu trebati `libfuse2`, a potreban mu je i ICU (`libicu`), koji većina distribucija već sadrži. |

Ako ne želite da pokrećete instaler, koristite Windows ZIP, macOS DMG ili Linux AppImage: nijedan od njih ne pokreće instalacioni program, a ažuriraju se sami, isto kao instalirana verzija.

### Ažuriranja

Gore navedeni paketi se sami ažuriraju. Rentier paketi **v1.3.0 i stariji to ne mogu**: pogledajte [Prelazak sa verzije v1.3.0 ili starije](#prelazak-sa-verzije-v130-ili-starije) ispod.

Pri svakom pokretanju Rentier proverava da li na Releases stranici postoji novija verzija. Ako postoji, na vrhu prozora se pojavljuje traka:

1. **„Dostupna je nova verzija v<verzija>”**: izaberite **Ažuriraj sada** da je preuzmete ili **Kasnije** da sakrijete traku. Rentier će vas ponovo pitati pri sledećem pokretanju.
2. **„Preuzimanje ažuriranja... <n>%”** prikazuje napredak preuzimanja.
3. **„Ažuriranje je spremno. Ponovo pokrenite program da biste ga primenili.”**: izaberite **Ponovo pokreni** da se Rentier zatvori, instalira ažuriranje i ponovo pokrene, ili **Kasnije** da nastavite sa radom. Ako izaberete Kasnije, ažuriranje se instalira pri sledećem pokretanju Rentier-a.

Ako preuzimanje ili instalacija ne uspe, crvena traka prikazuje **„Ažuriranje nije uspelo: <razlog>”** sa dugmićima **Pokušaj ponovo** (ponovo pokreće preuzimanje) i **Zatvori**. Ako Rentier ne može da pristupi Releases stranici, na primer kada niste povezani na internet, ne prikazuje nikakvu traku i jednostavno ponovo proverava pri sledećem pokretanju.

### Prelazak sa verzije v1.3.0 ili starije

Paketi v1.3.0 i stariji ne mogu sami da se ažuriraju. Pređite jednom; posle toga se Rentier ažurira iz same aplikacije.

| Operativni sistem | Šta treba uraditi |
|---|---|
| Windows, instalirana verzija | Jednom pokrenite `Rentier-<verzija>-win-x64-setup.exe`. Ako je Rentier bio instaliran za vaš nalog, instaler ga ažurira na istom mestu. Ako je bio instaliran **za sve korisnike**, instaler će vam to reći i prekinuti instalaciju: prvo deinstalirajte Rentier u **Podešavanja → Aplikacije** (Settings → Apps), pa ponovo pokrenite instaler. |
| Windows, portabilni ZIP | Raspakujte novi ZIP u novu, praznu fasciklu i pokrenite `Rentier.exe`. Zatim obrišite staru fasciklu i sve prečice ka njenom `Rentier.Desktop.exe`. |
| macOS | Otvorite novi DMG i prevucite `Rentier.app` u **Applications**, tako da zameni stari. |
| Linux | Umesto `.deb` ili `.tar.gz` paketa koristite AppImage. Ako ste instalirali `.deb`, uklonite ga komandom `sudo apt remove rentier`, da stara stavka u meniju više ne bi pokretala staru verziju. |

**Vaši podaci ostaju netaknuti.** Rentier čuva bazu podataka, podešavanja i dnevnike u vašem korisničkom profilu, a ne u fascikli programa: `%LocalAppData%\Rentier` na Windows-u, `~/Library/Application Support/Rentier` na macOS-u i `~/.local/share/Rentier` na Linux-u. Prelazak na drugi paket, ažuriranje ili deinstalacija programa ne diraju tu fasciklu.

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

### Dijagnostički dnevnici

Rentier upisuje jedan dnevnik po danu (na primer `rentier-20260115.log`) i briše dnevnike starije od 30 dana. Dnevnici beleže šta je program radio i eventualne greške, sa vremenskim oznakama. Rentier izbegava da u njih upisuje lične ili finansijske podatke (imena, adrese e-pošte, brojeve računa ili iznose), ali pregledajte dnevnik pre nego što ga podelite.

- **Gde se nalaze:** otvorite **Podešavanja → Dijagnostika → Otvori fasciklu sa dnevnicima**. Na Windows-u je fascikla `%LOCALAPPDATA%\Rentier\logs`, na macOS-u `~/Library/Application Support/Rentier/logs`, a na Linux-u `~/.local/share/Rentier/logs`.
- **Više detalja:** ako se od vas zatraži da reprodukujete problem, uključite **Detaljno (debug) beleženje** na istoj stranici. Stupa na snagu odmah i pamti se dok ga ne isključite.
- **Prijava problema:** priložite dnevnik za dan kada se problem desio uz svoj [GitHub issue](https://github.com/dj-milenkovic/rentier/issues).

---

## Sledeći koraci

- [IBKR Activity Statement instalacija](IBKR-INSTALACIJA.md) — detaljno uputstvo za generisanje ispravnog CSV-a
- [Pregled srpskog PP-OPO poreza](PREGLED-POREZA.md) — objašnjenje šta Rentier izračunava i zašto
