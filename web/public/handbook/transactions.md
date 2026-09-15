## Transakcje i kategorie

Lista transakcji jest **1:1 z tym, co przyszło z banku** — import nigdy nie zmienia opisu ani
kwoty. Jedyna rzecz, którą aplikacja dopisuje, to kategoria (i ewentualne przypięcia: zlecenie
stałe, zlecenie epizodyczne, transfer na budżet oszczędnościowy).

![Lista transakcji z kategoriami, statusem dopasowania i masowymi akcjami dla zaznaczonych wierszy](handbook/images/transactions-list.png)

### Jak działa kategoryzacja

Podejście jest hybrydowe: **reguły** łapią oczywiste przypadki (np. konkretny sprzedawca zawsze w
tej samej kategorii) i uczą model na starcie, a **model uczenia maszynowego** zajmuje się resztą,
biorąc pod uwagę opis, kwotę i to, czy to wydatek, czy przychód.

- Pewność powyżej progu → kategoria trafia na transakcję automatycznie.
- Pewność poniżej progu → transakcja ląduje na liście „Do przeglądu".
- Każda Twoja poprawka (także zatwierdzenie podpowiedzianej kategorii) doszkala model — im więcej
  poprawek, tym rzadziej trzeba poprawiać kolejne.
- Przychody dostają kategorię wyłącznie z reguł, nie z modelu — model uczy się tylko na wydatkach.

### Transfer na budżet oszczędnościowy

Jeśli budżet ma powiązany budżet oszczędnościowy, przelewy między nimi są oznaczone znacznikiem
„Transfer" i wyłączone z sum wydatków/przychodów — ale kategoria i opis zostają nietknięte, zgodnie
z zasadą „historia 1:1 z bankiem". Znacznik da się ręcznie zdjąć przez menu akcji przy transakcji
(„Odepnij transfer"), jeśli reguła dopasowała się niesłusznie.

### Filtrowanie z innych ekranów

Wejście z ekranu zleceń stałych albo epizodycznych filtruje listę do transakcji tego konkretnego
zlecenia i pokazuje okruszek „Przejdź do powiązanych" z powrotem.
