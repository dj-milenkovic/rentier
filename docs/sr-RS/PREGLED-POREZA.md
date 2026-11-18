# Pregled srpskog PP-OPO poreza

Ovaj dokument objašnjava pravila srpskog poreza na pasivni dohodak koja Rentier primenjuje. Dat je samo u informativne svrhe.

> **Odricanje od odgovornosti:** Ovo nije poreski savet. Srpsko poresko pravo je predmetno promenama. Uvek proverite svoju obavezu sa licenciranim srpskim poreskim savetnikom ili Poreskom upravom.

---

## Šta je PP-OPO?

**PP-OPO** je poreska prijava koju koriste rezidenti Srbije da sami prijave i plate porez na pasivne prihode ostvarene iz inostranstva. Skraćenica označava „Poreska prijava za porez po odbitku“ — dokument koji podnosi primalac prihoda kada inostrani isplatilac (npr. broker) ne obračunava i ne zadržava srpski porez.

Ova prijava se podnosi u slučajevima kada rezidenti Srbije primaju:
- **Dividende** od inostranih kompanija (na primer putem Interactive Brokers-a)
- **Prihod od kamata** od inostranih finansijskih institucija

Važno je naglasiti da se za svaku pojedinačnu isplatu — bilo dividende, bilo kamata — podnosi posebna PP‑OPO prijava.

---

## Vrste dohotka koje Rentier obrađuje

| Vrsta dohotka       | IBKR CSV sekcija                  | Tretman u srpskom porezu |
|---------------------|-----------------------------------|--------------------------|
| **Dividende**       | `Dividends`                       | Oporezive po stopi od 15%; inostrani WHT umanjuje srpsku obavezu |
| **Pripisana kamata**| `Interest` (Credit Interest redovi)| Oporeziva po stopi od 15%; inostrani WHT može se primeniti |

> **Odbitna kamata** (npr. kamata na marginu koju plaćate IBKR‑u) uvozi se iz izveštaja, ali ne generiše poresku prijavu — ne smatra se oporezivim dohotkom. Potvrdite ovo sa savetnikom.

---

## Poreska stopa

Srpski porez na pasivne prihode od kapitala iznosi **15%** bruto iznosa, preračunatog u dinare (RSD).

```
Srpski porez = bruto dohodak (RSD) × 15%
```

---

## Inostrani porez po odbitku (WHT)

Većina zemalja zadržava **porez na izvoru (WHT)** pre nego što sredstva stignu na vaš račun. Srbija omogućava da se ovaj porez kreditira prema domaćoj obavezi, čime se izbegava dvostruko oporezivanje.

Formula kredita:

```
Srpski porez = max(bruto srpski porez − WHT u RSD, 0)
```


- Kredit ne može biti veći od srpskog poreza.  
- Ako je inostrana stopa ≥ 15%, dodatni porez u Srbiji nije potreban.  
- Ako je niža (npr. 10% na američke dividende prema sporazumu Srbija–SAD), plaća se razlika u Srbiji.  
- WHT se vezuje za isplatu kojoj pripada: WHT na dividendu za tu dividendu, a WHT na pripisanu kamatu za kamatu tog meseca.

**Primer:**

| Stavka                        | Iznos        |
|-------------------------------|--------------|
| Bruto dividenda               | $100 USD     |
| NBS kurs (USD→RSD)            | 108.50       |
| Bruto dohodak                 | 10,850 RSD   |
| Srpski porez (15%)            | 1,627.50 RSD |
| Inostrani WHT (10%)           | 1,085 RSD    |
| **Srpski porez za uplatu**    | **542.50 RSD** |

---

## Kursni listovi

Svi iznosi se prijavljuju u **RSD**. Rentier koristi **srednji kurs NBS** na datum dohotka.

- Ako kurs za taj dan nije objavljen (vikend/praznik), koristi se poslednji prethodni radni dan.  
- Kursevi se čuvaju u lokalnoj SQLite bazi radi optimizacije.  
- IBKR kursevi (`Base Currency Exchange Rate`) koriste se samo za valute koje NBS uopšte ne objavljuje: kurs iz izveštaja prema osnovnoj valuti računa × NBS kurs te osnovne valute. Takve prijave su označene kao obračunate unakrsnim kursom. Valuta koju NBS objavljuje nikada ne prelazi na IBKR kurs — čak ni kada NBS kurs nije pronađen u periodu unazad; ta prijava tada prijavljuje grešku.
- Ako NBS **nije dostupna** (prekid mreže ili servisa), Rentier nikada ne koristi kurs nekog ranijeg dana niti IBKR kurs — NBS kurs za taj dan možda postoji. Umesto toga, ništa iz tog izveštaja se ne čuva, a izveštaj se automatski ponovo obrađuje pri sledećoj sinhronizaciji. Posle 3 neuspela pokušaja označava se kao **Greška**, uz prikazan razlog, kako bi mogao da se pregleda.

