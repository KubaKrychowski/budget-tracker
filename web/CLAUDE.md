# Frontend — Angular 22, NG-ZORRO 22, ngx-translate, Vitest

Uzasadnienia: `DECISIONS.md` §7. Wzór ekranu do skopiowania: `src/app/features/standing-orders/`
(albo `reservations/` — lista + modale + akcje).

## Komendy (z katalogu `web/`)

```bash
npx ng test --watch=false                                                   # wszystkie testy
npx ng test --watch=false --include src/app/features/reservations/reservations.spec.ts
npm run lint                                                                # ESLint + scripts/check-tables.mjs
npm run build
```

Nie odpalaj `npm start` (4200 to serwer użytkownika). Do przeglądarki służy `budget-tracker-web-demo` (root `CLAUDE.md`).

## Budowa ekranu

- Standalone komponent, selektor `app-…`, **sygnały**: `signal`, `computed`, `linkedSignal`, `effect`. Trasa z
  `loadComponent` w `app.routes.ts` z tytułem `… — Budżet tracker`. Nowy ekran dostępny z wyszukiwarki = wpis w
  `core/system-actions.ts` + klucz `actions.*`.
- **Odczyt z API: `httpResource`**, a wartość i błąd WYŁĄCZNIE przez `valueOf(res)` / `errorOf(res)` z
  `core/api/resource-value.ts`. ⚠️ `res.value()` w stanie błędu RZUCA — spinner kręci się wtedy w nieskończoność.
- **Zapis:** `firstValueFrom(this.http.post/put/delete(...))`, potem `resource.reload()`, komunikat
  `NzMessageService` z przetłumaczonym tekstem, błąd przez `inject(ErrorMessages).of(err)` (tekst z API albo ogólny).
- Żeby lista nie migała pustką przy przeładowaniu, trzymaj ostatnią świeżą wartość w `linkedSignal`
  (wzór: `listValue` w `features/transactions/transactions.ts`).
- Modele odpowiedzi w `core/api/models/<obszar>.ts` — nazwy i pola 1:1 z `*ResponseDto` backendu (camelCase), `readonly`.
- **Budżet:** ekran zależny od budżetu czyta `activeBudget.resolve(budgetIdZAdresu)` i publikuje wybór przez
  `activeBudget.set(...)`; przełącznik to `core/budget-switcher`, wybór idzie do adresu (`?budgetId=`).
- Potwierdzenia: `ConfirmDialogService.confirm({...})`, nie własne modale „na pewno?”.
- Nagłówek ekranu: breadcrumb `nz-breadcrumb` — szybki powrót, `Dashboard` jest korzeniem, a ścieżka może zależeć
  od pochodzenia (`?origin=`). Skopiuj układ z sąsiedniego ekranu tego samego typu.

## Pułapki NG-ZORRO i lintu (każda już raz wybuchła)

- **Każde `nz-input-number` MUSI mieć `[nzParser]="parseAmount"`** — domyślny parser zamienia `278,88` na 27888 bez objawu.
- **Ikony trzeba zarejestrować** w `core/icons.ts` (`APP_ICONS`) — niezarejestrowana po prostu się nie pokaże.
- **Nigdy samo `[nzShowPagination]="false"` na `nz-table`** — chowa przełącznik, a tabela dalej tnie do 10 wierszy.
  Zostaw stronicowanie i ustaw `[nzPageSize]` (wyjątek: paginacja serwerowa z `[nzFrontPagination]="false"`).
  Pilnuje `scripts/check-tables.mjs`.
- Lint `label-has-associated-control`: `<label nz-radio>` / `<label nz-checkbox>` nie przechodzą. Używaj
  `nz-segmented` zamiast radio, a checkbox jako `<span nz-checkbox [nzId]="'x'">` + `<label for="x">`.
- Modal: `nz-modal` z `*nzModalContent`; alert pierwszym elementem treści, przyciski stopki do prawej.
- Datepickery działają na adapterze date-fns z polską lokalizacją (`app.config.ts`) — nie dokładaj innego.

## Teksty i formatowanie

- Każdy tekst przez `| translate` / `TranslateService`, klucz dodany do **`public/i18n/pl.json` i `en.json`** jednocześnie.
  Klucze po angielsku, zagnieżdżone per ekran (`reservations.contribute.title`).
- Kwoty w komórkach tabeli: klasa `.amount` / `.tnum` (cyfry tabelaryczne), wyrównanie do prawej.
- Kolory tylko z tokenów (`src/styles/design-tokens.less`, `theme.less`); hexy nie trafiają do komponentów, poza
  wyjątkami już istniejącymi w danym `.scss`.
- ⚠️ `Intl` z `pl-PL` NIE grupuje liczb 4-cyfrowych (`2000,00`, ale `15 400,00`) — pamiętaj w oczekiwaniach testów.

## Testy (`*.spec.ts`, Vitest przez `ng test`)

- **Testuj DOM, nie sygnały** — reguła musi dojść do tekstu na ekranie. Modale NG-ZORRO renderują się poza hostem:
  czytaj `document.body.textContent`.
- HTTP przez `provideHttpClientTesting` + `HttpTestingController`. Dopasowuj żądania **po ścieżce**
  (`http.match(r => r.url === '/api/...')`) — `match('/api/x')` porównuje z parametrami, a niedopasowane żądanie
  wisi i `whenStable()` nie wraca (wygląda jak timeout).
- Tłumaczenia w teście: `translate.setTranslation('pl', {...})` z tylko potrzebnymi kluczami.
- Po `router.navigate` `whenStable` potrafi zawisnąć — wtedy krótkie `await new Promise(r => setTimeout(r))`.
- Test na regresję ma paść bez poprawki: cofnij na chwilę zmianę i sprawdź.
