-- Role Postgresa pod RLS (row-level security) na Budgets i jego dzieciach — patrz migracja
-- AddChildUserIdAndRls i DECISIONS.md. NIE jest to migracja EF Core: role są własnością całego
-- KLASTRA Postgresa, nie jednej bazy, więc uruchamia się to ręcznie, RAZ na klaster (nie per baza),
-- zanim migracja AddChildUserIdAndRls trafi na dane środowisko.
--
-- ⚠️ REWIZJA: pierwsza wersja tego skryptu próbowała zdjąć SUPERUSER z roli `budget`. Postgres na to
-- NIE POZWALA — `budget` jest "bootstrap superuserem" klastra (rolą założoną przez POSTGRES_USER przy
-- initdb), a taka rola musi zostać superuserem na zawsze (sprawdzone empirycznie: "permission denied to
-- alter role — the bootstrap superuser must have the SUPERUSER attribute"). Superuser ZAWSZE omija RLS,
-- więc appka łącząca się jako `budget` nigdy nie podlegałaby politykom, niezależnie od FORCE ROW LEVEL
-- SECURITY. Stąd inny podział ról niż pierwotnie planowany:
--   * budget       — zostaje tym, czym już jest: superuser, właściciel wszystkich tabel. Appka PRZESTAJE
--                    się nim łączyć (patrz appsettings) — od teraz to WYŁĄCZNIE Twoje konto administracyjne
--                    do psql/DBeaver, dokładnie jak dziś, tylko appka mu nie ufa.
--   * budget_app   — NOWA rola, którą appka łączy się na co dzień. Zwykła (nie superuser, nie
--                    BYPASSRLS) — RLS jej dotyczy naprawdę.
--   * budget_jobs  — NOLOGIN, BYPASSRLS — dla zadań Hangfire (BudgetPurger), które celowo działają
--                    bez kontekstu użytkownika i sprzątają WSZYSTKICH. Appka wchodzi w nią jawnie przez
--                    `SET LOCAL ROLE budget_jobs` na czas jednej transakcji.
--
-- ⚠️ Hasło budget_app NIGDY nie trafia do repo w tym pliku — appka bierze je z connection stringa
-- (appsettings/user-secrets/zmienne środowiskowe), tak jak każdy inny sekret w tym projekcie.
-- Podmień poniżej PRZED uruchomieniem, jeśli nie chcesz zostać przy `budget_app_dev_only`.

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'budget_app') THEN
        CREATE ROLE budget_app WITH LOGIN PASSWORD 'budget_app_dev_only';
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'budget_jobs') THEN
        CREATE ROLE budget_jobs WITH NOLOGIN BYPASSRLS;
    END IF;
END
$$;

-- Appka jako budget_app musi umieć jawnie wejść w budget_jobs (SET LOCAL ROLE w BudgetPurger) —
-- jednokierunkowe, w drugą stronę Postgres odrzuca to jako cykl członkostwa.
GRANT budget_jobs TO budget_app;

-- budget_admin z pierwszej wersji tego skryptu jest zbędny: budget sam w sobie (superuser) już
-- jest tym, czego potrzebowałeś do ręcznego podglądu wszystkiego — nie trzeba drugiej roli do tego.
DROP ROLE IF EXISTS budget_admin;