---

## Rok prijave

PP‑OPO mora biti podnet u roku od **30 dana** od datuma prihoda. Ako rok pada na neradni dan ili praznik, pomera se na prvi sledeći radni dan.

**Primer:**

| Datum prihoda | Rok (+30) | Prilagođeni rok |
|---------------|-----------|-----------------|
| 2024‑03‑01    | 2024‑03‑31 (nedelja) | 2024‑04‑01 (ponedeljak) |
| 2024‑04‑15    | 2024‑05‑15 (sreda)   | 2024‑05‑15 |
| 2024‑04‑30    | 2024‑05‑30 (četvrtak)| 2024‑05‑31 (petak, praznik) |

---

## Srpski praznici

Rentier koristi konfigurisan kalendar (`HolidayConf`). Standardni praznici:

| Datum        | Praznik             |
|--------------|---------------------|
| 1–2. januar  | Nova godina         |
| 7. januar    | Pravoslavni Božić   |
| 15–16. februar | Dan državnosti    |
| 1–2. maj     | Praznik rada        |
| 11. novembar | Dan primirja        |

> Pravoslavni Uskrs je pokretni praznik i mora se ažurirati svake godine prema zvaničnom kalendaru. Proverite zvanični kalendar na [www.gov.rs](https://www.gov.rs).

---

## Životni ciklus prijave

Svaki prihod prolazi kroz sledeće faze:

```
Init → Filed → Paid
```


| Status   | Značenje                                |
|----------|-----------------------------------------|
| **Init** | Rentier je izračunao prijavu; XML spreman za izvoz |
| **Filed**| XML podnet na ePorezi portalu           |
| **Paid** | Porez uplaćen                           |

---

## Podnošenje PP‑OPO

Rentier generiše XML u skladu sa šemom Poreske uprave (`http://pid.purs.gov.rs`). Postupak:

1. Izvezite XML (**Export PP‑OPO XML**).  
2. Prijavite se na [ePorezi](https://www.purs.gov.rs/e-porezi.html).  
3. Izaberite **PP‑OPO → Nova prijava**.  
4. Otpremite XML.  
5. Potvrdite i podnesite.  

Svaka isplata zahteva zasebnu prijavu.

---

## Često postavljena pitanja

- **Moram li podneti PP‑OPO ako je WHT već ≥ 15%?**  
  Da, prijava je obavezna i u informativne svrhe.  

- **Šta ako sam primio dividende u više valuta?**  
  Svaka se konvertuje nezavisno po kursu NBS.  

- **Šta ako NBS nema kurs za valutu?**  
  Ako NBS uopšte ne objavljuje valutu, unakrsni kurs se računa iz IBKR kursa iz izveštaja (vidi Kursni listovi). Ako ni on nije dostupan — ili NBS objavljuje valutu, ali kurs nije pronađen — prijava prijavljuje grešku i unosi se ručno.  

- **Postoji li ugovor Srbija–SAD?**  
  Da, stopa je obično 10% (umesto 30%), što se kreditira u Srbiji.  

- **Šta ako zakasnim sa prijavom?**  
  Rentier ne blokira podnošenje; kazne određuje Poreska uprava.  

- **Šta ako broker ispravi dividendu?**  
  Rentier detektuje korekciju i označava je u logu; prijava se ručno prilagođava. Duplikati se ignorišu. Isto pravilo važi i za ručni unos: dodavanje prijave za isplatioca i datum za koje prijava već postoji (bez obzira na velika i mala slova) se odbija — izmenite ili obrišite postojeću prijavu.  

- **Šta ako red u izveštaju ne može da se pročita?**  
  Ostatak izveštaja se i dalje obrađuje, ali se izveštaj označava kao **Delimična greška** (ili **Greška** ako nijedna prijava nije kreirana), a nečitljiv red se navodi u logu sinhronizacije, tako da nijedan dohodak ne bude prećutno izostavljen iz prijava.

---

## Dodatno čitanje

- [Poreska uprava Srbije — PP-OPO obrazac](https://www.purs.gov.rs) (pretražite "PP-OPO")
- [ePorezi portal](https://www.purs.gov.rs/e-porezi.html)
- [NBS kursni listovi](https://www.nbs.rs/kurs-liste/kursna-lista)
- [Ugovor Srbije i SAD o izbegavanju dvostrukog oporezivanja](https://www.mfin.gov.rs) — pretražite bilateralne ugovore

---

## Videti i

- [Prvi koraci](PRVI-KORACI.md) — instalirajte Rentier i pokrenite prvi uvoz
- [IBKR Activity Statement instalacija](IBKR-INSTALACIJA.md) — generišite ispravan CSV iz Interactive Brokers-a
