# Pregled srpskog PP-OPO poreza

Ovaj dokument objašnjava pravila srpskog poreza na pasivni dohodak koja Rentier primenjuje. Dat je samo u informativne svrhe.

> **Odricanje od odgovornosti:** Ovo nije poreska savet. Srpsko poresko pravo je predmetno promenama. Uvek proverite svoju obavezu sa licenciranim srpskim poreskim savetنиком ili Poreskom upravom (Poreska uprava).

---

## Šta je PP-OPO?

**PP-OPO** je srpska poreska forma koju koriste pojedini poreski obaveznici da sama prijave i plate porez na pasivni dohodak primljen iz inostranih izvora. Skraćenica se odnosi na "Poreska prijava za porez po odbitku" — prijava poreza na odbitku podnetanu od strane primaoca kada plaćajući subjekt (inostrani brokер) ne odbija srpski porez.

Ovo se primenjuje na srpske rezidente koji primaju:
- **Dividende** od inostranih kompanija (na primer putem Interactive Brokers-a)
- **Prihod od kamata** od inostranih finansijskih institucija

Svaki dohodak — svaka pojedinačna isplata dividende ili kreditiranje kamate — zahteva **odvojenu PP-OPO prijavu**.

---

## Vrste dohodka koje Rentier obrađuje

| Vrsta dohodka | IBKR CSV sekcija | Tretman srpskog poreza |
|---|---|---|
| Dividende | `Dividends` | Oporezive na 15%; inostrani WHT smanjuje srpski porez |
| Pripisana kamata | `Interest` (Credit Interest redovi) | Oporezive na 15%; inostrani WHT može se primeniti |

> **Odbitna kamata** (kamata koju plaćate IBKR-u na marzinu, na primer) se uvozi iz izjave ali ne kreira poresku prijavu — nije oporezivi dohodak. Potrdite to sa savetником.

---

## Poreska stopa

Srpski porez na lični dohodak od pasivnog dohodka od kapitala je **15%** bruto dohodka u srpskim dinarima (RSD).

```
Srpski porez = bruto dohodak (RSD) × 15%
```

---

## Inostrani kredit poreza na izvor

Većina zemalja koja plaća dividende odbija **porez na izvor (WHT)** na izvoru pre nego što sredstva dostignu vaš račun. Srbija vam omogućava da kreditirate ovaj inostrani porez prema vašoj srpskoj poreskoj obavezi, tako da ne plaćate porez dvaput na isti dohodak.

Kredit radi kako sledi:

```
Srpski porez = max(bruto srpski porez − WHT već plaćen u RSD, 0)
```

**Kredit ne može preći izračunati srpski porez.** Ako je inostrana WHT stopa jednaka ili viša od 15%, dodatni srpski porez nije obavezan. Ako je niža (na primer, 10% američka zadržavanja na dividende prema sporazumu Srbija-SAD), plaćate razliku Srbiji.

**Primer:**

| | Iznos |
|---|---|
| Bruto dividenda primljena | $100 USD |
| NBS kurs (USD→RSD) | 108.50 |
| Bruto dohodak u RSD | 10,850 RSD |
| Srpski bruto porez (15%) | 1,627.50 RSD |
| Inostrani WHT plaćen (10% × $100 = $10 USD) | 1,085 RSD |
| **Srpski porez obaveza** | **542.50 RSD** |

---

## Kurсni listovi

Svi iznosi moraju biti prijavljeni u **srpskim dinarima (RSD)**. Rentier konvertuje iznose inostrane valute koristeći **srednji kurs Narodne banke Srbije (NBS)** za datum dohodka.

- Rentier automatski preuzima kurse sa NBS veb-sajta za svaki datum dohodka.
- Ako NBS nije objavio kurs za taj tačan datum (vikendi, praznici), Rentier se vraća na **poslednji prethodni radni dan** kurs.
- Preuzeti kurсevi se čuvaju u lokalnoj SQLite bazi podataka da bi se izbegao ponovljeni mrežni zahtevi.

> IBKR uključuje svoje vlastite kurсeve u Activity Statement-u (sekcija `Base Currency Exchange Rate`). Rentier koristi ove kao rezervu samo za valute gde NBS ne objavljuje direktan kurs. Primarni izvor je uvek NBS.

---

## Rok prijave

PP-OPO forma mora biti podnešena u roku od **30 kalendarskih dana** od datuma primanja dohodka.

Ako 30. dan pada na vikend ili srpski javni praznik, rok se pomera na **sledeći radni dan**.

**Primer:**

| Datum dohodka | Sirov rok (+30 dana) | Prilagođeni rok |
|---|---|---|
| 2024-03-01 (petak) | 2024-03-31 (nedelja) | 2024-04-01 (ponedeljak) |
| 2024-04-15 (ponedeljak) | 2024-05-15 (sreda) | 2024-05-15 (nema prilagođavanja) |
| 2024-04-30 (utorak) | 2024-05-30 (četvrtak) | 2024-05-31 (petak — 30. maj je srpski praznik) |

Rentier to automatski izračunava koristeći konfigurisan srpski kalendar javnih praznika.

---

## Srpski javni praznici

