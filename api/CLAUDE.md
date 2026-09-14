# Backend — .NET 10, Minimal API, EF Core + Postgres 18

Uzasadnienia: `DECISIONS.md` §4–§6. Tu jest to, co trzeba wiedzieć, żeby pisać kod zgodny z resztą.

## Komendy

Rider trzyma otwarte API i blokuje `bin/`, więc **buduj do katalogów tymczasowych** (PowerShell, z korzenia repo):

```powershell
$o = "-p:BaseOutputPath=$env:TEMP\bt\bin\", "-p:IntermediateOutputPath=$env:TEMP\bt\obj\"
dotnet build api/src/BudgetTracker.Api @o
dotnet test api/tests/BudgetTracker.Api.Tests @o                                   # całość, wymaga Dockera
dotnet test api/tests/BudgetTracker.Api.Tests @o --filter "FullyQualifiedName~SavingsReservationTests"
```

Migracje (`dotnet ef` jest zainstalowany globalnie; te same katalogi tymczasowe przez zmienne środowiskowe):

```powershell
$env:BaseOutputPath = "$env:TEMP\bt\bin\"; $env:IntermediateOutputPath = "$env:TEMP\bt\obj\"
dotnet ef migrations add <NazwaPoAngielsku> --project api/src/BudgetTracker.Api --output-dir Infrastructure/Migrations
Remove-Item Env:BaseOutputPath; Remove-Item Env:IntermediateOutputPath
```

## Jak zbudowany jest feature

Wzór do skopiowania: `Features/StandingOrders/`.

```
Features/<Feature>/
├── <Feature>Module.cs     # Add<Feature>() — DI; Map<Feature>() — endpointy. Program.cs tylko to woła.
├── Commands/              # <Verb><Noun>CommandHandler — zmiana stanu, metoda HandleAsync
├── Queries/               # Get<Noun>QueryHandler — odczyt
├── Services/              # krok wspólny kilku handlerów (także klasa statyczna bez stanu)
├── Contracts/             # TYLKO kontrakt HTTP; każdy typ kończy się RequestDto albo ResponseDto
├── Models/                # typy wewnętrzne, nigdy nie wychodzą przez API
├── Consts/                # enumy ekranowe i stałe
└── Exceptions/            # wyjątki feature'a
```

- **Jeden typ najwyższego poziomu na plik.** DTO to `sealed record`.
- Endpoint woła handler wprost (bez MediatR), **nie łapie wyjątków** i deklaruje kody przez `.Produces(...)`.
  Każdy endpoint ma `.WithName(...)`. Nowy handler/serwis rejestruj w `Add<Feature>()` jako `AddScoped`.
- Zasoby adresujemy `{id:guid}` = `BusinessId`. **`int Id` nigdy nie wychodzi z API.**
- Budżet z zapytania rozstrzyga `*BudgetScope` feature'a (reguła budżetu domyślnego) — nie pisz tego od nowa.

## Encje i baza

- Encja dziedziczy po `Entity` (`BusinessId` = `Guid.CreateVersion7()`, `DeletedAt`). Filtr soft delete
  i unikalny indeks `BusinessId` zakłada pętla w `AppDbContext` — nic nie dopisujesz.
- **Bez publicznych setterów:** `{ get; protected set; }`, wymagane pola konstruktorem, zmiana stanu metodą
  o nazwie operacji (`Settle`, `Contribute`, `Disable`). Encja nie zna zegara, bazy ani progów — dostaje je w argumentach;
  decyzje feature'a podejmuje handler.
- ⚠️ EF materializuje encję przez konstruktor, dopasowując **nazwy parametrów do nazw właściwości**.
  Zmiana nazwy parametru wywala się w runtime, nie w kompilacji.
- **Relacje bez nawigacji** — sam klucz (`BudgetBusinessId`, `CategoryId`). Nazwy dociągasz złączeniem.
- Kasowanie = `db.Remove(x)` (interceptor zamienia na stempel `DeletedAt`); dzieci stempluje handler jawnie.
  `DeleteBehavior.Restrict` domyślnie. Indeks unikalny musi być częściowy (`WHERE "DeletedAt" IS NULL`).
