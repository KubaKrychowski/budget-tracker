# bt — kliencki CLI do budget-tracker (issue #25)

Instalowalny modul PowerShell dający globalną komendę `bt`, wołającą `POST /api/cli/execute` — ten sam
endpoint co panel terminala w aplikacji. Zero HTTP-owych szczegółów na co dzień: `bt <rzeczownik>
<czasownik> --flagi`, jak w panelu albo `gh`/`kubectl`.

## Instalacja

```powershell
.\tools\bt-cli\install.ps1
```

Zapyta raz o adres API (np. `http://localhost:5031` dla API z Ridera, `http://localhost:5099` dla
środowiska demo) i zapamięta go trwale (`BT_API_URL`, per-użytkownik). Otwórz nowe okno PowerShell —
`bt` jest już dostępne, bez importowania modułu i bez wpisów w `$PROFILE`.

Zmiana adresu później: `.\tools\bt-cli\install.ps1 -ApiUrl http://localhost:5099` albo ręcznie
`[Environment]::SetEnvironmentVariable('BT_API_URL', '...', 'User')`.

## Użycie

```powershell
bt help
bt budget list
bt limit set --category-id <guid> --amount 300 --warning-threshold 80 --valid-from 2026-09-01
bt standing-order create --rules-json '[{"titlePattern":"czynsz","amountFrom":1900,"amountTo":2100}]'
```

Składnia identyczna jak w panelu terminala w apce — pełny opis w podręczniku użytkownika, sekcja
„Terminal (CLI)" (`web/public/handbook/cli.md`).
