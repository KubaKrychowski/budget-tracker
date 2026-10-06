## Strategie: kredyt, nadpłata, poduszka

Strategia to **plan na kilkanaście miesięcy do przodu** — na przykład „nadpłacę kredyt premią i zbuduję poduszkę finansową”. Układasz go z klocków (kafelków) na tablicy, a aplikacja **na bieżąco liczy**, ile będziesz miał oszczędności i ile długu w każdym kolejnym miesiącu.

> 🧪 Strategia **niczego nie zmienia w Twoim budżecie**, dopóki sam o to nie poprosisz. To plan i symulacja, nie dane z wyciągów.

> ⚠️ To **szacunek do planowania, nie porada finansowa.** Odsetki liczone są według rzeczywistej liczby dni (rok = 365 dni), więc bank może naliczać inaczej o kilka złotych.

### Szybki start: wypróbuj szablon

Najłatwiej zacząć od gotowego przykładu:

1. Wejdź w **Strategie** (pasek wyszukiwania albo menu „Wszystkie funkcje”).
2. W ramce **Nowa strategia** kliknij **Z szablonu: kredyt i poduszka**.
3. Podmień przykładowe liczby na własne — kliknij kafelek i wpisz swoje kwoty.

![Lista strategii wybranego budżetu z przyciskami Otwórz i Usuń oraz ramką „Nowa strategia”](handbook/images/strategies-list.png)

Strategie należą do **budżetu** — przełącznik budżetu na górze pokazuje strategie wybranego. Zamiast szablonu możesz też zacząć od **pustej tablicy**.

### Tablica i kafelki

![Tablica strategii: kafelki połączone strzałkami, panel „Dodaj kafelek” po prawej i wynik symulacji na dole](handbook/images/strategies-board.png)

Na tablicy układasz **kafelki** i łączysz je strzałkami. Czytasz ją od lewej do prawej: najpierw **zdarzenie** („co się dzieje i kiedy”), potem **akcja** („co z tym robię”), a w razie potrzeby **warunek** („jeśli… to…”).

W panelu **Dodaj kafelek** po prawej są trzy rodzaje. Przeciągnij kafelek na tablicę albo kliknij **+** — taki kafelek pojawi się obok zaznaczonego i od razu się z nim połączy. **Konkretny rodzaj wybierasz w ustawieniach kafelka**, w polu „Rodzaj”.

- **Zdarzenie** — punkt wyjścia albo coś, co dzieje się w konkretnym miesiącu:
  - *Zdarzenie* (podwyżka, koniec zlecenia) — samo nic nie zmienia, uruchamia łańcuch akcji,
  - *Wpływ jednorazowy* (premia) i *Wydatek jednorazowy* (ubezpieczenie) — dodają albo odejmują kwotę w swoim miesiącu,
  - stan wyjściowy: *Nadwyżka miesięczna* (stała kwota odkładana co miesiąc), *Kredyt* (saldo, oprocentowanie roczne i rata) i *Poduszka docelowa* (kwota, którą chcesz uzbierać).
- **Akcja** — co robisz po zdarzeniu.
  - **Liczy symulacja:** *Zwiększ nadwyżkę*, *Nadpłać kredyt* (kwota i tryb: obniż ratę albo skróć okres), *Spłać resztę kredytu*.
  - **Zakłada w budżecie:** *Załóż cel oszczędzania*, *Załóż rezerwację*, *Ustaw limit kategorii*, *Zakończ zlecenie stałe* i *Dodaj wydatek jednorazowy*.
- **Warunek** — *Warunek* (porównuje oszczędności i dług, ma dwa wyjścia: **tak** i **nie**), *Czekaj* (sprawdź ponownie w następnym miesiącu) i *Koniec* (znacznik „strategia zrealizowana”).

### Ustawienia kafelka

Kliknij kafelek, żeby zobaczyć jego ustawienia po prawej: rodzaj, podpis, kwoty, miesiąc. Pod ustawieniami zobaczysz, **kiedy w symulacji kafelek się wykonał** (np. „Wykonany: maj 2027”).

![Ustawienia zaznaczonego kafelka akcji „Nadpłać kredyt”: rodzaj, podpis, kwota nadpłaty i tryb rozliczenia](handbook/images/strategies-tile.png)

Zmiana rodzaju zostawia pola, które nowy rodzaj też ma (np. kwotę), a strzałki, które przestają pasować, znikają. Na przykład zdarzenie nie przyjmuje strzałki wchodzącej, a „Koniec” nie ma wychodzącej.

### Łączenie kafelków

Strzałkę prowadzisz od kropki na **prawej** krawędzi kafelka do kropki na **lewej** krawędzi następnego:

