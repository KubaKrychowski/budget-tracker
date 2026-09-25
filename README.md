# Wydatki.pl

Osobisty tracker wydatków: import CSV z banków → kategoryzacja (reguły + ML) → dashboard
„gdzie ucieka kasa", limity, cele oszczędzania i zlecenia. Jedna osoba, jedna instalacja —
ale dane są odseparowane per właściciel (RLS w Postgresie), a logowanie idzie przez własny
serwer tożsamości.

Decyzje projektowe, model domenowy i zakres etapów żyją w **[DECISIONS.md](DECISIONS.md)** — to źródło
prawdy, nie ten plik. Instrukcje pracy dla agentów: [CLAUDE.md](CLAUDE.md), [api/CLAUDE.md](api/CLAUDE.md),
[web/CLAUDE.md](web/CLAUDE.md). README opisuje wyłącznie, jak to uruchomić.

## Stack

| Warstwa | Wybór |
|---|---|
| Backend | .NET 10 · Minimal APIs · vertical slice (12 slice'ów w `Features/`) |
| Baza | PostgreSQL 18 (Docker) · EF Core + Npgsql · RLS per właściciel |
| Tożsamość | `BudgetTracker.Identity` — ASP.NET Identity + OpenIddict (logowanie, 2FA, panel admina) |
| Zadania w tle | Hangfire (trening modelu jedzie kolejką, nie w żądaniu) |
| Pliki | Azure Blob Storage · lokalnie Azurite z `docker-compose` |
| ML | ML.NET in-process (`Microsoft.ML`) |
| Parsowanie CSV | CsvHelper |
| Frontend | Angular 22 · NG-ZORRO (Ant Design) · Vitest |
| CLI | `tools/bt-cli` — moduł PowerShell `bt` do `POST /api/cli/execute` |

## Wymagania

- .NET SDK 10.0.400+ (przypięte w `global.json`)
- Node.js **≥ 22.22.3** — Angular 22 nie wystartuje na starszym
- Docker Desktop (dla bazy)

## Uruchomienie

```bash
cp .env.example .env
docker compose up -d
```

`docker compose` stawia trzy usługi: **db** (Postgres), **mailhog** (podgląd maili z Identity —
potwierdzenie adresu, reset hasła) i **azurite** (emulator Azure Blob Storage na modele
kategoryzacji). Samo `up -d db` wystarczy tylko wtedy, gdy nie dotykasz logowania ani treningu.

Do zalogowania się potrzebny jest **serwer tożsamości** — bez niego front zatrzyma się na ekranie
logowania:

```bash
dotnet run --project BudgetTracker.Identity
```

Backend (`https://localhost:7xxx`, port z `api/src/BudgetTracker.Api/Properties/launchSettings.json`):

```bash
cd api && dotnet run --project src/BudgetTracker.Api
```

Frontend (`https://localhost:4200`, wymaga lokalnego certyfikatu — sekcja „Lokalny HTTPS" niżej):

```bash
cd web && npm start
```

### Lokalny HTTPS

Identity (`https://localhost:7226`), API (`https://localhost:7133`) i front (`https://localhost:4200`) działają lokalnie
wyłącznie na https, na jednym certyfikacie deweloperskim ASP.NET (CN=localhost). Jednorazowo:

```powershell
dotnet dev-certs https --trust        # zaufanie certyfikatowi w systemie (Windows poprosi o potwierdzenie)
.	oolsdev-certs.ps1                # eksport do web/.certs (plik PEM dla `ng serve`; poza gitem)
```

Kestrel (Identity i API) bierze certyfikat sam z magazynu systemowego, a `ng serve` z `web/.certs`
(`angular.json` → `serve.options`). Po zmianie adresów zrestartuj Identity — przy starcie sam uzgadnia zarejestrowanego
klienta SPA z konfiguracją. W Riderze wybierz profil `https` (jedyny w `launchSettings.json`).

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

Kategoryzacja działa hybrydowo: **reguły + model ML** (DECISIONS.md §3). Reguły i taksonomia
seedują się same przy starcie — model wymaga danych, których **nie ma w repozytorium**.

Katalog `data/` to **konfiguracja środowiska**, tak samo jak connection string. Trzyma:

| plik | co to |
|---|---|
| `category-rules.json` | reguły kategoryzacji specyficzne dla tej instalacji |

Jest w `.gitignore`, bo zawiera nazwy sprzedawców z prawdziwych transakcji.

⚠️ **Model i zbiór uczący nie leżą już na dysku.** Zbiór uczący powstaje z bazy (transakcje
z kategoriami plus ręczne korekty), a wytrenowany model idzie do bloba — katalog wersji, wraz
ze wskazaniem aktywnej, trzyma tabela `ModelVersions`. Jedyną ścieżką plikową, jaka została
w `CategorizationOptions`, jest `LocalRulesPath`.

**Reguły dzielą się na dwie grupy i to rozróżnienie jest istotne.** Wzorce, które pomogą
dowolnemu polskiemu użytkownikowi przy pierwszym imporcie (Biedronka, Orlen, „czynsz",
„apteka"), mieszkają w `BaselineSeed.cs` — w repozytorium. Wzorzec działający tylko dlatego,
że to *Twoja* historia (konkretna przychodnia, konkretny catering), jest daną osobową: trafia do
`data/category-rules.json` albo do bazy przez `POST /api/categorization/rules`, **nigdy do kodu**.
Lista reguł zdrowotnych w publicznym repozytorium czyta się jak spis świadczeniodawców autora.

Plik jest wczytywany przy każdym starcie; duplikatów nie będzie, bo identyfikator reguły
wyprowadza się z jej treści. Zepsuty JSON nie zatrzyma aplikacji — trafi do logu.
Ścieżki ustawia sekcja `Categorization` w `appsettings`.

Trening (wymaga pliku z danymi). Endpoint **kolejkuje** zadanie w Hangfire i zwraca `jobId` —
nie czeka na wynik, bo trening trwa dłużej niż rozsądny limit żądania HTTP:

```bash
curl -X POST https://localhost:7133/api/categorization/train
```

Status i raport odbierasz osobno, tym samym `jobId`:

```bash
curl https://localhost:7133/api/categorization/train/<jobId>
```

Wytrenowany model ląduje w blobie (lokalnie: Azurite), a katalog wersji w bazie — nie na dysku.


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

- **Integracji z bankiem** — odrzucona, nie odłożona. Wejściem jest CSV eksportowany ręcznie.
- **Hostingu i współdzielenia** — konta i izolacja danych są (Identity + RLS), ale nie ma
  zapraszania, wspólnych budżetów ani wdrożenia w chmurze. To instalacja dla jednej osoby.
- **Odczytu salda z banku** — saldo otwarcia podajesz sam przy zakładaniu budżetu, resztę
  aplikacja liczy z zaimportowanych transakcji. Konto, którego nie wgrasz, nie istnieje w rachunku.
- **Pozycji paragonu** — transakcja jest najmniejszą jednostką.

⚠️ **Font**: `web/src/styles/design-tokens.less` deklaruje `IBM Plex Sans` (z design systemu
w Figmie) i makiety są na nim zrobione, ale komentarz w `web/src/styles.scss` wciąż twierdzi,
że wybór nie zapadł. Jedno z dwóch jest nieaktualne — do rozstrzygnięcia przy okazji, patrz
DECISIONS.md §7.

## Dane

`.gitignore` blokuje `*.csv`, `data/` i `.env`. **Wyciągi bankowe nie trafiają do repo** — nawet
prywatnego. Pliki testowe trzymaj w `**/testdata/**` (wyjątek w `.gitignore`) i tylko zanonimizowane.
