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

ALTER DEFAULT PRIVILEGES FOR ROLE budget IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO budget_app, budget_jobs;
ALTER DEFAULT PRIVILEGES FOR ROLE budget IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO budget_app, budget_jobs;
