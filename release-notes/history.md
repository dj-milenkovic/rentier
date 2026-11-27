<!--
  User-facing release notes for versions released before PR descriptions carried a
  "## Release note" section. The release workflow merges these with the notes read from
  PR descriptions; the app shows the last 10 versions. Versions with no user-relevant
  changes (v1.0.12, v1.1.14, v1.1.16: dependency updates and internal work only) are left out.
-->

## v1.3.1

### EN
- Rentier can now update itself. When a new version is out, a bar at the top of the window offers to download and install it. This works with the new auto-updating downloads, the ones with "velopack" in the file name.

### SR
- Rentier sada može sam da se ažurira. Kada izađe nova verzija, traka na vrhu prozora nudi da je preuzme i instalira. Ovo radi sa novim paketima koji se sami ažuriraju, onima koji u nazivu fajla imaju „velopack“.

## v1.3.0

### EN
- New **Settings → Diagnostics** page. Rentier keeps a daily log file to help with troubleshooting, and leaves personal and financial data out of it. The page has a switch for more detailed logging and a button that opens the logs folder.
- Statement rows that cannot be read are no longer skipped without a warning. The report is marked **Partial Error** or **Error**, and the row is shown in the sync log.
- Foreign tax withheld on IBKR credit interest is now credited against the Serbian tax.
- Exchange rates for currencies that NBS does not publish are now correct for accounts whose base currency is not USD.
- Adding a manual filing for income that is already filed, with a different amount, no longer creates a second filing.
- PP-OPO export: field 3.3 (Ostalo) is never empty. If the payment notes are left blank, "Isplata na brokerski račun" is used.

### SR
- Nova stranica **Podešavanja → Dijagnostika**. Rentier vodi dnevnik za svaki dan koji pomaže pri otklanjanju problema, i u njega ne upisuje lične ni finansijske podatke. Na stranici je prekidač za detaljnije beleženje i dugme koje otvara fasciklu sa dnevnicima.
- Redovi izvoda koji ne mogu da se pročitaju više se ne preskaču bez upozorenja. Izveštaj se označava kao **Delimična greška** ili **Greška**, a red se prikazuje u dnevniku sinhronizacije.
- Porez plaćen u inostranstvu na kamatu koju isplaćuje IBKR sada se uračunava u porez u Srbiji.
- Kursevi za valute koje NBS ne objavljuje sada su tačni i za račune čija osnovna valuta nije USD.
- Ručni unos prijave za prihod koji je već prijavljen, sa drugačijim iznosom, više ne pravi drugu prijavu.
- Izvoz PP-OPO: polje 3.3 (Ostalo) nikad nije prazno. Ako napomena o isplati ostane prazna, koristi se „Isplata na brokerski račun“.

## v1.2.0

### EN
- The app version is now shown at the bottom of the sidebar.
- The user guide is now also available in Serbian.

### SR
- Verzija programa sada se prikazuje na dnu bočnog menija.
- Uputstvo za korisnike sada postoji i na srpskom jeziku.

## v1.1.18

### EN
- Filings: the payment reference can now be entered for **Filed** and **Paid** filings. Before, anything typed into it was silently discarded.
- The **Settings** group in the sidebar now opens and closes on every click, not only on every second one.

### SR
- Prijave: referenca plaćanja sada može da se unese za prijave sa statusom **Podneto** i **Plaćeno**. Ranije se sve što je otkucano tiho odbacivalo.
- Grupa **Podešavanja** u bočnom meniju sada se otvara i zatvara na svaki klik, a ne samo na svaki drugi.

## v1.1.17

### EN
- Fixed: clicking the sidebar did nothing, and the Dashboard, Filings, Reports and Settings pages showed no data.

### SR
- Ispravljeno: klik na bočni meni nije imao efekta, a stranice Kontrolna tabla, Prijave, Izveštaji i Podešavanja nisu prikazivale podatke.

## v1.1.15

### EN
- Tax is now calculated correctly for income with no foreign tax withheld.
- Manual filings have a new **Payment notes** field, used in the PP-OPO export.
- The Windows installer is now included in the release downloads.

### SR
- Porez se sada tačno računa za prihod na koji nije plaćen porez u inostranstvu.
- Ručne prijave imaju novo polje **Napomena o isplati**, koje se koristi pri izvozu PP-OPO.
- Instalacioni program za Windows sada je deo preuzimanja uz izdanje.

## v1.1.13

### EN
- Exchange rates calculated from the IBKR statement are checked before use, so an invalid rate can no longer end up in a filing.

### SR
- Kursevi izračunati iz IBKR izvoda proveravaju se pre upotrebe, pa neispravan kurs više ne može da završi u prijavi.