- **myszą** — przeciągnij,
- **klawiaturą** — zaznacz kafelek, naciśnij **C**, strzałkami wybierz cel i potwierdź **Enterem**.

**Warunek** ma dwie kropki: górna to „tak”, dolna to „nie”. Strzałkę lub kafelek usuniesz klawiszem **Delete** albo przyciskiem w panelu. Tablicę przesuwasz przeciąganiem tła, a kółkiem myszy ją przybliżasz.

### Jak liczy się symulacja

Symulacja idzie miesiąc po miesiącu — od miesiąca startu przez wybrany horyzont (domyślnie 24 miesiące). W każdym miesiącu kolejność jest taka:

1. zaczyna się kredyt (jeśli to jego miesiąc),
2. wykonują się zdarzenia tego miesiąca i akcje, które z nich wynikają,
3. naliczają się odsetki i spływa rata,
4. dopisuje się nadwyżka miesięczna,
5. na końcu sprawdzane są warunki i akcje, które z nich wynikają.

Dzięki temu nadpłata w maju liczy odsetki od niższego salda już za maj, a podwyżka ze stycznia działa od stycznia.

> ℹ️ **Rata zmniejsza dług, ale nie odejmuje oszczędności** — zakładamy, że nadwyżka jest już liczona po racie. Nadpłata w trybie „obniż ratę” zmniejsza ratę proporcjonalnie do spłaconej części długu, a w trybie „skróć okres” zostawia ratę bez zmian.

Gdy warunek nie jest spełniony i prowadzi przez „Czekaj”, zostanie sprawdzony jeszcze raz w następnym miesiącu — to jedyna dozwolona pętla.

Na dole tablicy widać **wynik**: kiedy kredyt zostanie spłacony, kiedy osiągniesz poduszkę (dopiero gdy dług jest zerowy) i ile będzie oszczędności na koniec.

### Problemy na tablicy

Kafelek z czerwonym znacznikiem **!** ma problem, a nad tablicą pojawia się baner z ich liczbą. Najczęstsze:

- brakuje parametru (kwoty, miesiąca, progu warunku),
- żadna strzałka nie prowadzi do kafelka, więc symulacja go nie wykona,
- zdarzenie nie ma żadnej akcji, a „Czekaj” nie wraca do warunku,
- kafelki tworzą pętlę, która nie przechodzi przez „Czekaj”,
- jest drugi kredyt albo druga poduszka — liczony jest tylko pierwszy z każdego.

Symulacja **pomija** kafelki z problemami, ale szkic zapisuje się mimo to.

### Zastosuj w budżecie

Plan możesz **zamienić w prawdziwe zmiany** przyciskiem **Zastosuj w budżecie…**. Założy on w budżecie to, co planują akcje z grupy „Zakłada w budżecie”: miesięczny cel oszczędzania, rezerwacje, limity kategorii, zakończenie zlecenia stałego i wydatki jednorazowe.

![Okno „Zastosuj strategię w budżecie” z listą akcji i ich statusami](handbook/images/strategies-apply.png)

Okno liczy **zapisaną** strategię (przy niezapisanych zmianach przycisk jest wyłączony) i przy każdej akcji pokazuje status:

| Status | Znaczenie |
|---|---|
| **nowa** | Założy nowy obiekt w budżecie. |
| **zmiana** | Zmieni to, co już jest (inna kwota celu albo limitu, zakończenie zlecenia). |
| **już jest** | To samo już jest w budżecie — akcja zostanie pominięta (dwukrotne zastosowanie niczego nie zdubluje). |
| **czeka** | Miesiąc akcji w symulacji jeszcze nie nadszedł (np. limit po spłacie kredytu) — zastosujesz ją później. |
| **uzupełnij** | Akcja ma problem na tablicy (brakuje kwoty, kategorii albo zlecenia). |

Zaznacz akcje, które chcesz zastosować, i potwierdź. Są stosowane **razem albo wcale** — błąd jednej cofa wszystkie.

> ⚠️ **Zastosowania nie da się cofnąć jednym kliknięciem.** Zmiany cofniesz osobno na ekranach celów, rezerwacji, limitów i zleceń.

### Zapis

Zmiany na tablicy **nie zapisują się same**:

- **Zapisz strategię** — wysyła całość,
- **Odrzuć zmiany** — wraca do ostatniego zapisu,
- **Parametry** — zmienia nazwę, miesiąc startu, oszczędności na początku i horyzont.

Usunięcie strategii nie cofa niczego, co wcześniej założyłeś z niej w budżecie.

### Terminal (CLI)

Wszystko, co robisz na tablicy, da się zrobić w terminalu komendami `strategy` (temat **Terminal**): `strategy list`, `get`, `create`, `save`, `simulate`, `delete`, `references`, `apply-preview` i `apply`.
