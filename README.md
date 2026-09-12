# Budżet tracker

Osobisty tracker wydatków: import CSV z banków → kategoryzacja (reguły + ML) → dashboard
„gdzie ucieka kasa". Single-user MVP.

Decyzje projektowe, model domenowy i zakres etapów żyją w **[CLAUDE.md](CLAUDE.md)** — to źródło
prawdy, nie ten plik. README opisuje wyłącznie, jak to uruchomić.

## Stack

| Warstwa | Wybór |
|---|---|
| Backend | .NET 10 · Minimal APIs · vertical slice |
| Baza | PostgreSQL 18 (Docker) · EF Core + Npgsql |
| ML | ML.NET in-process (`Microsoft.ML`) |
| Parsowanie CSV | CsvHelper |
| Frontend | Angular 22 · NG-ZORRO (Ant Design) · Vitest |

## Wymagania

- .NET SDK 10.0.400+ (przypięte w `global.json`)
- Node.js **≥ 22.22.3** — Angular 22 nie wystartuje na starszym
- Docker Desktop (dla bazy)

## Uruchomienie

```bash
cp .env.example .env
docker compose up -d db
```

Backend (`https://localhost:7xxx`, port z `api/src/BudgetTracker.Api/Properties/launchSettings.json`):

```bash
cd api && dotnet run --project src/BudgetTracker.Api
```

Frontend (`http://localhost:4200`):

```bash
cd web && npm start
```

### Landing page

Strona publiczna opisująca projekt to **osobna aplikacja w tym samym workspace**
(`web/projects/landing`) — własny build i bundel, ale wspólny motyw NG-ZORRO
i tokeny design systemu, więc wygląd nie może się rozjechać z aplikacją.

```bash
cd web && npx ng serve landing --port 4300
```

Zrzuty ekranu na stronie pochodzą z `DemoSeed` (zmyśleni sprzedawcy), nigdy z Twojej bazy —
patrz `plans/landing-page.md` i sekcja „Dane" niżej. Jak je odtworzyć, opisuje
`web/projects/landing/README.md`.

Sprawdzenie, czy baza faktycznie odpowiada — `GET /health/db` zwraca `{"database":"up"}`.
Jeśli dostajesz błąd połączenia, prawie zawsze znaczy to, że kontener nie chodzi, a nie że kod jest zły.

## Dane modelu kategoryzacji

Kategoryzacja działa hybrydowo: **reguły + model ML** (CLAUDE.md §3). Reguły i taksonomia
seedują się same przy starcie — model wymaga danych, których **nie ma w repozytorium**.

Katalog `data/` to **konfiguracja środowiska**, tak samo jak connection string. Trzyma:

| plik | co to |
|---|---|
| `training-set.csv` | pary `opis → kategoria` z realnych wyciągów |
| `category-model.zip` | wytrenowany model — artefakt, nie źródło |
| `category-rules.json` | reguły kategoryzacji specyficzne dla tej instalacji |

Wszystkie trzy są w `.gitignore`, bo zawierają nazwy sprzedawców z prawdziwych transakcji.

**Reguły dzielą się na dwie grupy i to rozróżnienie jest istotne.** Wzorce, które pomogą
dowolnemu polskiemu użytkownikowi przy pierwszym imporcie (Biedronka, Orlen, „czynsz",
„apteka"), mieszkają w `BaselineSeed.cs` — w repozytorium. Wzorzec działający tylko dlatego,
że to *Twoja* historia (konkretna przychodnia, konkretny catering), jest daną osobową: trafia do
`data/category-rules.json` albo do bazy przez `POST /api/categorization/rules`, **nigdy do kodu**.
Lista reguł zdrowotnych w publicznym repozytorium czyta się jak spis świadczeniodawców autora.

Plik jest wczytywany przy każdym starcie; duplikatów nie będzie, bo identyfikator reguły
wyprowadza się z jej treści. Zepsuty JSON nie zatrzyma aplikacji — trafi do logu.
Ścieżki ustawia sekcja `Categorization` w `appsettings`.

Trening (wymaga pliku z danymi):

```bash
curl -X POST http://localhost:5031/api/categorization/train
```


**Bez modelu aplikacja działa** — kategoryzują wtedy same reguły. To stan poprawny,
nie awaria: na danych, z których reguły powstały, pokrywały 97% transakcji.

## Bramka jakości

```bash
cd api && dotnet build && dotnet test
```

```bash
cd web && npx ng lint && npx ng test --watch=false && npx ng build
```

W Claude Code to samo robi `/verify`.

## Czego tu celowo nie ma

- **Migracji EF Core** — pierwsza powstanie razem z pierwszymi encjami (slice importu), nie na zapas.
- **Kodu w `Features/`** — foldery są puste. Vertical slice znaczy, że feature powstaje w całości,
  gdy go ciągniemy; puste rusztowanie „na przyszłość" to dokładnie to, czego ten projekt unika.
- **Wyboru fontu** — kandydaci (IBM Plex Sans / Inter) czekają na decyzję, patrz CLAUDE.md §7.
  Klasa `.tnum` na cyfry tabelaryczne w kolumnach kwot już jest w `web/src/styles.scss`.

## Dane

`.gitignore` blokuje `*.csv`, `data/` i `.env`. **Wyciągi bankowe nie trafiają do repo** — nawet
prywatnego. Pliki testowe trzymaj w `**/testdata/**` (wyjątek w `.gitignore`) i tylko zanonimizowane.
