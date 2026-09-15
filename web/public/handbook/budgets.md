## Budżety

Budżet to **abstrakcyjny zbiór zasad grupowania zaimportowanych transakcji** — nie „konto" ani
„miesiąc kalendarzowy". Każdy budżet ma nazwę, walutę, miesiąc początkowy i saldo początkowe, i od
tego momentu zbiera importowane wyciągi, limity, zlecenia i cele oszczędzania.

### Po co więcej niż jeden budżet

Kilka budżetów pozwala porównać warianty tych samych danych (np. „co by było, gdyby ten wydatek
trafił do innej kategorii") albo rozdzielić różne konta bankowe. Import CSV działa **w obrębie
budżetu** — ten sam wyciąg da się wgrać na inny budżet bez konfliktu duplikatów.

### Powiązany budżet oszczędnościowy

Jeśli masz osobne konto oszczędnościowe, zaimportuj je jako **osobny budżet** i połącz je z
budżetem głównym przy tworzeniu (opcja „Dodaj połączony budżet oszczędnościowy") albo później w
edycji. Powiązanie pozwala:

- oznaczyć w Ustawieniach **regułę przelewu** (fragment tytułu + zakres kwoty), która rozpoznaje
  własne przelewy między obydwoma budżetami — takie transakcje nie liczą się do wydatków ani
  przychodów żadnego z nich (ale zostają w historii transakcji, oznaczone jako „Transfer");
- zobaczyć na ekranie **Cele oszczędzania** prawdziwy bilans konta oszczędnościowego, zamiast
  szacunku z samej kategorii.

Bez powiązania ekran celów oszczędzania jest zablokowany — samo oznaczenie kategorii „Oszczędności"
nie wystarcza do policzenia realnego stanu konta.

### Cykl życia budżetu (Ustawienia → Budżety)

| Operacja | Co robi | Co zostaje |
|---|---|---|
| **Wyłączenie** | zamyka budżet na nowy import i nowe transakcje | wszystko, budżet nadal widoczny |
| **Reset** | usuwa transakcje, importy i limity | nazwa, waluta, miesiąc, saldo, cel i rezerwacje |
| **Usunięcie** | to co reset, plus cele i rezerwacje | budżet wraca po przywróceniu przez określony czas |
| **Przywrócenie** | cofa usunięcie budżetu i jego danych | — |

Wyłączenie **nie** blokuje edycji, resetu, usunięcia ani przywrócenia — to celowe, żeby dało się z
niego wyjść bez ponownego włączania.