- Nowa encja-dziecko budżetu → dopisz ją do `Features/Budgets/Services/BudgetChildren.cs` i `BudgetPurger.cs`
  (reset zostawia ZASADY budżetu — cele, rezerwacje, zlecenia — usunięcie zabiera wszystko).
- Typy: kwota `decimal` → `numeric(18,2)`, data `DateOnly` → `date`, znacznik czasu → `timestamptz`. Nigdy `double` na pieniądze.
- Lista wartości w wierszu → `ComplexCollection(...).ToJson()` (jsonb). ⚠️ EF generuje dla takiej kolumny
  `defaultValue: "{}"` — **popraw na `"[]"`**, inaczej istniejące wiersze nie zmaterializują się jako lista.
- Enum zapisywany w bazie: `Domain/Consts/`, członkowie **numerowani jawnie od 1**, w bazie jako kod słownika.
  Zmiana nazwy członka wymaga migracji danych.
- **Migracja:** po wygenerowaniu PRZECZYTAJ ją (defaulty, `AlterColumn` na danych), dopisz XML `<summary>`/`<remarks>`
  z tym, co robi z istniejącymi danymi. Przekształcenie danych pisz ręcznie SQL-em w migracji.

## Błędy i teksty

- Walidacja domenowa rzuca wyjątek feature'a (np. `ContributionAmountInvalidException`) — **bez tekstu dla użytkownika**.
- Mapowanie wyjątek → (kod HTTP, klucz zasobu) dopisz w `Infrastructure/DomainExceptionHandler.cs` (`Map`).
  400 = złe wejście; 404 = nieznany `BusinessId` w ADRESIE (dziedzicz po `EntityNotFoundException`);
  409 = poprawne żądanie, ale stan zasobu nie pozwala; identyfikator w CIELE żądania, którego nie ma = 400.
- Tekst pod kluczem `Obszar_Opis` dodaj do **obu** plików: `Resources/SharedResource.resx` (en) i `SharedResource.pl.resx`.

## Komentarze

- **Tylko XML doc** na typach i członkach, po polsku. Pułapka/uzasadnienie → `<remarks>` członka, którego dotyczy.
  Bez komentarzy w ciele metod. Ostrzeżenie przed nieoczywistą pułapką zaczynaj od `⚠️`.

## Testy

- xUnit na prawdziwym Postgresie z Dockera. Każda klasa ma własną bazę `budgettracker_<cos>_test`
  (inna nazwa zostanie odrzucona) i odtwarza ją przez `TestDatabase.ResetAsync`. Równoległość wyłączona w `AssemblyInfo.cs`.
- Handlery tworzy się w teście wprost (`new XCommandHandler(db, ...)`), czas przez `FakeTimeProvider`.
  Wzorzec: `SavingsReservationTests.cs`, `EpisodicOrdersTests.cs`.
- Nazwa testu opisuje regułę zdaniem, jak sąsiednie testy w klasie (`Reset_leaves_the_goal_and_reservations_alone`).
  Komentarz w teście mówi, JAKI błąd łapie.
- Test sprawdzający kasowanie potrzebuje podpiętego `SoftDeleteInterceptor`.
- Błąd „Infrastruktura testów, NIE logika” = coś trzyma bazę albo Docker nie działa — nie poprawiaj logiki.

## Seedy i dane

- `BaselineSeed` — kategorie i reguły dla KAŻDEGO użytkownika (w repo). `DemoSeed` — zmyślone dane pod demo
  i zrzuty landingu. `data/category-rules.json` — prywatne reguły instalacji, poza repo.
- Identyfikatory seedów: `DeterministicGuid.For(nazwa)`, nie literały Guid.
- Nowy bank = nowy `IStatementParser` w `Features/Import` + rejestracja w `AddImport`; handler importu zostaje nietknięty.
  Fikstura wyciągu w `tests/.../testdata/` musi być **zmyślona**.
