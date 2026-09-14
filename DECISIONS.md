# Dziennik decyzji — Budżet tracker

> Dawny `CLAUDE.md`. Odwołania „CLAUDE.md §N” w komentarzach kodu wskazują sekcje TEGO pliku.
> Instrukcje pracy (komendy, konwencje, definicja „gotowe”) są w `CLAUDE.md`, `api/CLAUDE.md` i `web/CLAUDE.md`.
>
> Dokument przekazania kontekstu (handoff). Powstał z sesji projektowej, w której ustalono
> analizę biznesową, architekturę, model ML, stack frontendowy i zakres MVP.
> Czytaj to jako źródło decyzji już podjętych — zanim zaproponujesz zmianę, sprawdź,
> czy dana rzecz nie była świadomie rozstrzygnięta poniżej (z uzasadnieniem).
> Nowa decyzja albo zmiana starej = blok `> **REWIZJA — RRRR-MM-DD: temat**` w sekcji, której dotyczy.

---

## 1. Czym jest projekt

Osobisty tracker wydatków. **Single-user MVP**, z opcją produktu dla innych „może kiedyś"
(personal-first, product-optional).

**Problem użytkownika:** nie śledzi wydatków systematycznie. Dzisiejszy workaround to wrzucanie
Excela do AI ad-hoc, żeby zobaczyć „gdzie ucieka kasa". To działa, ale kosztuje (za każdym razem),
jest ulotne (nic nie zostaje między analizami) i wyłącznie retrospektywne.

**Propozycja wartości:** produktyzacja pętli „zbierz dane → dostań wgląd" przez usunięcie jej
najgorszych części. Import CSV zastępuje ręczne składanie Excela; trwały dashboard z kategoryzacją ML
zastępuje jednorazową analizę. Dochodzi trwałość (historia, pamięć o dużych wydatkach) i planowanie w przód.

**Prawdziwy konkurent:** nie YNAB ani apka bankowa — własny obecny nawyk użytkownika (Excel→AI, nieregularnie).
MVP musi być **wyraźnie mniej uciążliwy** od tego, inaczej nie ma racji bytu. To ryzyko egzystencjalne, nie kosmetyczne.

**Dane wejściowe:** 2 konta bankowe + 1 karta lunchowa. Integracja z bankiem (API) **odrzucona** —
rdzeniem jest import plików CSV eksportowanych ręcznie z bankowości.

**⚠️ OTWARTE:** użytkownik zasygnalizował, że „aplikacja będzie dużo większa" niż obecny MVP,
ale nie sprecyzował wizji. Kierunki do dopytania: więcej funkcji dla tego samego usera / multi-user /
więcej źródeł danych / mądrzejszy ML / skala techniczna. To wpływa na rewizję odroczeń (multi-user, SQLite,
separacja domeny) — patrz sekcja 4. **Nie zakładaj zakresu docelowego — dopytaj.**

---

## 2. Zakres i etapy (buduj przyrostowo, nie all-at-once)

Kolejność nie jest dowolna — każdy etap żywi się danymi z poprzedniego.

