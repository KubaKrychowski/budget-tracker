# Budżet tracker — instrukcje pracy

Osobisty tracker wydatków: import CSV z banku → kategoryzacja (reguły + ML.NET) → budżety, limity,
oszczędności, zlecenia. Właściciel jest jednocześnie użytkownikiem i jedynym deweloperem. Rozmawiamy **po polsku**.

Ten plik mówi, JAK pracować. Szczegóły backendu są w [api/CLAUDE.md](api/CLAUDE.md), frontendu w
[web/CLAUDE.md](web/CLAUDE.md) (ładują się same, gdy pracujesz w tych katalogach). **Dlaczego** coś jest
tak, a nie inaczej, opisuje [DECISIONS.md](DECISIONS.md) — patrz „Decyzje” niżej.

## Mapa repo

| Ścieżka | Co tam jest |
|---|---|
| `api/src/BudgetTracker.Api/Features/<Feature>/` | vertical slice: moduł, `Commands/`, `Queries/`, `Services/`, `Contracts/`, `Exceptions/` |
| `api/src/BudgetTracker.Api/Domain/` | encje (`Entity`), słowniki (`DictionaryEntity`), enumy w `Consts/` |
| `api/src/BudgetTracker.Api/Infrastructure/` | `AppDbContext`, `Migrations/`, `DomainExceptionHandler`, seedy, Hangfire |
| `api/tests/BudgetTracker.Api.Tests/` | xUnit na PRAWDZIWYM Postgresie (bazy `budgettracker_*_test`) |
| `web/src/app/features/<ekran>/` | ekrany Angulara (komponent + `.html` + `.scss` + `.spec.ts`) |
| `web/src/app/core/` | wspólne: `api/models`, `active-budget`, `budget-switcher`, `confirm-dialog`, `icons.ts`, `parse-amount` |
| `web/public/i18n/pl.json`, `en.json` | wszystkie teksty UI |
| `web/projects/landing/` | publiczna strona projektu (osobna aplikacja) |
| `tools/bt-cli/` | instalowalny moduł PowerShell `bt` — kliencki CLI do `POST /api/cli/execute` spoza przeglądarki (issue #25) |
| `DECISIONS.md` | dziennik decyzji z uzasadnieniami (dawny `CLAUDE.md`; odwołania „CLAUDE.md §N” w kodzie prowadzą tam) |
| `plans/`, `docs/`, `.claude/` | lokalne plany i rusztowanie — **w `.gitignore`**, nie przenoś tam niczego, co ma zostać w repo |
| `data/` | lokalne dane modelu i prywatne reguły — **poza gitem, nie commituj** |

## Zadania

- Zadania to issues w **osobnym repo** `KubaKrychowski/budget-tracker-2-boards`, na tablicy „Budget Tracker 2”
  (`gh issue view <N> --repo KubaKrychowski/budget-tracker-2-boards`). Kod jest w `KubaKrychowski/budget-tracker`.
- Makiety: Figma, plik `75y55ipSEgD2YtzJ5z0uH4` (strony per ekran). Design system: `10vPHd0SjYP23dPMaPcYrG`.

## Przepływ pracy

1. **Przeczytaj issue i odpowiednią sekcję `DECISIONS.md`.** Zanim coś „naprawisz”, sprawdź, czy to nie była
   świadoma decyzja. Niejasność w wymaganiu biznesowym (co liczyć, co pokazać) = **zapytaj**, nie zgaduj.
   Pytaj konkretnie, z 2–3 opcjami i rekomendacją.
2. **UI: najpierw makieta w Figmie, potem STOP i czekaj na akceptację.** Plan albo issue to nie zgoda na kod.
   Przy istniejącej makiecie rób `get_screenshot` ramki — sam zrzut węzłów gubi grafikę.
   Makieta wygrywa z planem; gdy się rozjeżdżają, opisz rozjazd. Brak czegoś na makiecie to nie decyzja — zapytaj.
3. **Gałąź od `main`:** `feat/<krotki-opis>` albo `fix/<krotki-opis>` (po polsku, bez ogonków).
4. **Implementacja + testy** (backend i front osobno — komendy w plikach katalogów). Test ma sprawdzać regułę,
   którą zgłoszono, i najlepiej paść bez poprawki.
5. **Weryfikacja w przeglądarce** przy każdej zmianie widocznej w UI — patrz „Środowisko demo”.
6. **Gdy decyzja się zmieniła:** dopisz `REWIZJA` w `DECISIONS.md`.
7. **Commit i PR** — patrz „Git”. Nie merguj sam.

## Definicja „gotowe”

- [ ] `dotnet test` zielony (jeśli ruszony backend) — liczba testów w raporcie
- [ ] `npm test`, `npm run lint`, `npm run build` w `web/` czyste (jeśli ruszony front)
- [ ] migracja wygenerowana, przejrzana i ma XML doc (jeśli zmienił się model)
- [ ] teksty w `pl.json` **i** `en.json` / w obu `.resx`
- [ ] sprawdzone w przeglądarce na bazie demo, zrzut ekranu w raporcie
- [ ] grep po prywatnych danych (niżej), jeśli ruszone reguły, seedy albo fikstury
- [ ] PR z opisem: problem, zmiana, migracja, weryfikacja

Raportuj uczciwie: czego nie sprawdziłeś, napisz wprost.

## Twarde zasady

- **Prawdziwa baza użytkownika (`budgettracker`) jest nietykalna.** Migracja albo SQL na niej tylko po wyraźnej
  zgodzie, i najpierw backup (`pg_dump`). Do sprawdzania masz bazę demo i bazy testowe.
- **Nie zabijaj procesów użytkownika:** API z Ridera (port 5031) i `ng serve` na 4200. Rider blokuje `bin/`
  — buduj do katalogów tymczasowych (patrz `api/CLAUDE.md`).
- **Żadnych prywatnych danych w repo.** Nazwa konkretnego sprzedawcy, przychodni, miasta albo kwota z realnego
  wyciągu to dana osobowa. W kodzie tylko wzorce przydatne DOWOLNEMU polskiemu użytkownikowi. Dotyczy też
  komentarzy, testów, commitów i opisów PR — nawet gdy wyjaśniasz, co usunięto. Dane demo = `DemoSeed` (zmyślone).
- **Teksty dla użytkownika nigdy w kodzie:** backend `Resources/SharedResource*.resx`, front `web/public/i18n/*.json`.
- **Nazwy w kodzie po angielsku, komentarze i teksty po polsku.**
- Nie wprowadzaj MediatR, pełnego CQRS, repozytoriów per encja, Clean Architecture ani nowych bibliotek UI
  bez uzasadnienia realnym problemem — były świadomie odrzucone.
- Dialogi: alert **na górze** treści, przyciski stopki **do prawej**.

## Środowisko demo (weryfikacja w przeglądarce)

Wymaga działającego Docker Desktop (kontener `budget-tracker-db`, `docker compose up -d db`).
Konfiguracje w `.claude/launch.json` **katalogu nadrzędnego** (`workspace/`), uruchamiane przez `preview_start`:

| Nazwa | Port | Co |
|---|---|---|
| `budget-tracker-api-demo` | 5099 | API na bazie `budgettracker_uiverify` z `Demo:Seed=true` |
| `budget-tracker-web-demo` | 4310 | front z `web/proxy.demo.json` → 5099 |

- API **nie migruje bazy samo.** Po dodaniu migracji zaktualizuj bazę demo:
  `dotnet ef database update --project api/src/BudgetTracker.Api --connection "Host=localhost;Port=5432;Database=budgettracker_uiverify;Username=budget;Password=budget_dev_only"`
  (z tymczasowymi katalogami buildu). Objaw braku: 500 i `column ... does not exist` w logach API.
- 502 z proxy tuż po starcie = API jeszcze wstaje. Odczekaj i przeładuj.
- Klikaj przy `resize_window` preset **desktop** — przy emulowanym viewporcie kliknięcia nie trafiają.
- Modale NG-ZORRO mają animację — zrzut zaraz po kliknięciu łapie stan pośredni. Odczekaj sekundę.

## Git

- Commit po polsku, w trybie opisowym: `Obszar: co się zmieniło (#N)`, np. `Rezerwacje: wpłaty na cel zamiast kolejki zbierania (#23)`.
  Treść: punkty z tym, co i dlaczego.
- PR: tytuł jak commit; opis zaczyna się od `Closes KubaKrychowski/budget-tracker-2-boards#N`, potem sekcje
  Problem / Zmiana / Migracja / Weryfikacja.
- Przed commitem dotykającym reguł, seedów lub fikstur: grep po znanych prywatnych nazwach (lista poza repo,
  patrz `BaselineSeedTests`). `data/` nigdy nie idzie do commita.

## Windows / PowerShell — pułapki narzędzi

- PowerShell psuje cudzysłowy: polskie „ ” w argumentach, wyrażenia `jq`, identyfikatory SQL w `"..."`.
  Tekst z takimi znakami edytuj narzędziem Edit/Write albo skryptem node, a SQL puszczaj z pliku:
  `docker cp plik.sql budget-tracker-db:/tmp/q.sql; docker exec budget-tracker-db sh -c 'psql -U "$POSTGRES_USER" -d <baza> -f /tmp/q.sql'`
  (użytkownik bazy to `budget`, nie `postgres`).
- Pliki tymczasowe trzymaj w katalogu scratchpad sesji, nie w repo.
- „Docker API not found” = Docker Desktop nie działa. Uruchom go i odczekaj, zanim uznasz, że kod jest zły.

## Decyzje (`DECISIONS.md`) — gdzie szukać

| Temat | Sekcja |
|---|---|
| Kategoryzacja, ML, reguły, wpływy | §3 |
| Architektura, wyjątki → HTTP, `BusinessId`, soft delete, konwencje folderów | §4 |
| Encje, słowniki, enumy, budżet i jego cykl życia (reset/usunięcie/przywrócenie) | §5 |
| Limity, zlecenia stałe i epizodyczne, cele oszczędzania, rezerwacje i wpłaty | §5 (rewizje z 2026-09) |
| Import CSV, parsery, deduplikacja | §6, §7 (stepper) |
| Frontend: motyw, `parseAmount`, aktywny budżet | §7 |
| Prywatne dane, makiety, breadcrumby | §10 |
