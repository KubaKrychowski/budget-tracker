# bt — kliencki CLI do budget-tracker (issue #25)

Instalowalny modul PowerShell dający globalną komendę `bt`, wołającą `POST /api/cli/execute` — ten sam
endpoint co panel terminala w aplikacji. Zero HTTP-owych szczegółów na co dzień: `bt <rzeczownik>
<czasownik> --flagi`, jak w panelu albo `gh`/`kubectl`.

## Instalacja

```powershell
.\tools\bt-cli\install.ps1
```

Zapyta raz o adres API (np. `https://localhost:7133` dla API z Ridera, `https://localhost:5099` dla
środowiska demo) i zapamięta go trwale (`BT_API_URL`, per-użytkownik). Otwórz nowe okno PowerShell —
`bt` jest już dostępne, bez importowania modułu i bez wpisów w `$PROFILE`.

Zmiana adresu później: `.\tools\bt-cli\install.ps1 -ApiUrl https://localhost:5099` albo ręcznie
`[Environment]::SetEnvironmentVariable('BT_API_URL', '...', 'User')`.

## Logowanie

API wymaga zalogowanego użytkownika (BudgetTracker.Identity). Nic nie trzeba konfigurować poza adresami —
`bt-cli` jest klientem publicznym (kod autoryzacyjny + PKCE), **bez sekretu**:

```powershell
bt login    # otwiera przeglądarkę na stronie logowania; po zalogowaniu i zgodzie wraca do terminala
```

Hasło nie przechodzi przez CLI (loguje się Ci strona Identity, więc działa też weryfikacja dwuetapowa). Wynik wraca na adres
`http://127.0.0.1:<port>/callback` — moduł nasłuchuje na pierwszym wolnym z portów 53682–53686 (tylko na tej maszynie),
więc żaden z nich nie może być zajęty. Jeśli przeglądarka się nie otworzy, moduł wypisze adres do wklejenia.
Logowanie trzeba dokończyć w ciągu 3 minut.

Token zapisuje się w `%LOCALAPPDATA%\bt-cli\token.json` i odświeża się sam w tle (refresh token); `bt logout` go czyści.
Zapisany token jest zaszyfrowany DPAPI (odczyta go tylko to samo konto Windows na tym komputerze), starszy plik z jawnym
tekstem jest przy pierwszym użyciu migrowany. Serwer tożsamości domyślnie to `https://localhost:7226` — inny adres
(np. produkcyjny) ustaw przez `BT_IDENTITY_URL`, trwale jak `BT_API_URL`:

```powershell
[Environment]::SetEnvironmentVariable('BT_IDENTITY_URL', 'https://twoj-adres-identity', 'User')
```

**Aktualizacja z wersji 1.x:** wcześniej `bt login` pytał o e-mail i hasło i wymagał `BT_CLI_CLIENT_SECRET`. Po wdrożeniu nowego
serwera tożsamości stary sposób przestaje działać — zainstaluj moduł ponownie (`.\tools\bt-cli\install.ps1`) i zaloguj się
`bt login`. Zmienną `BT_CLI_CLIENT_SECRET` możesz usunąć.

## Użycie

```powershell
bt help
bt budget list
bt limit set --category-id <guid> --amount 300 --warning-threshold 80 --valid-from 2026-09-01
bt standing-order create --rules-json '[{"titlePattern":"czynsz","amountFrom":1900,"amountTo":2100}]'
```

Składnia identyczna jak w panelu terminala w apce — pełny opis w podręczniku użytkownika, sekcja
„Terminal (CLI)" (`web/public/handbook/cli.md`).
