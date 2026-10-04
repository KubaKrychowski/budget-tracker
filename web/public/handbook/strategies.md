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

Na tablicy układasz **kafelki** i łączysz je strzałkami. W panelu „Dodaj kafelek” po prawej są trzy rodzaje kafelków:
przeciągnij je na tablicę albo kliknij **„+”** — kafelek dodany plusem trafia obok zaznaczonego i od razu się z nim łączy.
**Konkretny rodzaj wybierasz w ustawieniach kafelka**, w polu „Rodzaj” (po kliknięciu kafelka):

- **Zdarzenie** — to, co dzieje się w konkretnym miesiącu albo stanowi punkt wyjścia:
  - *Zdarzenie* (podwyżka, koniec zlecenia) — samo nic nie zmienia, uruchamia łańcuch akcji,
  - *Wpływ jednorazowy* (premia, wyrównanie) i *Wydatek jednorazowy* (ubezpieczenie) — dopisują albo odejmują
    kwotę od oszczędności w swoim miesiącu,
  - stan wyjściowy: *Nadwyżka miesięczna* (stała kwota odkładana co miesiąc), *Kredyt* (saldo, oprocentowanie
    roczne i rata — z niego bierze się „dług” w symulacji) i *Poduszka docelowa* (kwota, którą chcesz uzbierać).
- **Akcja** — co robisz po zdarzeniu. „Liczy symulacja”: *Zwiększ nadwyżkę*, *Nadpłać kredyt* (kwota i tryb: obniż ratę
  albo skróć okres), *Spłać resztę kredytu*. „Zakłada w budżecie”: *Załóż cel oszczędzania*, *Załóż rezerwację*,
  *Ustaw limit kategorii*, *Zakończ zlecenie stałe* i *Dodaj wydatek jednorazowy* (zlecenie epizodyczne). Cztery pierwsze
  w symulacji nic nie zmieniają; wydatek jednorazowy odejmuje kwotę od gotówki w miesiącu, w którym akcja się wykona.
  Każda z nich ma w ustawieniach ramkę „Zastosuj w aplikacji” — mówi, co założy w budżecie.
- **Warunek** — *Warunek* (porównuje oszczędności i dług, ma dwa wyjścia: **tak** i **nie**), *Czekaj*
  (sprawdź ponownie w następnym miesiącu) i *Koniec* (znacznik „strategia zrealizowana”).

Zmiana rodzaju zostawia pola, które nowy rodzaj też ma (np. kwotę), a strzałki, które przestają pasować, znikają — na
przykład zdarzenie nie przyjmuje strzałki wchodzącej, a „Koniec” nie ma wychodzącej. Po wpływie jednorazowym nie da się
wybrać limitu kategorii (premia nie zmienia wydatków).

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

### Zastosuj w budżecie

Przycisk **„Zastosuj w budżecie…”** zakłada w budżecie to, co planują akcje z grupy „Zakłada w budżecie”: miesięczny cel
oszczędzania, rezerwacje, limity kategorii, zakończenie zlecenia stałego i wydatki jednorazowe. Okno liczy **zapisaną**
strategię (przy niezapisanych zmianach przycisk jest wyłączony) i przy każdej akcji pokazuje status:

- **nowa** — założy nowy obiekt, **zmiana** — zmieni to, co już jest (inna kwota celu albo limitu, zakończenie zlecenia),
- **już jest** — to samo już jest w budżecie, więc akcja jest pominięta (dzięki temu dwukrotne zastosowanie niczego nie zdubluje),
- **czeka** — miesiąc akcji w symulacji jeszcze nie nadszedł (np. limit po spłacie kredytu), zastosujesz ją później,
- **uzupełnij** — akcja ma problem na tablicy (brakuje kwoty, kategorii albo zlecenia).

Zaznaczone akcje są stosowane **razem albo wcale** — błąd jednej cofa wszystkie. Cel miesięczny działa jak na ekranie
„Cele oszczędzania” (podniesienie od bieżącego miesiąca, obniżenie od następnego), a limit jak na ekranie limitów
(historia zamkniętych miesięcy się nie zmienia). Zastosowania **nie da się cofnąć jednym kliknięciem** — zmiany cofniesz
na ekranach celów, rezerwacji, limitów i zleceń.

### Zapis

Zmiany na tablicy nie zapisują się same — przycisk **„Zapisz strategię”** wysyła całość, a **„Odrzuć zmiany”**
wraca do ostatniego zapisu. Przycisk **„Parametry”** zmienia nazwę, miesiąc startu, oszczędności na początku
i horyzont. Usunięcie strategii nie cofa niczego, co wcześniej założyłeś z niej w budżecie.
