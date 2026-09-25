-- Uruchom PO api/db/setup-rls-roles.sql i PO migracjach EF na danej bazie (budget_app/budget_jobs
-- nie są właścicielami tabel — te należą do `budget`, więc bez tych GRANT-ów appka dostawałaby
-- "permission denied" jeszcze zanim RLS zdążyłoby cokolwiek przefiltrować).
--
-- W odróżnieniu od setup-rls-roles.sql, TEN skrypt jest per baza — uprawnienia do tabel żyją
-- w konkretnej bazie, nie w klastrze. Uruchom go osobno na każdej: dev, budgettracker_uiverify,
-- każdej budgettracker_*_test, i na końcu na budgettracker (wyłącznie po wyraźnej zgodzie i
-- backupie — patrz root CLAUDE.md).
--
-- Domyślne uprawnienia (ALTER DEFAULT PRIVILEGES) sprawiają, że KOLEJNE migracje (nowe tabele,
-- zawsze tworzone przez `budget`) automatycznie odziedziczą te same GRANT-y — nie trzeba pamiętać
-- o odpaleniu tego skryptu po każdej migracji, tylko raz na bazę.

GRANT USAGE ON SCHEMA public TO budget_app, budget_jobs;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO budget_app, budget_jobs;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO budget_app, budget_jobs;

-- ⚠️ Właściciel tabel NIE jest wpisany na sztywno. Do 2026-09-25 stało tu `FOR ROLE budget` — nazwa
-- z lokalnego kontenera. Na hostowanym Postgresie właściciel nazywa się inaczej (u Neona `neondb_owner`),
-- a `ALTER DEFAULT PRIVILEGES FOR ROLE budget` na takiej bazie kończy się błędem „role does not exist".
-- Gorszy wariant tej pomyłki: skrypt przechodzi na roli, która NIE tworzy tabel, wtedy GRANT-y wyżej
-- działają, domyślne uprawnienia po cichu nie, i pierwsza KOLEJNA migracja zostawia appce tabelę,
-- do której nie ma dostępu.
--
-- `current_user` jest tu poprawnym źródłem, bo ten skrypt uruchamia się TĄ SAMĄ rolą, którą jadą
-- migracje EF — czyli właścicielem tworzonych tabel.
DO $$
BEGIN
    EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public '
        'GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO budget_app, budget_jobs',
        current_user);

    EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA public '
        'GRANT USAGE, SELECT ON SEQUENCES TO budget_app, budget_jobs',
        current_user);

    RAISE NOTICE 'Domyslne uprawnienia ustawione dla wlasciciela: %', current_user;
END
$$;
