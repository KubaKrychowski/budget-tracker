## Zlecenia stałe

Zlecenie stałe to **wydatek, który wraca regularnie** — czynsz, abonament telefonu, rata kredytu. Dzięki niemu aplikacja wie, które transakcje z wyciągu to Twoje „stałe opłaty”, i pokazuje, **które już zapłaciłeś w tym miesiącu, a które jeszcze czekają**.

![Lista zleceń stałych z rytmem, statusem miesiąca i menu akcji (Przejdź do powiązanych, Zmień, Zakończ, Usuń)](handbook/images/standing-orders-list.png)

### Jak dodać zlecenie

Wejdź w **Zlecenia stałe** i kliknij **Dodaj zlecenie**.

![Formularz zlecenia stałego z regułą dopasowania i podpowiedzią, ile transakcji pasuje](handbook/images/standing-orders-form.png)

1. **Nazwa** — np. „Telefon i internet”.
2. **Kwota zwykle** — ile zazwyczaj płacisz.
3. **Rytm** — co miesiąc, co kwartał albo co rok.
4. **Reguła dopasowania** — po czym aplikacja ma rozpoznać tę opłatę:
   - **Tytuł zawiera** — fragment opisu z wyciągu (np. „telekom”),
   - **Kwota od – do** — dopuszczalny **zakres**, nie jedna liczba.
5. Zapisz — kliknij **Zapisz zlecenie**.

> 💡 **Dlaczego zakres kwoty?** Dzięki niemu podwyżka (np. czynszu) nadal zostanie rozpoznana jako ta sama opłata. Możesz dodać kilka reguł — transakcja należy do zlecenia, gdy pasuje do **którejkolwiek** z nich.

Nad formularzem pojawi się podpowiedź, ile transakcji z historii pasuje do reguły (np. „Reguły pasują do 4 transakcji z historii budżetu”) — to dobry sposób, żeby sprawdzić, czy reguła nie jest za szeroka albo za wąska.

### Co zobaczysz na ekranie

- Kafle na górze: **Stałe zlecenia miesięcznie**, **Zeszło w tym miesiącu**, **Czeka do zapłaty**, **Inna kwota niż zwykle**.
- Niebieski baner z listą opłat, które w tym miesiącu **jeszcze czekają**.
- W tabeli — status miesiąca, np. „Czeka · zwykle do 14.”.
- Po prawej lista **Ostatnio przypięte transakcje**, z linkiem **Odepnij** przy każdej.
- Strzałkami **‹ ›** przełączasz miesiące.

### Menu akcji (⋯)

| Akcja | Do czego służy |
|---|---|
| **Przejdź do powiązanych** | Otwiera listę transakcji, które należą do tego zlecenia. |
| **Zmień** | Edytuje nazwę, kwotę, rytm i reguły. |
| **Zakończ** | Kończy zlecenie od wybranego miesiąca (np. po spłacie kredytu). |
| **Usuń** | Usuwa zlecenie. |

> ✅ **Zakończ zamiast usuwać.** Zakończone zlecenie przestaje być oczekiwane i liczone w podsumowaniu, a import przestaje do niego przypinać nowe transakcje — ale **historia dopasowań zostaje**.

### Ważne: zlecenie tylko „przypina”

⚠️ Dopasowanie **nie zmienia kategorii** transakcji. To celowe — historia z banku ma zostać nietknięta. Kolumna „Kategoria” na liście pokazuje **najczęstszą kategorię wśród przypiętych transakcji**, a nie kategorię samego zlecenia.

- Dopasowanie działa **wstecz**: zapisanie lub zmiana reguły przelicza całą dotychczasową historię budżetu.
- Działa też **przy każdym kolejnym imporcie**.

### Pomyłka? Odepnij

Jeśli reguła złapała transakcję niesłusznie, kliknij **Odepnij** przy niej na liście „Ostatnio przypięte transakcje”. Ręczne odpięcie jest **zapamiętywane na stałe** — późniejsza zmiana reguły nie przypnie jej z powrotem (chyba że przypniesz ją do innego, bardziej pasującego zlecenia).
