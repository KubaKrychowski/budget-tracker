## Terminal (CLI)

Terminal to **wiersz poleceń wbudowany w aplikację**. Każdą operację, którą robisz klikaniem, możesz wykonać jedną linią tekstu. Przydaje się, gdy chcesz coś zrobić szybko, zautomatyzować powtarzalne zadania albo oddać obsługę aplikacji skryptowi czy asystentowi AI.

> 💡 **Nie musisz go używać.** Wszystko, co jest w terminalu, jest też dostępne przez zwykły interfejs.

### Jak go otworzyć

Kliknij ikonę **terminala** w górnym pasku. Z prawej strony wysunie się panel — wpisz komendę i naciśnij **Enter**.

![Panel terminala z wynikiem komendy „budget list”](handbook/images/cli-terminal.png)

Zamkniesz go przyciskiem **✕**. Strzałki **↑/↓** przewijają ostatnio wpisane komendy tej karty (pamięć znika po zamknięciu karty).

### Pierwsze komendy do wypróbowania

```
help
budget list
```

- `help` — pełna lista komend z opisem, przykładem i flagami. **Zacznij od niej, ilekroć nie pamiętasz nazwy komendy.**
- `budget list` — lista Twoich budżetów.
- `budget help` — komendy dotyczące samych budżetów (działa dla każdego „rzeczownika”).

### Składnia

```
rzeczownik czasownik --flaga wartość --inna-flaga wartość
```

Przykłady:

```
transaction list --budget-id <guid> --category-id <guid>
limit set --category-id <guid> --amount 300 --warning-threshold 80 --valid-from 2026-09-01
episodic-order create --name "Nowy laptop" --category-id <guid> --amount 4200 --due-month 2026-12
```

Zasady:

- **Wartość ze spacją** bierz w cudzysłów: `--name "Nowy laptop"`.
- **Pole złożone** (np. reguły dopasowania) idzie jako surowy JSON w **pojedynczym** cudzysłowie: `--rules-json '[{"titlePattern":"czynsz","amountFrom":1900,"amountTo":2100}]'`. Pojedynczy cudzysłów jest celowy — JSON ma w środku własne podwójne.
- **Identyfikator** (`<id>`) podajesz jako zwykły argument: `budget disable <id>`.
- Identyfikatory (GUID) poznasz z odpowiedzi komend `list`.

### Co, jeśli coś pójdzie nie tak

Zła flaga albo zły format (np. tekst tam, gdzie oczekiwany jest identyfikator) pokazuje **krótki komunikat** zamiast surowego błędu. Reguły biznesowe (np. „ta nazwa jest wymagana”) dają dokładnie ten sam komunikat co odpowiedni ekran — terminal korzysta z tych samych mechanizmów co przyciski w aplikacji, więc żadna walidacja nie jest tu słabsza.

### Poza przeglądarką

Panel w aplikacji to tylko jeden z klientów. Ten sam mechanizm działa **z prawdziwej powłoki** — skryptem, `curl`-em albo innym programem — bez otwierania aplikacji (`POST /api/cli/execute`, ciało `{"line": "..."}`).

Dla powłoki PowerShell jest gotowy klient: katalog `tools/bt-cli/` w repozytorium, instalowany jednym `install.ps1` (jak `az` czy `gh` — instalujesz raz, potem `bt` działa w każdym nowym oknie). Składnia ta sama, tylko bez `curl`-a i JSON-a na wejściu:

```
bt help
bt budget list
bt limit set --category-id <guid> --amount 300 --warning-threshold 80 --valid-from 2026-09-01
```

Szczegóły instalacji i konfiguracji adresu API znajdziesz w `tools/bt-cli/README.md`.
