# Landing page

Strona publiczna opisująca projekt komuś, kto nie jest jego autorem. Issue
[#12](https://github.com/KubaKrychowski/budget-tracker-2-boards/issues/12),
plan i uzasadnienia: `plans/landing-page.md`.

To jest **prezentacja projektu**, nie landing produktu — aplikacja jest single-user, bez
rejestracji i bez hostingu, więc „Wypróbuj za darmo" nie miałoby dokąd prowadzić. Jedyne CTA
prowadzi do repozytorium.

```bash
cd web && npx ng serve landing --port 4300
```

## ⚠️ Zrzuty ekranu — czego NIE WOLNO zrobić

**Nie wolno zrobić zrzutu z bazy deweloperskiej.** Trzyma ona realny wyciąg autora: apteki,
przychodnie, nazwy miast, wysokość wynagrodzenia. To są dane o zdrowiu — kategoria szczególna
wg RODO — a publikacja jest nieodwracalna: cache'e i archiwa nie znają cofania.

Materiały powstają **wyłącznie** z `DemoSeed` (`api/…/Infrastructure/DemoSeed.cs`), na osobnej,
czystej bazie, ze zmyślonymi sprzedawcami. `DevSeed` też się **nie nadaje** — celowo używa
prawdziwych nazw sieci handlowych (w tym aptek i przychodni), bo daje przez to realistyczne
wejście kategoryzatorowi.

## Jak odtworzyć zrzuty

Baza demo stoi obok zwykłej i jest kasowana po wszystkim.

```bash
docker exec budget-tracker-db psql -U budget -d postgres -c "CREATE DATABASE budgettracker_demo OWNER budget;"
```

```bash
cd api && ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=budgettracker_demo;Username=budget;Password=budget_dev_only" dotnet ef database update --project src/BudgetTracker.Api
```

```bash
cd api && ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=budgettracker_demo;Username=budget;Password=budget_dev_only" Demo__Seed=true dotnet run --project src/BudgetTracker.Api --no-launch-profile --urls http://localhost:5031
```

Przy działającym `npm start` w `web/` zrzuty robi się z `/dashboard` i `/savings`.

⚠️ **Jednorazowy `chrome --headless --screenshot` NIE nadaje się do tego zadania.** ApexCharts
animuje słupki, a taki zrzut łapie wykres w połowie animacji — wychodzą słupki, które nie
zgadzają się z tabelą obok, czyli obrazek pokazujący aplikację jako zepsutą. Trzeba sterować
przeglądarką (CDP) i odczekać realnym zegarem kilka sekund po załadowaniu.

Na koniec:

```bash
docker exec budget-tracker-db psql -U budget -d postgres -c "DROP DATABASE budgettracker_demo;"
```

## Obrazek Open Graph

`og-cover.html` w katalogu projektu to szablon, z którego generuje się `public/og-cover.png`
(1200×630). Leży poza `src/` i `public/`, więc nic go nie bunduje.

⚠️ Ścieżki Open Graph w `src/index.html` są **względne**, bo domena nie jest wybrana. Slack
i LinkedIn je rozwiną, część starszych botów nie — po wyborze domeny zamień na pełne URL-e
i dopisz `og:url`.

## Czego ta strona nie może zgubić

Kryteria akceptacji #12 są w całości o uczciwości i pilnuje ich `src/app/app.spec.ts`:

- każdy punkt roadmapy niesie status, a „Gotowe" ma dokładnie jeden — dzięki temu prognoz
  i paragonów nie da się przeczytać jako funkcji istniejących,
- żadnej obietnicy salda konta — „odłożone" liczy się z kategorii, dopóki #10 jest otwarte,
- obowiązkowa sekcja „czego to nie robi",
- jedno CTA, prowadzące do kodu.

Bundle waży ok. 144 kB po kompresji, z czego większość to arkusz Ant Design. To jest cena
decyzji o Angularze i NG-ZORRO zamiast statycznego HTML — świadomej, opisanej w rewizji planu.