### Etap 0 — RDZEŃ (to budujemy teraz), 5 procesów:
1. **Import CSV** (z 2 kont; karta lunchowa/gotówka ręcznie)
2. **Dodaj transakcję ręcznie** (zawiera „oznacz duży wydatek" jako flagę)
3. **Popraw kategorię** (kolejka „do przeglądu" → korekta → douczanie ML)
4. **Oznacz duży wydatek** (flaga na transakcji, nie osobny ekran)
5. **Przeglądaj wydatki** — dashboard „gdzie ucieka kasa" (agregacja per kategoria)

### Etap 1 — po 1–2 mies. historii:
- Budżety / limity na kategorie, sprawdzanie stanu budżetu

### Etap 2 — po kilku mies. danych:
- Prognozy, planowanie/śledzenie większych i nadchodzących wydatków

**Metodyka:** iteracyjnie/przyrostowo, w duchu kanban (przepływ, nie sprinty; solo-dev po godzinach).
Znamy cel (diagramy use case + klas = mapa docelowa), ale projektujemy szczegółowo i implementujemy
tylko to, co aktualnie „ciągniemy". Nie budujemy na zapas rzeczy, których kształtu nie znamy bez danych.

---

## 3. Kategoryzacja / ML

**Podejście hybrydowe (NIE czyste ML):** reguły + ML.
- **Reguły** łapią oczywiste przypadki (np. „Biedronka"→Jedzenie, „Orange"→Rachunki) i służą do
  **bootstrapu** — generują pierwsze etykiety, gdy nie ma jeszcze danych treningowych (problem zimnego startu).
- **ML** zajmuje się resztą.

**Pętla aktywnego uczenia:** model przewiduje kategorię + pewność → jeśli pewność **≥ próg (0.7)**
→ auto-kategoria; jeśli **< próg** → status „do przeglądu" → użytkownik poprawia → poprawka staje się
danymi treningowymi → retrening. Każda korekta usera (także zatwierdzenie/zmiana auto-kategorii) douczają model.

**Model — klasyczne ML, NIE deep learning** (opisy transakcji to krótki, zaszumiony tekst):
- TF-IDF na **char n-gramach (3–5)** + word n-gramach → **klasyfikator liniowy** (Regresja Logistyczna /
  w ML.NET: `SdcaMaximumEntropy`).
- Cechy: znormalizowany opis + **kwota** + znak (przychód/wydatek), ewentualnie dzień miesiąca.
- Normalizacja opisu przed wektoryzacją: lowercase, usuń daty/numery kart/ciągi cyfr, zbij białe znaki.
- Char n-gramy są kluczowe: odporność na literówki i sklejone kody sprzedawców („BIEDRONKA1234WAW").

**Implementacja: ML.NET in-process** (ta sama aplikacja co API), za interfejsem `ICategorizer`.
Model zapisany do pliku `.zip`, ładowany przez `PredictionEnginePool`. **Bez osobnego mikroserwisu**
(zbędna złożoność przy single-user). `FeaturizeText` z jawnie włączonymi char-gramami (`FeaturizeTextOptions`).

Przewaga sytuacyjna: user = jednocześnie deweloper i użytkownik → idealna pętla zwrotna, model douczają się
szybko na jego własnych danych.

> **REWIZJA — 2026-09-02.** Model widzi **wyłącznie wydatki**. Zbiór treningowy (1224 wiersze)
> nie zawiera ani jednego wpływu, a wszystkie kategorie bazowe są wydatkowe — więc zapytany
> o wynagrodzenie klasyfikator i tak musi wskazać którąś z nich. Na realnym wyciągu robił to
> z pewnością ~1.0 (wypłata → „Gastronomia"), czyli **powyżej progu**, więc taka transakcja
> nie trafiała nawet do przeglądu. Wysoka pewność nie znaczy tu „trafione" — znaczy
> „pytanie było źle zadane".
> - `HybridCategorizer` nie kieruje kwot dodatnich do modelu. Brak reguły = brak kategorii.
> - Wpływy obsługują wyłącznie reguły, przez `CategoryRule.Direction` (Any/Expense/Income).
> - `CategoryRule.TransactionTypePattern` dopasowuje **typ operacji**, nie opis. Bez tego nie da
>   się złapać wypłaty z bankomatu: jej opisem jest adres bankomatu, nierozróżnialny od zakupu.
>   Ta reguła musi mieć **niski priorytet** — bankomat stojący w sklepie inaczej wyląduje
>   w „Jedzeniu", bo wygra wzorzec opisowy.
> - Kategorie przychodowe: `Wynagrodzenie`, `Zwroty`, `Przychody inne`. Wyprowadzone z danych
>   (81 wpływów dzieli się dokładnie tak), nie wymyślone. Ostatnia jest świadomie jednym workiem —
>   z wyciągu nie wynika, kto i za co przelał.
>
> ⚠️ Gdyby kiedyś model miał obsłużyć też wpływy, trzeba **najpierw** je oetykietować.
> Samo zdjęcie bramki przywróci ten sam błąd.

---

## 4. Architektura backendu (.NET)

**Wybór: Vertical Slice na Minimal APIs.** NIE pełny Clean/Onion, NIE klasyczne N-tier.
- Clean = koszt (warstwy, mapowania, ceremonia) płacony z góry, zwrot dopiero przy dużym zespole/długim życiu — przerost dla solo MVP.
- N-tier = tanie na start, ale logika rozłazi się po wspólnych serwisach przy rozroście featurami.
- Vertical slice = kod wokół funkcji, nie warstw; nowy feature = nowy folder, bez grzebania w reszcie.

**Dwa twarde szwy za interfejsami** (tylko tam, gdzie realnie będzie druga implementacja):
- `IStatementParser` — parser per bank (2 banki + karta → formaty będą się mnożyć).
- `ICategorizer` — reguły dziś, ML jutro, może inny model potem.
- **NIE** robimy repository-per-entity (brak drugiej implementacji = ceremonia bez zysku).

**Lekki CQRS bez CQRS:** komendy zmieniają stan, zapytania czytają — wychodzi naturalnie ze slice'ów
(`ImportHandler` = komenda, `ListTransactionsHandler`/dashboard = query). **Bez MediatR** — endpoint woła
handler wprost. Pełny CQRS (osobne modele read/write, event sourcing) **odrzucony** — overkill dla single-user.
Rewizja: osobny read-model (SQL views / tabele zmaterializowane) rozważyć DOPIERO, gdy agregacje dashboardu/prognoz
staną się kosztowne.

**Dane:** EF Core + **PostgreSQL 18** (provider `Npgsql.EntityFrameworkCore.PostgreSQL`), uruchamiany
lokalnie przez `docker-compose.yml` w korzeniu repo (`docker compose up -d db`).

> **REWIZJA — 2026-08-31.** Pierwotnie ustalono SQLite na MVP, z migracją na Postgres dopiero „gdy pojawi
> się multi-user". User odwrócił tę decyzję przed pierwszą linijką kodu — idziemy od razu na Postgres.
> Co to zmienia:
> - Baza jest **usługą, nie plikiem** — opis „jeden serwer + jeden plik" z diagramu komponentów jest
>   nieaktualny; uruchomienie projektu wymaga działającego Dockera.
> - Znikają za to pułapki późniejszej migracji: SQLite nie ma prawdziwego `decimal` (trzyma go jako
>   `TEXT`/`REAL`) ani typu `date`. Kwoty i daty trzeba by było przemapować — a najgorszy moment na taki
>   refaktor to ten, w którym masz już w bazie realną historię wydatków.
> - **Mapowanie typów (pilnuj tego):** `Kwota` → `numeric(18,2)`, nigdy `float`/`double`;
>   `Data` (`DateOnly`) → `date`; `Confidence` → `numeric(4,3)`; znacznik czasu importu → `timestamptz`.
> - Dochodzą narzędzia, których SQLite nie miał: indeksy GIN, `pg_trgm`, natywne `ILIKE` — przydatne przy
>   wyszukiwaniu po opisie sprzedawcy. **Nie używaj ich na zapas**, dopiero gdy zapytanie zacznie boleć.
> - Multi-user (sekcja niżej) pozostaje odroczony — Postgres go nie wprowadza, tylko przestaje blokować.

**Wyjątki domenowe → kody HTTP w jednym miejscu:** `DomainExceptionHandler` (`IExceptionHandler`
+ `app.UseExceptionHandler()`). Endpointy **nie** obudowują handlerów w `try/catch` — reguła
„nieznany `BusinessId` = 404" jest przekrojowa, a rozpisana przy każdej operacji prędzej czy
później gdzieś wypadnie i literówka w adresie da 500 zamiast 404.
- Handler mapuje TYLKO to, co zna, i przepuszcza resztę (`false`) — inaczej prawdziwa awaria
  udawałaby ładny 4xx.
- ⚠️ **`BadHttpRequestException` musi być w mapie.** Samo dołożenie `UseExceptionHandler`
  przechwytuje ten wyjątek, zanim host odczyta jego status — bez jawnej gałęzi zepsuty JSON
  w ciele żądania zaczyna zwracać **500 zamiast 400**. To nie jest hipoteza, tylko regresja
  złapana przy wdrażaniu tego handlera.
- ⚠️ Cena: kody odpowiedzi znikają z kodu endpointu. Dlatego endpointy deklarują je przez
  `Produces` — to jedyny ślad po kontrakcie, jaki zostaje przy `MapPost`.
- Endpoint MOŻE złamać domyślne mapowanie, jeśli ma powód. Import łapie `EntityNotFoundException`
  sam i zwraca **400**, nie 404: identyfikator budżetu przychodzi tam w parametrze/ciele,
  a nie w adresie zasobu, więc 404 mówiłby „nie ma endpointu importu".
- **Wyjątki nie noszą tekstów dla użytkownika.** Zdanie, które zobaczy człowiek, żyje w `.resx`;
  wyjątek niesie tylko typ i identyfikator. Ten sam tekst zapisany w obu miejscach rozjedzie się
  przy pierwszej poprawce.

**Multi-user — ODROCZONY, ale z tanim hakiem:** teraz BEZ `UserId`/logowania. Ale cały dostęp do danych
trzymamy w handlerach feature'ów, żeby później dołożyć multi-user przez EF Core **global query filters**
(dodanie kolumny + jeden globalny filtr). Opcjonalnie: `Domain` jako osobny projekt (nie tylko folder),
by chronić encje przed logiką i łagodzić główną pułapkę slice'ów (duplikacja/rozłażenie domeny).

> **REWIZJA — 2026-09-03: tożsamość, kasowanie, kaskady.** Trzy reguły przekrojowe, ustalone przy
> okazji projektowania ekranu ustawień budżetów. Obowiązują **wszystkie encje**, nie tylko budżet.
> Plan wdrożenia: `plans/business-id-soft-delete.md`.
> - **`BusinessId` (Guid) na każdej encji i to na nim operujemy.** `int Id` zostaje kluczem głównym
>   i obcym w bazie, ale **nie wychodzi z API** — kontrakty, adresy i front posługują się wyłącznie
>   `BusinessId`. Klucz zapisu i klucz publiczny to dwie różne rzeczy: pierwszy służy indeksom,
>   drugi ma być stabilny przy re-seedzie i przenoszeniu danych między środowiskami.
>   Generowanie: `Guid.CreateVersion7()` (uporządkowany w czasie — indeks rośnie na końcu,
>   zamiast fragmentować się losowo), nie `Guid.NewGuid()`.
> - **Soft delete zamiast kasowania.** Każda encja ma `DeletedAt` (`timestamptz`, `null` = żywy)
>   i globalny filtr EF `HasQueryFilter(e => e.DeletedAt == null)`. Usunięcie stempluje rodzica
>   i jego dzieci jawnie, w handlerze, w jednej transakcji. Dwa skutki uboczne, o których łatwo
>   zapomnieć: indeksy unikalne muszą stać się **częściowe** (`WHERE "DeletedAt" IS NULL`), inaczej
>   skasowany wiersz blokuje utworzenie nowego o tej samej nazwie; a deduplikacja importu **nie może**
>   widzieć skasowanych, inaczej reset budżetu cicho zablokuje ponowny import tego samego pliku.
> - **Kaskadowe usuwanie w bazie jest OSTATECZNOŚCIĄ, wszędzie.** Domyślnie `DeleteBehavior.Restrict`.
>   Skoro nic nie kasujemy fizycznie, kaskada nie ma czego robić, a zostawiona w konfiguracji jest
>   naładowaną bronią. Kaskada wchodzi w grę wyłącznie dla wiersza czysto podrzędnego, bez własnego
>   znaczenia poza rodzicem, i tylko gdy soft delete rodzica nie ma sensu. Taki przypadek dziś nie istnieje.
>
> **Stan wdrożenia (2026-09-03): zrobione.** Migracja `BusinessIdOnAllEntities`, wszystkie siedem
> encji, kontrakty i front. Szczegóły, o których warto wiedzieć przy dokładaniu kolejnej encji:
> - Oba pola siedzą w abstrakcyjnej klasie **`Entity`**, po której dziedziczą wszystkie encje.
>   `BusinessId` nadaje **inicjalizator właściwości**, nie interceptor — dzięki temu identyfikator
>   istnieje od razu po `new`, a nie dopiero po `SaveChanges`, więc da się go użyć przed zapisem.
>   ⚠️ `Entity` jest bazą CZYSTO KODOWĄ: EF jej nie mapuje, bo nie ma własnego `DbSet` i nic na nią
>   nie wskazuje nawigacją. Gdyby ktoś dodał taką nawigację, EF zrobi z tego hierarchię TPH
>   i **wszystkie tabele wpadną do jednej**.
> - Unikalny indeks i filtr globalny zakłada PĘTLA po `Model.GetEntityTypes()` w `AppDbContext` —
>   nowa encja dziedzicząca po `Entity` dostaje jedno i drugie bez dopisywania czegokolwiek.
>   Pilnuje tego test `Every_entity_has_business_id_and_soft_delete`.
> - `SoftDeleteInterceptor` odpowiada **wyłącznie** za zamianę `db.Remove(x)` na stempel `DeletedAt` —
>   tego jednego nie da się zrobić w encji, bo konstruktor nie przechwyci kasowania.
>   Musi być podpięty także w testach, jeśli test sprawdza kasowanie.
> - **Seedy nie mają wpisanych literałów Guid.** Identyfikator wyprowadza `DeterministicGuid.For(nazwa)`
>   (UUID v5 z nazwy): ta sama nazwa zawsze daje ten sam Guid, a dołożenie kategorii nie wymaga
>   dopisywania i pilnowania kolejnego literału. Zmiana `DeterministicGuid.Namespace` przestawia
>   WSZYSTKIE identyfikatory danych seedowanych.
> - Nieznany `BusinessId` = **404**, nigdy cichy fallback na pierwszy z brzegu — inaczej literówka
>   w adresie pokazywałaby liczby innego budżetu jako swoje.

### Struktura projektu (docelowa)
Repo jest **monorepo** — backend, frontend i infrastruktura w jednym miejscu, bo przy solo-devie jedna
zmiana kontraktu API i jej konsumpcja w UI powinny mieścić się w jednym commicie.

```
budget-tracker/                    # nazwa repozytorium; lokalny katalog może nazywać się inaczej
├── docker-compose.yml             # Postgres 18 — jedyna zależność infrastrukturalna
├── global.json                    # przypina SDK .NET 10
├── .env / .env.example            # dane dostępowe do bazy (.env poza repo)
├── web/                           # Angular 22 + NG-ZORRO (sekcja 7)
└── api/
    ├── BudgetTracker.slnx
    ├── tests/BudgetTracker.Api.Tests/
    └── src/BudgetTracker.Api/
        ├── Features/
        │   └── <Feature>/                 # Budgets, Categorization, Dashboard, Import, Savings, Transactions
        │       ├── <Feature>Module.cs     # DI + endpointy (+ zadania cykliczne feature'a)
        │       ├── Commands/              # <Verb><Noun>CommandHandler — operacje zmieniające stan
        │       ├── Queries/               # Get<Noun>QueryHandler — odczyty
        │       ├── Services/              # logika współdzielona przez handlery feature'a
        │       ├── Contracts/             # *RequestDto / *ResponseDto — to, co jedzie przez HTTP
        │       ├── Models/                # typy wewnętrzne feature'a (wejście modelu ML, wynik parsera)
        │       ├── Consts/                # enumy feature'a i stałe (numeracja od 1)
        │       └── Exceptions/            # wyjątki feature'a, jeden na plik
        ├── Domain/                # encje (Entity) i słowniki (DictionaryEntity) + Consts/ — enumy domenowe
        ├── Infrastructure/        # AppDbContext (EF Core + Npgsql), Migrations/, Jobs/ (Hangfire), Exceptions/
        └── Program.cs             # spina moduły: AddJobs().AddBudgets()…; UseJobsDashboard().UseBudgetJobs()
```

Rejestracja DI trzymana per slice (np. `BudgetsModule.AddBudgets()`); `Program.cs` tylko woła metody rozszerzające,
mapuje endpointy (`app.MapBudgets()` itd.) i rejestruje zadania cykliczne feature'ów (`app.UseBudgetJobs()`).
Dodanie banku = nowy `IStatementParser` + 1 linijka w `AddImport`.

> **REWIZJA — 2026-09-11: konwencja feature'a i zadania cykliczne** (plan `plans/backend-cqrs-hangfire-refactor.md`).
> - **Jeden handler na operację** z metodą `HandleAsync`: zapis = `*CommandHandler` w `Commands/`, odczyt =
>   `*QueryHandler` w `Queries/`. Endpoint dalej woła handler wprost — **bez MediatR**. Wspólne kroki kilku
>   handlerów idą do `Services/`, a nie do kopii w każdym handlerze.
> - **Jeden typ najwyższego poziomu na plik** — kontrakty, wyjątki, encje.
> - **Zadania cykliczne: Hangfire, nie `BackgroundService`.** Serwer w procesie API, storage w tym samym Postgresie
>   (schemat `hangfire`, poza migracjami EF). Zadanie rejestruje moduł feature'a. **Historia przebiegów jest audytem** —
>   dlatego `JobHistoryRetentionFilter` trzyma ją przez `Jobs:HistoryRetentionDays`; domyślnie Hangfire kasuje udane
>   przebiegi po dobie. Panel `/hangfire` tylko w Development.
> - **Komentarze w kodzie źródłowym: wyłącznie XML doc** typów i członków. Uzasadnienie pułapki idzie do `<remarks>`
>   członka, którego dotyczy — nie do komentarza w ciele metody.
>
> ⚠️ **Stan wdrożenia (2026-09-12): wszystkie feature'y przebudowane** — Budgets, Dashboard, Transactions, Import,
> Savings i Categorization mają `Commands/`, `Queries/`, `Services/`, `Contracts/` i `Exceptions/`, a jeden typ na plik
> obowiązuje też w `Domain/` i `Infrastructure/`. Ustawienia feature'a (`BudgetOptions`, `CategorizationOptions`) zostają
> w korzeniu feature'a, obok modułu. Krok wspólny dla kilku handlerów idzie do `Services/` — także jako klasa statyczna
> (`ReservationViews`, `CategoryRuleValidator`), jeśli nie ma własnego stanu.

> **REWIZJA — 2026-09-12: gdzie co leży** (plan `plans/backend-conventions-entities.md`).
> - **`Contracts/` to wyłącznie kontrakt HTTP**, a każdy typ w nim kończy się `RequestDto` albo `ResponseDto` —
>   także części składowe odpowiedzi (`DashboardMetricsResponseDto`, `CategorySpendResponseDto`). Sufiksy `*View`,
>   `*Result` i `*Option` nie istnieją. Nazwa typu mówi kierunek; JSON-a to nie zmienia.
> - **`Models/` to typy wewnętrzne feature'a**, które nigdy nie wychodzą przez API: `ParsedRow` (szew parsera),
>   `TransactionFeatures` i `CategoryPrediction` (wejście i wyjście ML.NET), `TrainingSet`, `ModelCatalog`.
>   Dzięki temu sufiks `*Dto` coś znaczy zamiast wisieć na wszystkim.
> - **`Consts/` trzyma enumy i stałe.** Enum domenowy, zapisywany w bazie, idzie do `Domain/Consts/'
>   (`TransactionStatus`, `RuleDirection`, `AccountType`); enum ekranowy do `Features/<Feature>/Consts/`
>   (`BudgetStatus` z konwerterem, `MonthVerdict`, `ReservationStatus`, `TransactionDirection`, `CreateBudgetError`).
>   Tam też lądują stałe bez zachowania (`TransactionLimits`).
> - **Wyjątki mieszkają w feature'ach**, a nie w `Domain`: `Features/<Feature>/Exceptions/`. Jedyny wyjątek
>   generyczny — `EntityNotFoundException` — stoi w `Infrastructure/Exceptions/`.
>   ⚠️ Konsekwencja: `Infrastructure/BudgetScope` zna `Features.Budgets.Exceptions`, bo rzuca
>   `BudgetNotFoundException`. To jedyne miejsce, gdzie Infrastructure sięga po feature — alternatywą byłby
>   wyjątek generyczny, który nie mówi, czego nie znaleziono.

---

## 5. Model domenowy (z diagramu klas)

- **Konto**: Id, Nazwa, Typ
- **Transakcja**: Id, Data (DateOnly), Kwota (decimal), Opis, DuzyWydatek (bool), CategoryId, Confidence (decimal), Status
- **Kategoria**: Id, Nazwa
- **Budzet**: Id, Miesiac        (Etap 1)
- **PozycjaBudzetu**: Limit      (łączy Budzet ↔ Kategoria; Etap 1)
- **Regula**: Wzorzec → Kategoria
- **ImportCSV**: Id, Data        (batch importu → wiele transakcji)
- **Predykcja**: Kategoria, Pewnosc   (osobno, bo trzyma pewność ML do progu)

**Relacje:** Konto 1→* Transakcja; ImportCSV 1→* Transakcja; Transakcja *→1 Kategoria;
Transakcja 1→1 Predykcja; Regula *→1 Kategoria; Budzet 1→* PozycjaBudzetu; PozycjaBudzetu *→1 Kategoria.

> **REWIZJA — 2026-09-01 (review PR #1).** Powyższe polskie nazwy encji opisują **pojęcia domenowe**,
> nie identyfikatory. **W kodzie wszystkie nazwy są po angielsku** — `Transaction`, `Category`,
> `Account`, `Budget`, `BudgetItem`. Komentarze pozostają po polsku.
> Teksty widoczne dla użytkownika nie mogą być zaszyte w kodzie: backend trzyma je w
> `Resources/SharedResource*.resx` (przez `IStringLocalizer`), frontend w `web/public/i18n/*.json`
> (przez `ngx-translate`). Kultura domyślna: `pl`.

**Stany transakcji (enum `TransactionStatus`):**
`Imported` → (`powyżej progu`) `AutoCategorized` / (`poniżej progu`) `PendingReview`
`PendingReview` → `ManuallyCategorized`; `AutoCategorized` → (`poprawka`) `ManuallyCategorized`;
oba → `Confirmed`. Stan mapuje się wprost na wygląd wiersza w tabeli (żółty status / zielony ptaszek / neutralny).

Do bazy trafia **nazwa** stanu (kod słownika `TransactionStatuses`) — zmiana nazwy wymaga migracji danych (rewizja niżej).

> **REWIZJA — 2026-09-11: słowniki w bazie.** Wartość z zamkniętej listy zapisywana w bazie to **słownik**:
> tabela dziedzicząca po `DictionaryEntity`, z czytelnym **kodem jako kluczem głównym** (bez `BusinessId`
> i soft delete), seedowana przez `HasData`. Kolumna odwołująca się do słownika trzyma **kod**, nie liczbowe
> ID — przy budżecie stoi `PLN` (kod ISO 4217, nietłumaczony), żeby przeglądając bazę nie trzeba było niczego
> sprawdzać. Strażnik `Every_entity_has_business_id_and_soft_delete` dopuszcza wyłącznie `Entity`
> i `DictionaryEntity` i wymaga seeda każdego słownika.
> - **Relacje bez nawigacji** — klucz obcy zostaje w bazie; dotyczy także `Category` i `Account`.
> - Enumy zostają w C# dla logiki, a do bazy mają trafiać jako kod. Zmiana nazwy członka enuma jest wtedy
>   dozwolona, ale **wymaga migracji danych**.
>
> - Słownik na enumie dziedziczy po `DictionaryEntity<TEnum>` (`TransactionStatusDictionary`, `RuleDirectionDictionary`,
>   `AccountTypeDictionary`), a waluta po `DictionaryEntity<string>`. ⚠️ Kod jest generyczny, bo EF zakłada klucz obcy
>   tylko między właściwościami **tego samego typu CLR** — słownik z kluczem `string` nie połączy się z kolumną typu enum.
>   Seed idzie z `Enum.GetValues`, więc nowa wartość enuma dostaje wiersz słownika w migracji wygenerowanej razem z nią.
> - Zmiana liczby na kod w istniejącej kolumnie to migracja **pisana ręcznie** (`ALTER COLUMN ... USING CASE`):
>   wygenerowane `AlterColumn` zapisałoby `'1'` zamiast `'AutoCategorized'` i klucz obcy odrzuciłby dane.
>
> ⚠️ **Stan wdrożenia:** słowniki zrobione (`Currency`, `TransactionStatuses`, `RuleDirections`, `AccountTypes`,
> migracja `EnumDictionaries`). Nawigacje usunięte (schemat bez zmian) — nazwę kategorii dociąga złączenie albo
> podzapytanie po `db.Categories`. Konsekwencja: wiersz nadrzędny, którego klucz jest potrzebny dzieciom (np. `ImportBatch`
> przy imporcie), zapisuje się pierwszym `SaveChanges` wewnątrz transakcji, bo bez nawigacji EF nie uzupełni klucza sam.

> **REWIZJA — 2026-09-12: encje z zachowaniem i numeracja enumów** (plan `plans/backend-conventions-entities.md`).
> - **Encja nie ma publicznych setterów.** Wszystkie właściwości są `{ get; protected set; }`, komplet wymaganych
>   pól idzie **konstruktorem**, a stan zmienia się **metodą o nazwie operacji**: `Budget.Disable(at)`,
>   `Transaction.Recategorize(categoryId, status, confidence)`, `SavingsReservation.Settle(at, transactionBusinessId)`,
>   `ImportBatch.CountImported()`, `Entity.MarkDeleted(at)` / `Restore()`. Pola, które muszą ruszać się razem,
>   mają jedną metodę — rozjazd kategorii, statusu i pewności jest niewidoczny na ekranie.
> - ⚠️ **Metody encji nie podejmują decyzji feature'a.** Encja przyjmuje trójkę (kategoria, status, pewność),
>   ale o tym, CO w nią wpisać, decyduje handler: próg pewności i `ManualCategoryCorrection` zostają w feature'rze.
>   Encja nie zna zegara, bazy ani progu — datę i czas dostaje w argumencie.
> - ⚠️ **EF materializuje encje PO KONSTRUKTORZE** (wiąże parametry po nazwach właściwości), więc nazwy parametrów
>   nie są dowolne, a zmiana nazwy parametru wywala materializację w runtime, nie w kompilacji. Słowniki też mają
>   konstruktor z kodem, a `HasData` buduje je tym konstruktorem.
> - **Deterministyczny `BusinessId` seedów** nadaje jawne `WithSeedBusinessId<T>(guid)` — jedyna droga, którą
>   identyfikator da się zmienić po utworzeniu, i wyłącznie dla danych seedowanych.
> - **Enumy numerujemy od 1**, jawnie przy każdym członie, i trzymamy w folderze `Consts` (patrz §4).
>   `0` przestaje być legalną wartością, więc pominięte pole w JSON-ie albo niezainicjalizowana właściwość nie
>   udaje świadomego wyboru. Na słownikach w bazie wychodzi to jako naruszenie klucza obcego — kod spoza słownika.
>   Złapane dwa razy przy tej zmianie: konto bez `Type` w teście i reguła bez `direction` w `data/category-rules.json`
>   (brak pola = `Any` jest teraz mapowany jawnie w `LocalRulesSeed`). Pilnuje tego `EnumConventionsTests`.

> **REWIZJA — 2026-09-02: czym jest budżet.** Powyższy opis („Budzet: Id, Miesiac" + pozycje
> z limitami) był niepełny i doprowadził do dwóch błędów. Ustalenie użytkownika:
>
> **Budżet to byt abstrakcyjny — zbiór zasad, na których pracują procesy wokół
> zaimportowanych transakcji.** Z tego wynika:
> - **Transakcje należą do budżetu** (`Transaction.BudgetId`), a import w kroku 1 wskazuje,
>   na który budżet naliczać.
> - **Budżet ma własny bilans.** `Budget.InitialBalance` to punkt startu;
>   `bilans(dzień) = InitialBalance + suma transakcji budżetu z datą ≤ dzień`.
>   Ujemny bilans początkowy jest dozwolony — debet to legalny stan konta.
> - **Kilka budżetów na ten sam miesiąc jest poprawne** — porównywanie wariantów tych samych
>   danych. Rozróżnia je NAZWA, nie okres. Nie dodawaj ograniczenia unikalności po miesiącu.
> - **Deduplikacja importu działa W OBRĘBIE BUDŻETU**, nie globalnie. To wynika wprost
>   z powyższego: skoro budżety służą do porównywania wariantów tych samych danych, ten sam
>   wyciąg MUSI dać się wgrać na drugi budżet. Przy zasięgu globalnym drugi wariant
>   przychodził pusty — każdy wiersz był „duplikatem" wiersza z pierwszego budżetu.
>   Dlatego `POST /api/import/preview` też przyjmuje `budgetId`: bez niego podgląd
>   pokazywałby inny wynik niż zapis.
> - **Dashboard liczy wszystko dla WYBRANEGO budżetu.** Wcześniej `budgetId` służył wyłącznie
>   do zsumowania limitów, więc przełączanie budżetu nie zmieniało żadnej liczby na ekranie.
> - Kafel i wykres „Stan budżetu" pokazują **bilans na dany dzień**, nie narastające wydatki
>   w okresie. Wykres startuje od bilansu otwarcia (`InitialBalance` + wszystko sprzed okna),
>   bo data nie przenosi pieniędzy.
> - `BudgetItem` (limity per kategoria) to nadal Etap 1 i **inna wielkość niż bilans** —
>   nie mieszaj ich na jednym wykresie.

> **REWIZJA — 2026-09-13: limity wydatków (Etap 1).** Decyzje użytkownika i to, co z nich wynika w kodzie
> (`Features/Limits`, ekran `/limits`, makieta Figma — strona „Limity wydatków"):
> - **Limit na KATEGORIĘ, a miesięczny limit budżetu to ich SUMA** — nie osobne pole. **Bez podpowiedzi kwot**:
>   prognozy to osobne zadanie (dlatego kreator budżetu już nie obiecuje „podpowiemy limity").
> - **Limit ma historię** (`BudgetItem.ValidFrom` / `ValidTo`), tak jak cel oszczędnościowy. Zmiana kończy poprzedni
>   limit miesiąc wcześniej i zakłada nowy; każdy miesiąc liczy się z limitem, który obowiązywał W NIM.
>   ⚠️ Każde miejsce sumujące `BudgetItems` musi filtrować po miesiącu — suma wszystkich wierszy liczy każdą zmianę
>   kwoty jako osobny limit (tak było w `BudgetListItemReader`, poprawione).
> - **Historia jest zamknięta**: zmiana istniejącego limitu wstecz to 409. Wyjątek: PIERWSZY limit kategorii wolno
>   zadeklarować wstecz (inaczej cała dotychczasowa historia zostaje „bez limitu").
> - **Próg ostrzeżenia ustawiany przy każdym limicie** (`WarningThreshold`, domyślnie 80%); stan paska liczony na kwotach,
>   nie na zaokrąglonym procencie.
> - **Do limitu liczą się wszystkie wydatki z kategorią, także oznaczone jako duże.** Wydatki bez kategorii nie liczą się
>   do żadnego limitu i ekran mówi o nich osobnym zdaniem. Kategorie przychodowe (wszystkie reguły `Income`) i
>   „Oszczędności" nie dostają limitu i nie wchodzą do „wydane poza limitami".
> - Przełącznik budżetu to wspólny `core/budget-switcher` — wybór idzie do ADRESU, bo adres wygrywa z `ActiveBudget`.

> **REWIZJA — 2026-09-14: zlecenia stałe** (etap 1 zgłoszenia #18 na tablicy; `Features/StandingOrders`, ekran
> `/standing-orders`, makieta Figma — strona „Zlecenia”):
> - **Zlecenie stałe to osobny byt** („Czynsz”) z regułą: tytuł ZAWIERA frazę (≥ 3 znaki, to dane — nie wzorzec LIKE)
>   + ZAKRES kwoty wydatku. Zakres, nie jedna kwota, bo czynsz po podwyżce dalej ma być czynszem.
> - ⚠️ **Zlecenie TYLKO SIĘ PRZYPINA** (`Transaction.StandingOrderBusinessId`) — **nie zmienia kategorii** (decyzja
>   użytkownika). Kolumna „Kategoria” na ekranie to najczęstsza kategoria PRZYPIĘTYCH transakcji.
> - Przypinanie **wstecz** (zapis/zmiana zlecenia przelicza całą historię budżetu) i **przy imporcie** (w tej samej
>   transakcji bazodanowej co zapis wierszy). Transakcja należy do jednego zlecenia — pierwsze wygrywa.
> - ⚠️ **Ręczne „Odepnij” jest pamiętane** (`StandingOrderUnpinnedFrom`) — bez tego każda zmiana reguły przypinałaby
>   transakcję z powrotem. Pamiętane jest KONKRETNE zlecenie, więc transakcja może trafić do innego.
> - Zlecenie jest ZASADĄ budżetu, jak cel oszczędnościowy: **reset je zostawia, usunięcie zabiera**, przywrócenie
>   oddaje, purge kasuje (`BudgetChildren.SoftDeleteSavingsAsync`, `BudgetPurger`).
> - Lista transakcji ma filtr `standingOrderId` i okruszek `?origin=standing-orders` („Przejdź do powiązanych”).
> - Reguł jest wiele (jsonb, łączone „lub”, każda z własnym zakresem kwot). Zakończenie (`EndMonth`) to nie usunięcie:
>   po ostatnim miesiącu zlecenie nie jest oczekiwane ani liczone w sumie, a import go już nie przypina.

> **REWIZJA — 2026-09-14: zlecenia epizodyczne ZASTĄPIŁY flagę „duży wydatek”** (etap 2 zgłoszenia #18;
> `Features/EpisodicOrders`, ekran `/episodic-orders`, makiety Figma — strona „Zlecenia”):
> - **`Transaction.IsLargeExpense` nie istnieje.** Wydatek jednorazowy to transakcja ZREALIZOWANEGO zlecenia
>   epizodycznego — na tym stoi dowód na ekranie celów (`GetSavingsQueryHandler`). Migracja `EpisodicOrders` przeniosła
>   flagi (tylko wydatki z budżetem) i usunęła kolumnę. Akcji masowej nie ma: każde zlecenie potrzebuje nazwy.
> - ⚠️ **Jedno źródło liczb:** zaplanowane ma plan (kategoria, kwota, termin), zrealizowane wskazuje transakcję
>   i kwotę, datę oraz kategorię bierze Z NIEJ. Plan zostaje po realizacji — po nim poznajemy, co da się
>   „cofnąć do zaplanowanych”.
> - **„Załóż cel oszczędzania” tworzy zwykłą rezerwację**, którą zlecenie RZĄDZI: zmiana planu przepisuje nierozliczoną
>   rezerwację, usunięcie zlecenia ją zabiera, „Oznacz jako kupione” rozlicza ją ZAKUPEM (nie wypłatą z oszczędności —
>   warunki z ekranu rezerwacji tu nie obowiązują). Uzbierane liczy `GetSavingsReservationsQueryHandler`, nie drugi kod.
> - Zrealizowane, którego transakcja zniknęła (usunięcie, reset), jest ukryte, a nie kasowane — przywrócenie transakcji
>   je oddaje. Cykl życia budżetu jak przy zleceniach stałych (reset zostawia, usunięcie zabiera).
> - Oznaczenie z listy transakcji idzie bez `budgetId` — serwer bierze budżet TRANSAKCJI, bo lista pokazuje kilka naraz.

> **REWIZJA — 2026-09-03: cykl życia budżetu.** Ekran „Ustawienia → Budżety" (issue #3)
> wprowadza cztery operacje, których wcześniej nie było. Różnice między nimi są subtelne
> i pomylenie ich daje błąd, którego nie widać na ekranie.
>
> | Operacja | Co robi | Co zostaje |
> |---|---|---|
> | **Wyłączenie** (`DisabledAt`) | zamyka budżet na **nowe dane** | wszystko; zarządzanie działa normalnie |
> | **Reset** | stempluje `DeletedAt` na transakcjach, importach i limitach | budżet z nazwą, walutą, miesiącem, `InitialBalance` **oraz celem i rezerwacjami** |
> | **Usunięcie** | to co reset **plus** cele, rezerwacje i stempel na samym budżecie | wszystko, przez `RetentionDays` |
> | **Przywrócenie** | zdejmuje stempel z budżetu i dzieci skasowanych **razem z nim** | — |
>
> - **`DisabledAt` to NIE `DeletedAt`.** Wyłączony budżet jest w pełni widoczny — filtr globalny
>   go nie dotyczy. Wyłączenie zamyka go na import i dopisywanie transakcji, a **nie** na edycję,
>   reset, usunięcie i przywrócenie; inaczej byłoby pułapką, z której trzeba się wygrzebywać
>   ponownym włączeniem. Import na wyłączony budżet to **409**, nie 400 — żądanie jest poprawne,
>   to stan zasobu na nie nie pozwala. Sprawdzenie działa też w **podglądzie**: bez tego
>   użytkownik przeszedłby cały stepper i dopiero „Zapisz" powiedziałoby mu, że się nie da.
> - **Przywracanie porównuje znacznik czasu.** Wracają tylko dzieci, których `DeletedAt` równa się
>   `DeletedAt` budżetu. Bez tego reset wykonany przed usunięciem zostałby po cichu cofnięty.
> - **`GET /api/budgets` to jedyne miejsce w aplikacji z `IgnoreQueryFilters()`.** Ekran ustawień
>   pokazuje usunięte jako czwarty stan — bez tego nie byłoby gdzie ich przywrócić.
> - **Okno retencji jest USTAWIENIEM** (`Budgets:RetentionDays`), nie liczbą w kodzie ani w tekście
>   tłumaczenia. Front dostaje je razem z listą i wstawia do modala usuwania. Po jego upływie
>   sprzątanie kasuje fizycznie — `BudgetPurger` to jedyne miejsce, w którym dane naprawdę znikają,
>   i dlatego idzie przez `ExecuteDelete`, a nie `db.Remove` (to drugie znaczy tu „ostempluj").
>
> **REWIZJA — 2026-09-12: dwa wejścia do sprzątania, jedna mechanika.** Mechanika kasowania siedzi
> w `Features/Budgets/Services/BudgetPurger.cs`, a handlery różnią się WYŁĄCZNIE progiem czasu:
> - `purge-deleted-budgets` (cron z `Budgets:PurgeCron`) — kasuje to, co przeleżało okno retencji.
> - `purge-deleted-budgets-now` — kasuje **wszystko oznaczone do usunięcia**, bez czekania.
>   Istnieje, bo „usuń teraz" jest potrzebne, gdy w budżecie wylądowały dane, których nie wolno
>   trzymać ani dnia dłużej (import na zły budżet, cudzy wyciąg).
>
> ⚠️ To drugie zadanie stoi na **`Cron.Never()`** i nie wolno mu dać harmonogramu. Rejestrujemy je
> jako cykliczne tylko po to, żeby było widoczne w `/hangfire` z przyciskiem „Trigger now" —
> Hangfire nie ma innego sposobu pokazania zadania uruchamianego ręcznie. Cron sprowadziłby okno
> retencji do zera i zamieniłby komunikat z modala usuwania w kłamstwo przy pierwszym przebiegu.
> W storage widać to wprost: wpis ma cron `0 0 31 2 *` i **nie ma pola `NextExecution`**.
>
> ⚠️ Warunek `DeletedAt != null` należy do `BudgetPurger`, nie do handlerów. Gdyby każdy budował
> własne zapytanie, to jego pominięcie zamieniłoby wymuszone sprzątanie w „skasuj wszystko" —
> bez ostrzeżenia i bez możliwości cofnięcia. Pilnują tego testy w `BudgetSettingsTests`.
> Wymuszony przebieg loguje się na poziomie `Warning`, bo pomija obietnicę daną użytkownikowi,
> a historia przebiegów jest audytem.
>
> Konsekwencja, o której trzeba wiedzieć: panel jest dostępny tylko w Development, więc na
> produkcji nie ma dziś czym tego wyzwolić. Dla narzędzia kasującego dane z pominięciem retencji
> to właściwość, nie brak — wejście dla użytkownika ma sens dopiero z ekranem, który pyta „na pewno".
>
> **REWIZJA — 2026-09-12: cele oszczędzania i rezerwacje są DZIEĆMI budżetu.**
> `SavingsGoal.BudgetBusinessId` i `SavingsReservation.BudgetBusinessId` przypisują je do budżetu
> (zwykła kolumna, bez relacji EF, jak przy transakcji), a zasięg ekranów rozstrzyga `SavingsBudgetScope`
> — ta sama reguła budżetu domyślnego co na dashboardzie i liście transakcji.
>
> ⚠️ **Cykl życia budżetu tego nie honorował.** Oba typy powstały PO `BudgetChildren`, więc nikt ich
> tam nie dopisał: usunięcie budżetu zostawiało żywy cel wskazujący na budżet, którego nie widać,
> przywrócenie wracało bez niego, a `BudgetPurger` — jedyne miejsce kasujące fizycznie — robił
> z nich SIEROTY na zawsze. Na bazie deweloperskiej wyszło z tego 1 cel i 12 rezerwacji bez budżetu.
>
> ⚠️ **`SoftDeleteAsync` i `SoftDeleteSavingsAsync` są rozdzielone celowo**, bo pierwsza ma dwóch
> wywołujących o różnych intencjach. Usunięcie woła obie; **RESET woła tylko pierwszą**, bo cel
> i rezerwacja są ZASADĄ, nie danymi — przeżywają reset tak samo jak nazwa i bilans początkowy.
> Dopisanie oszczędności do wspólnej metody kasowało cel przy resecie; złapał to
> `Reset_leaves_the_goal_and_reservations_alone`, a nie przegląd kodu.
> - **Dashboard nie ukrywa wyłączonych budżetów** — historię ogląda się także po zamknięciu
>   budżetu. Oznacza je tagiem i gasi kafle dopisujące dane. Ukrycie ich byłoby cichym
>   skasowaniem widoku na przeszłość.

---

## 6. Przepływ importu + mapowanie

**Flow:** wybierz bank → wgraj CSV → parsuj + normalizuj → dla każdej transakcji: reguła pasuje?
tak → kategoria z reguły / nie → predykcja ML → pewność ≥ 0.7 auto, inaczej „do przeglądu" → zapisz → podsumowanie.

**Mapowanie — WAŻNE rozróżnienie:**
- **W UI (stepperze) NIE MA mapowania kolumn.** Użytkownik nie wskazuje „ta kolumna to data". Parser per bank
  zna z góry układ swojego formatu (wynika z wyboru źródła w kroku 1).
- **W kodzie mapowanie JEST**, trzy warstwy: surowy CSV → `XxxRawRecord` (kształt banku, CsvHelper)
  → `ParsedRow` (wspólny znormalizowany kontrakt: DateOnly, decimal, oczyszczony opis) → `Transaction` (domena).
- Dzięki temu reszta silnika (kategoryzacja, zapis, dashboard) nie wie, z którego banku pochodzi plik.
  `ICategorizer` dostaje znormalizowany opis, nie surowy wiersz.
- Ręczny ekran mapowania kolumn pojawi się DOPIERO przy „wgraj dowolny CSV z nieznanego banku" (brak parsera).

**⚠️ Ślepa plama:** karta lunchowa (Pluxee/Edenred itp.) często nie ma wygodnego eksportu CSV, a wydatki są
zawężone do jednej kategorii (jedzenie). Realnie: kilka pozycji ręcznie albo traktowanie jako stała kwota na jedzenie.
Gotówka też ręcznie. **Do zweryfikowania**, czy karta w ogóle daje eksport.

---

## 7. Frontend

**Stack: Angular + NG-ZORRO (Ant Design).**
- PrimeNG **odrzucony**: przeszedł pod PrimeUI z modelem klucza licencyjnego (stare wersje MIT zostają darmowe,
  ale nowy nurt może być płatny).
- Blazor **odrzucony** przez usera: trzymałby jeden język (C#), ale ma mniejszy ekosystem komponentów;
  przy apce component-heavy (tabele/wykresy/formularze) to realna wada.
- NG-ZORRO: darmowy (MIT), bogaty, mocna tabela — dobry zamiennik PrimeNG. Estetyka Ant Design: gęsta,
  danocentryczna, pasuje do apki pełnej tabel.
- **AG Grid Community** (MIT) jako opcja pod JEDEN widok transakcji, jeśli tabela będzie potrzebować ciężkiej
  artylerii (wirtualizacja, grupowanie).

**Font — ROZSTRZYGNIĘTY (2026-08-31): IBM Plex Sans.** Nie jest to już rekomendacja, tylko odczyt
z design systemu w Figmie (strona `🅰 Typography` — wszystkie style tekstowe używają IBM Plex Sans).
Serwowany lokalnie z `@fontsource/ibm-plex-sans` (wagi 400 i 600), bez CDN-a. Subset `latin-ext`
jest w buildzie, więc polskie znaki diakrytyczne mają pokrycie.

Krytyczne: **cyfry tabelaryczne** — `font-variant-numeric: tabular-nums` / `font-feature-settings: "tnum"` na kolumnach
z kwotami, żeby liczby wyrównywały się w pionie. Klasa `.tnum` / `.amount` czeka w `web/src/styles.scss`.

> **REWIZJA — 2026-09-12: każde `nz-input-number` MUSI mieć `[nzParser]="parseAmount"`.**
> Domyślny parser NG-ZORRO zakłada format en-US: usuwa przecinki jako separator tysięcy
> i zostawia spacje. Na polskich kwotach daje to dwa błędy, z czego drugi jest groźniejszy:
> - `6 278,88` → `NaN`, więc wklejenie jest odrzucane i pole wraca do poprzedniej wartości
>   (na ekranie wygląda to jak wyzerowanie),
> - `278,88` → **27888**, czyli kwota stukrotnie za duża **bez żadnego objawu**. Nic nie wygląda
>   na zepsute, a do bazy idzie inna liczba niż na wyciągu.
>
> ⚠️ `nzParser` jest zwykłym inputem sygnałowym, **nie `WithConfig`**, więc nie da się tego
> ustawić globalnie przez `NZ_CONFIG` — każde pole musi dostać atrybut osobno i o tym najłatwiej
> zapomnieć przy dodawaniu nowego. Dziś wszystkie 10 pól liczbowych w aplikacji to kwoty.
>
> Jedyna niejednoznaczność (`6.278` to tysiące czy grosze?) jest rozstrzygnięta jawnie w
> `decimalSeparatorOf` na korzyść formatu polskiego — przy grupie dokładnie trzech cyfr to
> tysiące, `6.50` zostaje kwotą dziesiętną. Nie zmieniaj tego bez przeczytania tamtego doca.
>
> `parseAmount` zwraca `NaN` dla śmieci — tak NG-ZORRO rozpoznaje „nie ruszaj wartości pola".
> Zwrócenie `0` zamieniłoby literówkę w cichy zapis zera.

> **REWIZJA — 2026-09-13: budżet w widoku (`core/active-budget.ts`, issue #16).**
> Budżet między ekranami jeździ w adresie (`?budgetId`), ale NIE każde wejście go niesie: wyszukiwarka
> akcji w nagłówku, okruszki i linki wewnątrz ekranów nawigują gołą trasą, a dashboard trzymał wybór
> tylko w pamięci komponentu. Skutek: wybierasz budżet na dashboardzie, klikasz „Cele oszczędzania"
> i widzisz budżet domyślny. Przy kilku budżetach z tym samym miesiącem domyślny to ostatnio UTWORZONY,
> więc świeży, pusty budżet przejmował każdy ekran.
>
> **Reguła: adres wygrywa, gdy coś mówi; gdy milczy — obowiązuje `ActiveBudget`.** Ekran zależny od budżetu
> czyta `activeBudget.resolve(budżetyZAdresu)`, a gdy dostaje `budgetId` w adresie albo użytkownik zmienia
> wybór, publikuje go przez `activeBudget.set(...)`. Usunięcie budżetu woła `forget`.
>
> ⚠️ Celowo NIE dopisujemy `budgetId` do każdego linku. Pięć miejsc już o tym zapomniało (nagłówek,
> oszczędności → rezerwacje ×2, oszczędności → transakcje, rezerwacje → oszczędności), a szóste zapomni
> przy następnym ekranie. Fallback w miejscu ODCZYTU nie da się pominąć — ale nowy ekran MUSI go użyć.
>
> Stan żyje w pamięci karty: przeładowanie wraca do budżetu domyślnego jak dotąd. To, czy domyślny
> powinien uwzględniać pusty, świeżo utworzony budżet, jest osobną, otwartą decyzją.

### Motyw NG-ZORRO ↔ design system

Źródło prawdy dla kolorów i typografii to plik Figma **Design System**
(`10vPHd0SjYP23dPMaPcYrG`), strony `🎨 Colors` i `🅰 Typography`.

- `web/src/styles/design-tokens.less` — surowe tokeny przepisane z Figmy (6 palet × 11 odcieni
  + tokeny semantyczne `Text/*`, `Input/*`, `Stroke/*`). **To jedyne miejsce z hexami.**
- `web/src/theme.less` — mapowanie tokenów na zmienne Less NG-ZORRO.

**Pułapka, o którą łatwo się rozbić:** Less ewaluuje zmienne leniwie i wygrywa *ostatnia*
deklaracja. Nadpisania muszą iść **po** `@import` ng-zorro. Odwrotna kolejność kompiluje się
bez błędu i po cichu nie robi nic — zostaje domyślny Ant `#1890FF`.

Skala typograficzna z Figmy (IBM Plex Sans): Heading 60/48/40, Subtitle 28/18, Body 18/16,
line-height 1.3, wagi 400 i 600. `@font-size-base` = **16px**, nie ant-owe 14px — to zmienia
wysokości komponentów w całej apce, więc nie „poprawiaj" tego z powrotem bez decyzji.

**Platformy:** web + mobile. Mobile służy głównie do podglądu; import to zadanie „przy biurku". Responsywny web
wystarczy na MVP (NG-ZORRO: `responsiveLayout` stack/scroll). NIE natywna apka mobilna na tym etapie.

**Stepper importu (NG-ZORRO `nz-steps`), 4 kroki:**
1. **Źródło** — wybór konta/banku (determinuje parser). Warunek: źródło wybrane.
2. **Plik** — upload CSV (`nz-upload`). Wstępny parse. Zły format → status kroku `error` z komunikatem
   (to gałąź „Format OK? → nie"). Warunek: plik + parse OK.
3. **Podgląd** — tabela wczytanych wierszy + kategorie (poniżej progu = „do przeglądu"). Kategoryzacja odpala się
   przed tym krokiem (pokaż spinner — może być wiele wierszy). Warunek: potwierdzenie „Zapisz import".
4. **Gotowe** — `nz-result`: ile zaimportowano / ile do przeglądu + przycisk „Przejdź do przeglądu" (→ kolejka korekt).
- Liniowy (bez skoków w przód), wstecz dozwolony (podmiana pliku). **Bez kroku mapowania kolumn** (patrz sekcja 6).

> **REWIZJA — 2026-09-02 (po lekturze makiet w Figmie).** Powyższy opis powstał ze specyfikacji,
> a nie z makiet, i w kilku miejscach się z nimi rozjeżdżał. Rozstrzygnięcia użytkownika:
>
> | | Ten dokument | Makieta | Zbudowano |
> |---|---|---|---|
> | Liczba kroków | 4 | ramki kroku 1 → **5** (z „Konfiguracją konwertera"), ramki 2–4 → 4 | **4** |
> | Orientacja | — | **pionowa, po lewej, z opisami** | pionowa |
> | Krok 1 | konto + bank | **bank + budżet** | bank + budżet |
> | Krok 3 | podgląd + potwierdzenie | **edytowalny**: zaznaczanie, „Usuń zaznaczone", szukajka, korekta kategorii | edytowalny |
> | Krok 4 | `nz-result` | **panel statystyk** | panel statystyk |
>
> - **Makiety są wewnętrznie niespójne**: ramki kroku 1 (`2:3`, `36:677`, `36:1034`) mają pięć
>   kroków, ramki 2–4 (`32:112`, `32:1082`, `56:8930`) już tylko cztery. Wersja 4-krokowa wygrywa,
>   bo zgadza się z §6: parser per bank zna format z góry, więc nie ma czego konfigurować.
>   Ekran mapowania kolumn wróci dopiero przy „wgraj CSV z nieznanego banku".
> - **Budżet zamiast konta** to świadoma decyzja użytkownika wbrew modelowi z §5 — tam relacji
>   Budżet→Transakcja nie ma, bo budżet był zestawem miesięcznych limitów per kategoria.
>   Relacja `Transaction.BudgetId` powstała pod tę makietę. `ImportBatch` też wskazuje budżet,
>   nie konto. Konto **nie bierze udziału w imporcie**.
> - Import jest **dwufazowy**: `POST /api/import/preview` liczy i nic nie zapisuje,
>   `POST /api/import` przyjmuje **wiersze po edycji użytkownika**, nie plik. Ponowne parsowanie
>   pliku przy zapisie wyrzuciłoby do kosza wszystko, co użytkownik poprawił w kroku 3.
> - `CommitRowRequestDto.Edited` rozstrzyga status: korekta człowieka daje `ManuallyCategorized`
>   i zeruje `Confidence` (decyzja człowieka nie jest predykcją), więc nie wraca do kolejki
>   przeglądu, którą właśnie ręcznie rozbroił.
> - **Duplikatów nie ma w makiecie**, a system je wykrywa. Pokazujemy je w tabeli jako wygaszony
>   wiersz ze znacznikiem „Duplikat" i nie wysyłamy do zapisu.

---

## 8. Materiały referencyjne z sesji

W trakcie sesji powstały diagramy (do odtworzenia/rozbudowy w razie potrzeby):
- Przepływ danych importu; pętla uczenia ML; roadmapa etapów; mapa procesów użytkownika; makieta głównego ekranu.
- Zestaw UML: use case, klasy, sekwencja (import), stany (transakcja), komponenty — zebrane w pliku
  `diagramy-uml-tracker.html`.
- 3 flowcharty MVP: import, popraw kategorię (z pętlą douczania), dodaj ręcznie.

---

## 9. Założenia do potwierdzenia z userem
- **Kryterium sukcesu:** przyjęto adopcję (faktyczne użycie co miesiąc). Potwierdzić.
- **Budżet czasu:** przyjęto „kilka tygodni po godzinach". Potwierdzić — skaluje rozmiar MVP.
- **Próg pewności 0.7** — do strojenia na realnych danych.
- **Wizja „dużo większej aplikacji"** (sekcja 1) — dopytać PRZED decyzjami, które trudno odwrócić.

---

## 10. Zasady pracy dla Claude'a w tym repo
- Trzymaj się vertical slice: nowa funkcja = nowy folder w `Features/`, logika obok endpointu.
- Nowy bank = nowy `IStatementParser` + rejestracja; nie ruszaj handlera importu.
- Nie wprowadzaj MediatR, pełnego CQRS, repository-per-entity ani Clean Architecture bez wyraźnej potrzeby
  (były świadomie odrzucone — jeśli proponujesz, uzasadnij zmianę realnym bólem).
- Kategoryzacja zawsze za `ICategorizer`; ML in-process.
- Pilnuj, by dostęp do danych szedł przez handlery (przyszły multi-user przez global query filters).
- Jeśli duplikujesz tę samą logikę w 3. slice — wyciągnij do wspólnej domeny (to sygnał, nie zapasowa abstrakcja).

> **ZASADA — 2026-09-11: szczegół z czyjejś historii nie trafia do repozytorium.**
> Wzorzec reguły, fikstura testowa, komentarz albo przykład, który istnieje tylko dlatego, że to
> *czyjś* wyciąg — konkretna przychodnia, catering, lokalna stacja paliw, miasto, kwota z realnej
> transakcji — jest daną osobową. Do kodu trafiają wyłącznie wzorce, które pomogą **dowolnemu**
> polskiemu użytkownikowi. Resztę dodaje się przez `POST /api/categorization/rules` albo do
> `data/category-rules.json`, który leży poza repozytorium (issue #13).
>
> ⚠️ **Dotyczy to także wyjaśniania, dlaczego coś usunięto.** Komentarz, test, commit czy opis PR,
> który „dla kontekstu" wymienia usunięte nazwy, wnosi je z powrotem — i to z etykietą, że dotyczą
> autora, czyli gorzej niż przed usunięciem. Przy #13 zrobiłem to dwa razy: strażnik w teście
> trzymał listę prywatnych wzorców jako literały, a doc handlera podawał je jako przykłady.
> Obie wpadki złapał dopiero grep przed commitem, więc **grep po znanych prywatnych nazwach
> jest obowiązkowym krokiem przed każdym commitem, który dotyka reguł, seedów albo fikstur.**
> Strażnik, który potrzebuje takiej listy, czyta ją z pliku poza repo (patrz `BaselineSeedTests`).

> **ROZSTRZYGNIĘCIE — 2026-09-07: makieta jest źródłem prawdy.**
> Gdy makieta w Figmie i plan/issue mówią co innego, **wygrywa makieta** — plany powstają
> zwykle PRZED nią i to ona jest ostatnią wypowiedzianą decyzją. Nie znaczy to, że planu się
> nie czyta: plan niesie *uzasadnienia*, których w makiecie nie widać, więc rozjazd trzeba
> **opisać** (tak jak §7 przy stepperze importu i rewizja w `plans/savings-potential.md`),
> a nie po cichu wybrać jedną wersję.
>
> ⚠️ **Rozjazd to nie to samo co milczenie.** Makieta, która czegoś NIE pokazuje, bo powstała
> przed tą funkcją, niczego nie rozstrzyga — wtedy pytaj, zamiast traktować brak jako decyzję.
> Ale pytaj też, zanim uznasz coś za lukę: przy „Celach oszczędzania" wziąłem brak kafla na
> makiecie dashboardu za przeoczenie, bo breadcrumb mówi „Dashboard / …". Wniosek był błędny —
> patrz zasada o breadcrumbach niżej.

> **ZASADA — 2026-09-08: makietę trzeba ZOBACZYĆ, nie tylko odczytać jej węzły.**
> Zrzut drzewa z Figma MCP oddaje teksty, a grafikę zwraca jako puste ramki i grupy
> (`Group 7`, `Frame 2`). Kafel rezerwacji (`147:96`) zbudowałem z samych węzłów tekstowych
> i wyszedł rząd osobnych kółek postępu zamiast **jednego wykresu o pierścieniach
> współśrodkowych** — a to nie była różnica kosmetyczna: pierścienie współśrodkowe pokazują
> podział JEDNEJ puli, osobne kółka mówią „niepowiązane paski".
>
> Przy każdym kaflu, wykresie lub czymkolwiek z grafiką: **`get_screenshot` na ramce**, zanim
> zaczniesz pisać szablon. Sam `get_figma_data` wystarcza tylko dla formularzy i tabel.

> **ROZSTRZYGNIĘCIE — 2026-09-07: czym są breadcrumby.**
> Breadcrumb to **szybki powrót do poprzedniego ekranu**, a `Dashboard` jest **ekranem matką** —
> korzeniem, pod którym wisi wszystko inne.
>
> ⚠️ Breadcrumb **nie jest mapą nawigacji** i nie obiecuje, że na ekranie nadrzędnym istnieje
> odnośnik w dół. „Dashboard / Cele oszczędzania" znaczy „stąd wrócisz na dashboard", a nie
> „na dashboardzie jest kafel oszczędności". Wejście w ekran może iść zupełnie inną drogą —
> np. przez wyszukiwarkę akcji w nagłówku, która jest na każdym ekranie.
>
> Stąd ścieżka bywa ZALEŻNA OD POCHODZENIA: lista transakcji pokazuje „Ustawienia / Dane
> treningowe / Lista transakcji", gdy przyszło się z zakładki treningowej (`?origin=training`),
> bo powrót ma prowadzić tam, skąd użytkownik przyszedł — a nie do korzenia.
