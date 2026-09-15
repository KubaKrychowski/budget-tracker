## Zlecenia epizodyczne

Zlecenie epizodyczne to zaplanowany, jednorazowy wydatek (np. „Nowy laptop") — następca dawnej flagi
„duży wydatek". Ma dwa stany:

- **Zaplanowane** — kategoria, orientacyjna kwota i termin, zanim wydatek się wydarzy.
- **Zrealizowane** — wskazuje konkretną transakcję z importu; kwotę, datę i kategorię bierze WŁAŚNIE
  z niej, nie z planu. Plan zostaje widoczny nawet po realizacji, żeby dało się cofnąć „do
  zaplanowanych", jeśli oznaczenie było pomyłką.

### Realizację można oznaczyć z dwóch miejsc

Z listy transakcji („Oznacz jako zrealizowane zlecenie") albo z samego ekranu zleceń. Oznaczenie z
listy transakcji nie wymaga wskazania budżetu — serwer bierze budżet z samej transakcji, bo lista
może pokazywać kilka budżetów naraz.

### Powiązanie z celami oszczędzania

„Załóż cel oszczędzania" przy zleceniu epizodycznym tworzy zwykłą rezerwację na ekranie Cele
oszczędzania, którą to zlecenie **rządzi**: zmiana kwoty lub terminu planu przepisuje nierozliczoną
rezerwację, usunięcie zlecenia ją zabiera, a „Oznacz jako kupione" rozlicza ją zakupem — nie
wypłatą z oszczędności, więc zwykłe warunki wypłaty z ekranu rezerwacji tu nie obowiązują.

Jeśli transakcja zrealizowanego zlecenia zniknie (np. przez reset budżetu), zlecenie wraca do stanu
ukrytego, a nie kasowanego — przywrócenie transakcji przywraca też je.