Rentier koristi konfigurisan kalendar praznika (`HolidayConf`) za izračunavanje rokova. Standardni srpski javni praznici uključuju:

| Datum | Praznik |
|---|---|
| 1–2. januar | Nova godina |
| 7. januar | Pravoslavni Božić |
| 15–16. februar | Dan državnosti |
| 1–2. maj | Međunarodni praznik rada |
| 11. novembar | Dan armisticiјuma |

> Pravoslavni Uskrs je pomakljivi praznik i mora biti ažuriran u konfiguraciji svake godine. Proverite zvanični kalendar na [www.gov.rs](https://www.gov.rs).

---

## Životni ciklus prijave

Svaki dohodak prolazi kroz tri-koračni životni ciklus u Rentier-u:

```
Init → Filed → Paid
```

| Status | Značenje |
|---|---|
| **Init** | Rentier je izračunao prijavu; PP-OPO XML može biti izvezen |
| **Filed** | Ste podneli XML na portal ePorezi |
| **Paid** | Ste platili dužni porez |

Koraci su sekvencijalni — ne možete označiti prijavu kao Paid bez prvo označavanja kao Filed.

---

## PP-OPO podnošenje

Rentier generiše PP-OPO XML datoteku koja se poklapa sa šemom objavljenom od strane Poreske uprave Srbije (`http://pid.purs.gov.rs`). Za podnošenje:

1. Izvezite XML iz Rentier-a (**Export PP-OPO XML** dugme na bilo kojoj prijavi).
2. Prijavite se na [ePorezi](https://www.purs.gov.rs/e-porezi.html) — onlajn portal Poreske uprave Srbije.
3. Izaberite **PP-OPO → Nova prijava** (Nova prijava).
4. Otpremite XML datoteku.
5. Potvrdite i podneti.

Svaki dohodak zahteva svoju odvojenu prijavu. Ako ste imali 20 isplata dividendi tokom godine, podnešete 20 PP-OPO obrazaca.

---

## Često postavljena pitanja

**Trebam li podneti PP-OPO ako je sav moj inostrani porez odbijen na izvoru?**  
Da — čak i ako inostrani WHT jednaka ili prelazi 15% i dodatni srpski porez nije obavezan, i dalje ste dužni da podneste PP-OPO obrazac u informativne svrhe. Potrdite to sa savetником.

**Šta ako sam primio dividende u više valuta?**  
Svaka dividenda se konvertuje nezavisno koristeći NBS kurs za njenu valutu na datum dohodka. Rentier to automatski obrađuje.

**Šta ako NBS kurs nije dostupan za određenu valutu?**  
Rentier će pokušati da se vrati na kurs koji je dao IBKR iz izjave. Ako kurs uopšte nije dostupan, izveštaj će prikazati grešku obrade i moraćete ručno uneti kurs.

**Šta je sa SAD dividendama — postoji li ugovor o izbjegavanju dvostrukog oporezivanja?**  
Da, Srbija i Sjedinjene Države imaju ugovor o izbjegavanju dvostrukog oporezivanja. Standardna američka stopa zadržavanja za srpske rezidente je obično 10% (smanjena sa 30%). Ugovorena stopa smanjuje srpski porez zahvaljujući odbijenom iznosu. Rentier primenjuje WHT kredit mehanički na osnovu onoga što je u vašoj izjavi — uvek proverite primenu ugovora sa savetником.

**Šta ako sam propustio rok prijave?**  
Rentier izračunava rokove kao referencu, ali vas ne sprečava da podnese zakašnjeno. Obratite se Poreskoj upravi ili vašem savetнику za vodstvo o kaznama za zakašnjelo podnošenje.

**Šta ako brokер ispravi dividendu nakon što sam je već uvezao?**  
Brokeri povremeno ponovno izdaju dividendu po ispravljenom iznosu (storniranje plus ponovno postavljanje u niz izjava). Rentier to detektuje: ako uvezena izjava sadrži dohodak za istu kompaniju i datum isplate kao postojeća prijava ali sa drugačijim bruto iznosom, druga prijava nije kreirana. Umesto toga, log sinhronizacije prikazuje grešku **"Broker correction detected"** sa oba iznosa, i trebali biste da pregledате и ручno приlagodите postojeću prijavu. Uvoženje iste izjave (ili identičnog dohodka) dvaput je bezbedno — tačne duplikate se tiho preskakaju.

---

## Dodatno čitanje

- [Poreska uprava Srbije — PP-OPO obrazac](https://www.purs.gov.rs) (pretražite "PP-OPO")
- [ePorezi portal](https://www.purs.gov.rs/e-porezi.html)
- [NBS kurсni listovi](https://www.nbs.rs/kurs-liste/kursna-lista)
- [Ugovor Srbije i SAD o izbegavanju dvostrukog oporezivanja](https://www.mfin.gov.rs) — pretražite bilateralne ugovore

---

## Videti i

- [Prvi koraci](PRVI-KORACI.md) — instalirajte Rentier i pokrenite prvi uvoz
- [IBKR Activity Statement Setup](IBKR-INSTALACIJA.md) — generišite ispravan CSV iz Interactive Brokers-a
