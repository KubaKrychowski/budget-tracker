## Import wyciągu z banku

Zamiast przepisywać wydatki ręcznie, wgrywasz plik z historią konta, a aplikacja sama go odczytuje i podpowiada kategorie. **Niczego nie zapisujemy, dopóki nie potwierdzisz** — najpierw dostajesz podgląd.

### Czego potrzebujesz

- Pliku **CSV** pobranego z bankowości internetowej (zwykle w sekcji „Historia”, opcja „Eksportuj”).
- Banku z listy obsługiwanych: **PKO BP** lub **mBank**. Kolejne banki można dodać bez zmian w reszcie aplikacji.
- Utworzonego i **aktywnego** budżetu, na który trafią transakcje.

### Krok po kroku

Import to kreator z czterema krokami: **Konto → Plik CSV → Podgląd → Gotowe**. Wejdziesz w niego z pulpitu („Zaimportuj wyciąg”) albo wpisując „import” w pasku wyszukiwania.

#### 1. Konto

![Krok 1: wybór banku i budżetu](handbook/images/import-step1.png)

Wybierz **bank**, z którego pochodzi wyciąg (dzięki temu sami rozpoznamy format pliku — nic nie ustawiasz ręcznie) i **budżet**, na który mają trafić zmiany. Gdy oba pola są wypełnione, odblokuje się przycisk **Dalej**.

#### 2. Plik CSV

![Krok 2: wgranie pliku](handbook/images/import-step2.png)

Kliknij pole albo **przeciągnij plik** w jego obszar. Wgrywasz jeden plik CSV naraz.

#### 3. Podgląd

![Krok 3: podgląd wczytanych transakcji z proponowanymi kategoriami](handbook/images/import-step3.png)

Tu dzieje się najważniejsze. Dla każdej transakcji widzisz datę, nazwę, kwotę i **proponowaną kategorię**. Nad tabelą jest podsumowanie, np. „Do zapisania: 3 · do weryfikacji: 3 · duplikatów: 0”.

W kolumnie **Status** zobaczysz:

- 🟢 **Poprawne** — aplikacja jest pewna kategorii (kolumna **Trafność** pokazuje, jak bardzo),
- 🔵 **Do weryfikacji** — aplikacja nie jest pewna; wybierz kategorię z listy lub zostaw na później.

Możesz też wyszukać transakcję, zaznaczyć kilka i użyć **Akceptuj zaznaczone** (zatwierdzasz podpowiedziane kategorie) albo **Usuń zaznaczone** — takie wiersze **nie trafią do importu** (nic nie dzieje się z Twoim kontem w banku).

Przy każdym wierszu jest też menu **⋮** z dodatkowymi akcjami, np. edycją wiersza przed zapisem.

#### 4. Gotowe

Przycisk **Dalej** w podglądzie **zapisuje import** — zapisane zostaje dokładnie to, co zostało w tabeli. Potem dostajesz podsumowanie. Jeśli w podglądzie nie został żaden wiersz do zaimportowania, przycisk jest nieaktywny.

### Co dzieje się z każdą transakcją

1. Model kategoryzacji ocenia, jaka kategoria pasuje, i **jak bardzo jest tego pewny**.
2. **Wysoka pewność** → kategoria przypisuje się sama. **Niska** → transakcja trafia na listę „Do przeglądu”.
3. Każda Twoja poprawka **uczy model** — im więcej poprawisz, tym rzadziej będziesz musiał to robić w przyszłości.
4. W tym samym kroku aplikacja dopasowuje **zlecenia stałe** i, jeśli budżet ma budżet oszczędnościowy, oznacza **własne przelewy** jako „Transfer”.

### Duplikaty

Ten sam wyciąg wgrany **dwa razy na ten sam budżet** zostanie rozpoznany jako duplikat i pominięty — nie zdublujesz więc wydatków, nawet jeśli pliki się nakładają. Wgranie go na **inny** budżet nie jest duplikatem.

### Najczęstsze problemy

| Co widzisz | Co zrobić |
|---|---|
| Lista budżetów jest pusta albo brakuje budżetu | Budżet może być wyłączony — włącz go w **Ustawienia → Budżety**. Import na wyłączony budżet się nie powiedzie. |
| Dużo transakcji „Do weryfikacji” | To normalne przy pierwszym imporcie. Poprawiaj kategorie — model szybko się uczy. |
| Plik nie jest rozpoznawany | Sprawdź, czy wybrałeś właściwy bank i czy plik to CSV wyeksportowany prosto z bankowości. |
