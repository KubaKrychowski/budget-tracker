## Import wyciągu z banku

Import wczytuje eksport CSV z banku, dopasowuje kolumny i od razu proponuje kategorię dla każdej
transakcji.

### Obsługiwane banki

Aplikacja rozpoznaje format pliku automatycznie. Obsługiwane są eksporty **PKO BP** i **mBanku** —
kolejne banki można dodać bez zmian w reszcie mechanizmu importu.

### Co dzieje się z każdym wierszem

1. Wiersz trafia do podglądu, zanim cokolwiek zostanie zapisane.
2. Model kategoryzacji ocenia, jaka kategoria pasuje, razem z pewnością tej oceny.
3. Przy wysokiej pewności transakcja dostaje kategorię automatycznie. Przy niskiej — trafia na
   listę „do przeglądu", gdzie poprawka od razu doszkala model na przyszłość.
4. Zlecenia stałe i (jeśli budżet ma powiązany budżet oszczędnościowy) reguła transferu przypinają
   się do pasujących transakcji w tym samym kroku co zapis.

### Duplikaty liczą się w obrębie budżetu

Ten sam wyciąg wgrany dwa razy na TEN SAM budżet zostanie rozpoznany jako duplikat i pominięty.
Wgranie go na **inny** budżet nie jest traktowane jak duplikat — budżety służą też do porównywania
wariantów tych samych danych.

### Budżet musi być aktywny

Import na wyłączony budżet się nie powiedzie — trzeba go najpierw włączyć w Ustawieniach.
