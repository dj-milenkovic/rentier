# IBKR Activity Statement podešavanja

Ovaj vodič objašnjava kako generisati ispravan CSV izvoz iz Interactive Brokers-a (IBKR) i kako ga povezati sa Rentier-om — bilo kroz ručno otpremanje ili dozvoljavanjem Rentier-u da ga automatski preuzme iz vašeg inbox-a.

---

## Šta Rentier zahteva

Rentier parsira **Activity Statement** CSV format izvezen iz IBKR-a. Traži četiri specifične sekcije:

| Naziv sekcije (u CSV) | Šta sadrži | Obavezno? |
|---|---|---|
| `Dividends` | Bruto isplate dividendi po akciji, datumu i valuti | Da (ako imate dividende) |
| `Withholding Tax` | Inostrani porez već odbijen od svake dividende | Da (ako je porez odbijen) |
| `Interest` | Pripisana kamata (zarađena) i odbitna kamata (naplaćena) | Da (ako imate prihod od kamate) |
| `Base Currency Exchange Rate` | IBKR-ove sopstvene FX stope za period | Preporučeno kao rezerva |

> **Samo ove sekcije se čitaju.** Sve ostale sekcije u Activity Statement-u (trgovine, akcije, stanja gotovine, itd.) su bezbedno ignorisne.

---

## Opcija A — Ručni izvoz (Najjednostavniji)

### Korak 1 — Prijavite se na IBKR Client Portal

