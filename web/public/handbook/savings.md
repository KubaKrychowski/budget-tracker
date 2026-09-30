## Cele oszczędzania i rezerwacje

Ekran pokazuje stan konta oszczędnościowego, cel do niego przypisany oraz rezerwacje — kawałki tego
stanu odłożone z myślą o konkretnym wydatku.

![Cele oszczędzania: dowód celu, historia miesięcy oraz rezerwacje z wolnymi środkami](handbook/images/savings-overview.png)

### Warunek wejścia

Ekran jest dostępny tylko dla budżetu, który ma **powiązany budżet oszczędnościowy** (patrz temat
„Budżety"). Bez powiązania nie da się policzyć prawdziwego bilansu konta — ekran poprosi najpierw
o dodanie powiązania w edycji budżetu.

### Stan oszczędności

To bieżący bilans powiązanego budżetu oszczędnościowego — nie osobne, ręcznie wpisywane pole i nie
suma samych wpłat. Rośnie i maleje razem z transakcjami zaimportowanymi na ten budżet.

### Rezerwacje i wolne środki

- Rezerwacja **nie przenosi pieniędzy** — tylko dzieli już istniejący stan konta na kawałki
  z etykietą celu.
- **Uzbierane** w rezerwacji to suma RĘCZNYCH wpłat, które zarejestrujesz na ekranie — nie
  automatyczne rozłożenie stanu konta wg terminów.
- W oknie „Wpłać" wybierasz, **skąd wpłacasz**: z konta oszczędnościowego albo ze zwykłego.
- Wpłata z **konta oszczędnościowego** ma dwa ograniczenia: nie więcej, niż brakuje do kwoty
  rezerwacji, i nie więcej, niż faktycznie zostało na koncie po wpłatach z oszczędności na inne,
  wciąż otwarte rezerwacje.
- Wpłata ze **zwykłego konta** wymaga wybrania **kategorii limitu**. Pieniądze zostają na koncie,
  ale są na nim zarezerwowane na cel, a kwota wlicza się do limitu tej kategorii w miesiącu
  wpłaty (o ile kategoria ma limit). Okno pokazuje, ile będzie po wpłacie — przekroczenie limitu
  tylko ostrzega, nie blokuje. Takie wpłaty nie tworzą transakcji: wydatek i tak pojawi się
  z wyciągu banku. Ze zwykłego konta ogranicza cię tylko kwota brakująca do celu, bo aplikacja nie
  zna jego stanu.
- Rozliczona rezerwacja przestaje wliczać swoje wpłaty ze zwykłego konta do limitu — zakup, którym
  ją rozliczasz, jest już prawdziwą transakcją w swojej kategorii.
- **Wolne środki = stan konta minus nierozliczona część rezerwacji**, czyli jej pełna kwota bez
  tego, co pokryły wpłaty ze zwykłego konta. Wpłaty z oszczędności nie zmieniają wolnych środków —
  dopiero rozliczenie (albo usunięcie) rezerwacji je zwalnia.
- Ile jest **zarezerwowane na zwykłym koncie**, widać w nagłówku ekranu rezerwacji i na pulpicie
  w podsumowaniu (kafelek pojawia się tylko wtedy, gdy coś jest zarezerwowane).

### Rezerwacje z zleceń epizodycznych

„Załóż cel oszczędzania" przy zleceniu epizodycznym tworzy rezerwację rządzoną przez to zlecenie —
więcej w temacie „Zlecenia epizodyczne".
