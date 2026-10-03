## Strategie

Strategia to plan na kilkanaście miesięcy do przodu — na przykład „nadpłacę kredyt premią i zbuduję poduszkę”.
Układasz go z klocków na tablicy, a aplikacja liczy z nich, ile będziesz miał oszczędności i ile długu
w każdym kolejnym miesiącu. Strategia **niczego nie zmienia w budżecie**: to plan i symulacja, nie dane z wyciągów.

> To **szacunek do planowania, nie porada finansowa.** Odsetki liczone są według rzeczywistej liczby dni
> (rok 365 dni), bank może naliczać inaczej o kilka złotych.

### Lista strategii

W menu „Wszystkie funkcje” wejdź w **Strategie**. Strategie należą do budżetu — przełącznik budżetu na górze
pokazuje strategie wybranego. Nową strategię zakładasz **pustą** albo z szablonu „Kredyt i poduszka”. Liczby
w szablonie są przykładowe — wpisz własne w kafelkach.

### Tablica i kafelki

Na tablicy układasz **kafelki** i łączysz je strzałkami. Kafelki bierzesz z panelu „Dodaj kafelek” po prawej:
przeciągnij je na tablicę albo kliknij **„+”** — kafelek dodany plusem trafia obok zaznaczonego i od razu
się z nim łączy. Są trzy zakładki:

- **Zdarzenia** — to, co dzieje się w konkretnym miesiącu albo stanowi punkt wyjścia:
  - *Zdarzenie* (podwyżka, koniec zlecenia) — samo nic nie zmienia, uruchamia łańcuch akcji,
  - *Wpływ jednorazowy* (premia, wyrównanie) i *Wydatek jednorazowy* (ubezpieczenie) — dopisują albo odejmują
    kwotę od oszczędności w swoim miesiącu,
  - *Nadwyżka miesięczna* — stała kwota odkładana co miesiąc,
  - *Kredyt* — saldo, oprocentowanie roczne i rata; to z niego bierze się „dług” w symulacji,
  - *Poduszka docelowa* — kwota, którą chcesz uzbierać.
- **Akcje** — co robisz po zdarzeniu: *Zwiększ nadwyżkę*, *Nadpłać kredyt* (kwota i tryb: obniż ratę albo skróć
  okres), *Spłać resztę kredytu*, a także *Załóż cel oszczędzania*, *Załóż rezerwację*, *Zakończ zlecenie stałe*
  i *Ustaw limit kategorii*. Te cztery ostatnie to akcje „do zastosowania w budżecie” — w symulacji nic nie zmieniają.
- **Warunki** — *Warunek* (porównuje oszczędności i dług, ma dwa wyjścia: **tak** i **nie**), *Czekaj*
  (sprawdź ponownie w następnym miesiącu) i *Koniec* (znacznik „strategia zrealizowana”).

Kliknij kafelek, żeby zobaczyć jego ustawienia: podpis, miesiąc, kwoty. Pod ustawieniami widać, **kiedy
w symulacji kafelek się wykonał** albo kiedy warunek został spełniony.

### Łączenie kafelków

Strzałkę prowadzisz od kropki na prawej krawędzi kafelka do kropki na lewej krawędzi następnego — myszą
(przeciągnij) albo klawiaturą: zaznacz kafelek, naciśnij **C**, strzałkami wybierz cel i **Enter**. Warunek ma dwie
kropki: górna to „tak”, dolna to „nie”. Zdarzenia i kafelki stanu wyjściowego nie mają wejścia (nic do nich nie
prowadzi), a „Koniec” nie ma wyjścia.

Strzałkę lub kafelek usuniesz klawiszem **Delete** albo przyciskiem w panelu. Tablicę przesuwasz przeciąganiem tła,
a kółkiem myszy przybliżasz.

### Jak liczy się symulacja

Symulacja idzie miesiąc po miesiącu, od miesiąca startu przez wybrany horyzont (domyślnie 24 miesiące).
W każdym miesiącu kolejność jest taka:

1. zaczyna się kredyt (jeśli to jego miesiąc),
2. wykonują się zdarzenia tego miesiąca i akcje, które z nich wynikają,
3. naliczają się odsetki i spływa rata,
4. dopisuje się nadwyżka miesięczna,
5. na koniec sprawdzane są warunki i akcje, które z nich wynikają.

Dzięki temu nadpłata w maju liczy odsetki od niższego salda już za maj, a podwyżka ze stycznia działa od stycznia.

**Rata zmniejsza dług, ale nie odejmuje oszczędności** — przyjmujemy, że nadwyżka jest już liczona po racie.
Nadpłata w trybie „obniż ratę” zmniejsza ratę proporcjonalnie do spłaconej części długu, a w trybie „skróć okres”
zostawia ratę bez zmian. Gdy warunek nie jest spełniony i prowadzi przez „Czekaj”, zostanie sprawdzony jeszcze raz
w następnym miesiącu — to jedyna dozwolona pętla.

Na dole tablicy widać **wynik**: kiedy kredyt zostanie spłacony, kiedy osiągniesz poduszkę (dopiero gdy dług jest
zerowy) i ile będzie oszczędności na koniec.

### Problemy na tablicy

Kafelek z czerwonym znacznikiem **!** ma problem, a nad tablicą pojawia się baner z ich liczbą. Najczęstsze:

- brakuje parametru (kwoty, miesiąca, progu warunku),
- żadna strzałka nie prowadzi do kafelka, więc symulacja go nie wykona,
- zdarzenie nie ma żadnej akcji, a „Czekaj” nie wraca do warunku,
- kafelki tworzą pętlę, która nie przechodzi przez „Czekaj”,
- jest drugi kredyt albo druga poduszka — liczony jest tylko pierwszy z każdego.

Symulacja **pomija** kafelki z problemami, ale szkic zapisuje się mimo to.

### Zapis

Zmiany na tablicy nie zapisują się same — przycisk **„Zapisz strategię”** wysyła całość, a **„Odrzuć zmiany”**
wraca do ostatniego zapisu. Przycisk **„Parametry”** zmienia nazwę, miesiąc startu, oszczędności na początku
i horyzont. Usunięcie strategii nie cofa niczego, co wcześniej założyłeś z niej w budżecie.
