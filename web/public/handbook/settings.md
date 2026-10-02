## Ustawienia i reguły

Ekran ma kilka zakładek — poniżej te, które dotyczą budżetów i kategoryzacji.

### Budżety

Lista wszystkich budżetów z akcjami cyklu życia — edycja, wyłączenie, reset, usunięcie i
przywrócenie. Opisy operacji i różnice między nimi znajdziesz w temacie „Budżety". Stąd też
dodajesz i zmieniasz **regułę powiązania z budżetem oszczędnościowym** (fragment tytułu przelewu +
zakres kwoty) dla budżetów, które taki budżet mają połączony.

### Dane treningowe

Podgląd tego, na czym uczy się model kategoryzacji: Twoje dotychczasowe poprawki kategorii. Więcej
danych treningowych zwykle oznacza mniej transakcji trafiających „do przeglądu" w przyszłości.

### Reguły predykatu

Reguły kategoryzacji, które działają **przed** modelem uczenia maszynowego — dopasowanie fragmentu
opisu (albo typu operacji) do konkretnej kategorii, niezależnie od tego, czego nauczył się model.
Przydają się do oczywistych, powtarzalnych przypadków (stały sprzedawca zawsze w tej samej
kategorii) oraz do kategoryzowania przychodów, których model w ogóle nie ocenia.

![Lista reguł predykatu posortowana priorytetem, z ostrzeżeniem o remisie priorytetów](handbook/images/settings-rules.png)

### Kategorie

Lista kategorii, do których przypisujesz transakcje. Obok **kategorii wspólnych** (oznaczonych jako „Wspólna"),
dostępnych dla wszystkich kont i tylko do odczytu, możesz dodać **własne** — widzisz je tylko Ty.

Przy dodawaniu wybierasz **typ**: wydatek albo wpływ. Kategoria przychodowa przyjmuje wyłącznie wpływy
(przypisanie do niej wydatku zostanie odrzucone) i nie ma limitów; kategoria wydatkowa przyjmie każdą
transakcję, także zwrot zakupu. Nazwa musi być unikalna wśród Twoich i wspólnych kategorii.

- **Zmiana nazwy** jest zawsze możliwa.
- **Zmiana typu i usunięcie** są możliwe tylko wtedy, gdy kategoria nie jest jeszcze używana — kolumna
  „Użycie" pokazuje liczbę transakcji, reguł i limitów. Zajętą kategorię najpierw opróżnij, przenosząc
  jej transakcje, reguły i limity do innej.
- Własnej kategorii możesz użyć w **regułach predykatu**, limitach i przy ręcznym przypisywaniu transakcji.