Idite na [https://www.ibkr.com/portal](https://www.ibkr.com/portal) i prijavite se.

### Korak 2 — Navigujte do Statements

U meniju, idite na **Performance & Reports → Statements**.

### Korak 3 — Generišite Activity Statement

1. Izaberite **Statement type: Activity**.
2. Postavite **raspon datuma** — obično cela kalendarska godina (1. januar – 31. decembar) da uhvatite sve dohodke.
3. Postavite **Format: CSV**.
4. Pazite da su sledeće sekcije **uključene**:
   - Dividends
   - Withholding Tax
   - Interest
   - Base Currency Exchange Rate (preporučeno)
5. Kliknite **Run** ili **Create Statement**.

> Dugme za preuzimanje se pojavljuje nakon što je izjava generisana. Može potrajati i do minut.

### Korak 4 — Otpremite u Rentier

U Rentier-u, idite na **Importers → [vaš importer] → Upload Statement**, izaberite CSV datoteku i kliknite **Process**.

---

## Opcija B — Automatska obrada e-pošte preko IBKR Flex Queries

IBKR može automatski da vam pošalje izjavu po rasporedu (dnevno, sedmično, mesečno). Rentier prati vašu inbox i automatski uvozi izveštaje.

### Korak 1 — Kreirajte Flex Query u IBKR

1. U Client Portal-u, idite na **Performance & Reports → Flex Queries**.
2. Kliknite **Create** i izaberite **Activity Flex Query**.
3. Dajte joj deskriptivni naziv, na primer `Rentier Monthly`.
4. U **Sections**, omogućite najmanje:
   - **Dividends**
   - **Withholding Tax**
   - **Interest**
   - **Base Currency Exchange Rate**
5. Postavite **Format: CSV**.
6. U **Delivery**, izaberite **Email** i unesite email adresu koju će Rentier pratiti.
7. Postavite željeni **raspored** (na primer mesečno prvog dana meseca, pokrivajući prethodni mesec).
8. Sačuvajte Flex Query.

> IBKR šalje izjavu sa `@interactivebrokers.com` adrese. Predmet obično sadrži "Flex Statement" ili "Activity Statement" a izveštaj je `.csv` datoteka.

### Korak 2 — Konfigurišite sanduče u Rentier

Videti [Prvi koraci — Korak 5](PRVI-KORACI.md#korak-5--konfigurajte-automatsku-obradu-e-pošte-opciono) za potpune uputstvo za podešavanje sandučića.

### Korak 3 — Konfigurišite filter Importer-a

Uređujte vaš Importer i postavite sledeća polja filtera da se poklapaju sa e-poštom koju IBKR šalje:

| Polje filtera | Preporučena vrednost | Napomene |
|---|---|---|
| Filter pošiljaoca | `interactivebrokers.com` | Podudaranje podstringa na pošiljaocu; suzuje rezultate na IBKR e-poruke |
| Filter predmeta | `Flex Statement` ili `Activity Statement` | Podudaranje podstringa na predmetu; prilagodite ako vaša Flex Query koristi prilagođeni predmet |
| Regex za prilog | `.*\.csv` | Regex upoređen sa nazivom priloga; **ne sme biti prazan** |

> **Regex za prilog je obavezan.** Ako je ostavljen prazan, nijedan izveštaj neće biti uvezen, čak i ako se pronađe odgovarajuća e-pošta.

### Korak 4 — Pokrenite sinhronizaciju

U Rentier-u, idite na **Sinhronizacija → Pokreni sinhronizaciju**. Nakon prve uspešne sinhronizacije, kasnije sinhronizacije obrađuju samo e-poruke novije od poslednje uveiene poruke.

---

## Razumevanje CSV formata

Ako želite da proverite da je vaša datoteka ispravna pre nego što je uvezte, otvorite je u tekstualnom editoru. Rentier traži redove strukturirane na sledeći način:

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

> **Napomena:** U IBKR-ovom CSV-u iznosi poreza po odbitku prikazani su kao **negativne vrednosti**, jer predstavljaju sredstva koja su već zadržana. Rentier ih prepoznaje i automatski pravilno obrađuje.

### Interest
```
Interest,Header,Currency,Date,Description,Amount,...
Interest,Data,USD,2024-01-31,USD Credit Interest for Jan-2024,12.45,...
Interest,Data,USD,2024-01-31,USD Debit Interest for Jan-2024,-3.20,...
```

> Rentier obrađuje samo redove čiji opis sadrži **"Credit Interest"** ili **"Debit Interest"**. Drugi opisi kamata se ignorišu.

### Base Currency Exchange Rate
```
Base Currency Exchange Rate,Header,FromCurrency,Date,...,ToCurrency,Rate,...
Base Currency Exchange Rate,Data,USD,2024-03-15,...,EUR,0.91723,...
```

---

## Šta Rentier kreira a šta ignoriše

| Ulaz | Šta Rentier kreira |
|---|---|
| Red `Dividends,Data` | Moguća prijava sa vrstom dohodka **Dividend** |
| Upoređeni red `Withholding Tax,Data` | WHT kredit primenjuje se na odgovarajuću prijavu dividende |
| Red `Interest,Data` sa "Credit Interest" | Moguća prijava sa vrstom dohodka **Interest** |
| Red `Interest,Data` sa "Debit Interest" | Uvezeno ali nije korišćeno za poreske prijave (debit kamate nisu oporezivi dohodak u Srbiji — proverite sa savetnikom) |
| Neupoređeni WHT red (nema odgovarajuće dividende) | Logovano kao parse upozorenje; nije kreirana prijava |
| Bilo koja druga sekcija | Sasvim ignoriše |

---

## Otklanjanje grešaka

### "No recognised IBKR sections found in the CSV"
Vaša datoteka ne sadrži nijedan od četiri očekivana naziva sekcije. To obično znači:
- Izvezli ste pogrešan tip izjave (na primer Trade Confirmation umesto Activity Statement)
- Datoteka je XML izvoz umesto CSV — ponovno izvezite i izaberite **CSV format**

### "WHT_UNMATCHED — No dividend found for WHT entry"
Red poreza po odbiku se odnosi na akciju i datum za koji nije pronađena dividenda u istoj datoteci. Mogući uzroci:
- Sekcija `Dividends` nije uključena u izvoz — ponovno izvezite sa omogućenom opcijom
- Raspon datuma je previše uzan i propušta odgovarajuću dividendu

### "WHT_CURRENCY_MISMATCH"
Porez po odbitku je odbijen u drugoj valuti od dividende. Ovo je neuobičajeno ali može se desiti sa viševalutnim računima. Pregledajte raw CSV i konsultujte se sa savetnikom.

### Prazan uvoz nakon sinhronizacije e-pošte
Proverite sledeće redom:
1. **Regex za prilog** nije prazan na Uvozniku
2. **Filter pošiljaoca** i **Filter predmeta** se poklapaju sa stvarnom e-poštom koju IBKR šalje
3. Pristupni podaci za sandučića su ispravni (prvo pokušajte da se povežete sa standardnim IMAP klijentom)
4. IBKR je zaista poslao izjavu — proverite direktno inbox

---

## Sledeći koraci

- [Prvi koraci](PRVI-KORACI.md) — potpun vodič kroz instalaciju uključujući konfiguraciju sandučića i profila
- [Pregled srpskog PP-OPO poreza](PREGLED-POREZA.md) — razumevanje poreskih pravila koja Rentier primenjuje
