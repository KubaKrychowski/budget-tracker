## Terminal (CLI)

Ikona terminala w nagłówku otwiera panel, w którym da się wykonać **każdą operację, jaką robi się
klikaniem** — jedną linią tekstu. Powstał z myślą o automatyzacji i o AI, które ma korzystać
z aplikacji bez klikania w interfejs, ale działa tak samo dobrze wpisywany ręcznie.

### Składnia

```
rzeczownik czasownik --flaga wartość --inna-flaga wartość
```

Na przykład:

```
budget list
transaction list --budget-id <guid> --category-id <guid>
limit set --category-id <guid> --amount 300 --warning-threshold 80 --valid-from 2026-09-01
```

- Wartość ze spacją bierz w cudzysłów: `--name "Nowy laptop"`.
- Pole złożone (np. reguły dopasowania) idzie jako surowy JSON w **pojedynczym** cudzysłowie:
  `--rules-json '[{"titlePattern":"czynsz","amountFrom":1900,"amountTo":2100}]'`. Pojedynczy
  cudzysłów jest celowy — JSON ma własne podwójne cudzysłowy w środku, więc podwójny by się z nimi
  pogryzł.
- `<id>` z adresu REST (np. `{businessId}`) jest tu argumentem pozycyjnym: `budget disable <id>`.

### `help`

Samo `help` zwraca pełną listę dostępnych komend z opisem, przykładem użycia i listą flag. `<rzeczownik>
help` (np. `budget help`) zawęża listę do jednego rzeczownika. Nie trzeba znać składni z góry —
zaczynaj od `help`, ilekroć nie pamiętasz dokładnej nazwy flagi.

### Historia

Strzałki góra/dół przewijają ostatnio wpisane komendy tej karty — pamięć wygasa z zamknięciem karty
(nie jest to coś, co ma przetrwać między sesjami).

### Błędy

Zła flaga albo zły format (np. nie-GUID tam, gdzie oczekiwany jest identyfikator) pokazuje krótki
komunikat zamiast surowego JSON-a. Reguły biznesowe (np. „ta nazwa jest wymagana") dają dokładnie
ten sam komunikat co odpowiedni ekran — terminal woła te same handlery co przyciski w aplikacji,
więc żadna walidacja nie jest tu inna ani słabsza.

### Poza przeglądarką

Panel w nagłówku to tylko jeden z klientów. Ten sam endpoint (`POST /api/cli/execute`, ciało
`{"line": "..."}`) da się wywołać spoza przeglądarki — skryptem, `curl`-em albo innym agentem — bez
otwierania aplikacji w ogóle.

Do pracy z prawdziwej powłoki jest gotowy klient: `tools/bt-cli/` w repo, moduł PowerShell instalowany
jednym `install.ps1` (jak `az`/`gh` — instalujesz raz, potem `bt` działa w każdym nowym oknie). Ta sama
składnia co tutaj, tylko bez `curl`-a i JSON-a na wejściu:

```
bt help
bt budget list
bt limit set --category-id <guid> --amount 300 --warning-threshold 80 --valid-from 2026-09-01
```

Szczegóły instalacji i konfiguracji adresu API w `tools/bt-cli/README.md`.
