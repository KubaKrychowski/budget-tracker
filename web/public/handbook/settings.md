## Ustawienia i reguły

Do Ustawień wejdziesz kołem zębatym w prawym górnym rogu. Ekran ma kilka zakładek — poniżej te, które dotyczą budżetów i kategoryzacji.

### Budżety

Lista wszystkich Twoich budżetów, z saldem, miesięcznym limitem, liczbą transakcji i statusem. Menu **⋮** przy budżecie pozwala go edytować, wyłączyć, zresetować, usunąć oraz ustawić **reguły powiązania** z budżetem oszczędnościowym. Opis każdej akcji znajdziesz w temacie **Budżety**.

### Dane treningowe

Zakładka pokazuje, **czego uczy się model kategoryzacji**: ile przykładów pochodzi z pliku bazowego, ile z Twoich poprawek i jak rozkładają się po kategoriach.

![Dane treningowe: liczba przykładów, przycisk „Doucz model” i rozkład po kategoriach](handbook/images/settings-training.png)

- **Doucz model** — model uczy się na Twoich najnowszych poprawkach. Niebieski komunikat obok mówi, ile poprawek przybyło od ostatniego treningu.
- **Przelicz kategorie** — stosuje aktualny model i reguły do transakcji, które już są w bazie. Kategorie nadane przez Ciebie ręcznie lub potwierdzone zostają nietknięte, a transakcje, co do których model nie ma zdania, wracają do kolejki „Do przeglądu”. Przed przeliczeniem aplikacja poprosi o potwierdzenie.
- Kategorie bez ani jednego przykładu są wypisane w żółtej ramce — **model nigdy ich nie wskaże**, dopóki nie dodasz przykładów (albo reguły).

> ℹ️ Podane trafności są orientacyjne i nie są bramką jakości — każdy trening dzieli zbiór inaczej, więc wyższy wynik niż poprzednio nie znaczy automatycznie „lepszy model”.

### Reguły predykatu

To reguły kategoryzacji, które działają **przed** modelem uczenia maszynowego. Mówisz w nich: „jeśli opis zawiera X, to kategoria Y” — niezależnie od tego, czego nauczył się model. Przydają się do:

- stałych sprzedawców, którzy zawsze powinni trafiać do jednej kategorii,
- **przychodów** (np. wynagrodzenie, zwroty), bo model ich w ogóle nie ocenia.

![Lista reguł posortowana według priorytetu, z opisem wzorca, typu operacji i kategorii](handbook/images/settings-rules.png)

Reguły stosują się **w kolejności priorytetu** — wygrywa pierwsza pasująca, dlatego lista jest posortowana priorytetem (im niższa liczba, tym wcześniej reguła się sprawdza). Jeśli dwie reguły mają ten sam priorytet i mogą pasować do tej samej transakcji, ekran ostrzeże Cię o **remisie** — nadaj im wtedy różne priorytety. Nową regułę dodasz przyciskiem **Nowa reguła**, a listę przeszukasz polem „Szukaj we wzorcach, kategoriach…”.

### Kategorie

To lista kategorii, do których przypisujesz transakcje.

![Lista kategorii z typem, zakresem (Wspólna) i liczbą użyć](handbook/images/settings-categories.png)

- **Kategorie wspólne** (oznaczone jako „Wspólna”) są dostępne dla wszystkich i **tylko do odczytu**.
- Możesz dodać **własne** — **Nowa kategoria** — widzisz je tylko Ty.
- Przy dodawaniu wybierasz **typ**: **wydatek** albo **wpływ**. Kategoria przychodowa przyjmuje tylko wpływy i nie ma limitów; kategoria wydatkowa przyjmie każdą transakcję, także zwrot zakupu.
- Nazwa musi być **unikalna** wśród Twoich i wspólnych kategorii.

Co możesz zrobić z własną kategorią:

- **Zmienić nazwę** — zawsze.
- **Zmienić typ albo usunąć** — tylko, gdy nie jest jeszcze używana. Kolumna **Użycie** pokazuje liczbę transakcji, reguł i limitów. Zajętą kategorię najpierw „opróżnij”, przenosząc jej transakcje, reguły i limity do innej.

Własnej kategorii możesz używać w regułach, limitach i przy ręcznym przypisywaniu transakcji.

### Konto i bezpieczeństwo

Zakładka z ustawieniami Twojego konta.

### Co nowego

Ikona **gwiazdki** w górnym pasku otwiera historię zmian w aplikacji. **Czerwona kropka** oznacza wydania, których jeszcze nie widziałeś — po aktualizacji okno „Co nowego” pokaże się też samo, raz.

Zamknięcie okna oznacza zmiany jako przeczytane **w tej przeglądarce**; w innej przeglądarce okno pojawi się ponownie.
