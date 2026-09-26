-- Schemat Hangfire zakladany PRZEZ WLASCICIELA, nie przez aplikacje.
--
-- Hangfire przy starcie sam instaluje swoje tabele i probuje w tym celu wykonac CREATE SCHEMA.
-- Aplikacja laczy sie rola `budget_app`, ktora nie ma prawa CREATE na bazie - i nie ma go miec,
-- bo na braku DDL opiera sie caly podzial rol pod RLS (patrz setup-rls-roles.sql). Bez tego skryptu
-- start konczy sie petla: "42501: permission denied for database <baza>" przy CREATE SCHEMA "hangfire".
--
-- ⚠️ Uprawnienie CREATE nadajemy WYLACZNIE na tym jednym schemacie. Hangfire musi tworzyc w nim swoje
-- tabele przy aktualizacji wersji, ale poza `hangfire` rola dalej nie zalozy niczego - w szczegolnosci
-- nie dotknie schematu `public`, gdzie stoja dane objete politykami RLS.
--
-- Uruchom RAZ na baze, rola wlasciciela:
--   psql "<connection-string-wlasciciela>" -f api/db/setup-hangfire-schema.sql

CREATE SCHEMA IF NOT EXISTS hangfire;

GRANT USAGE, CREATE ON SCHEMA hangfire TO budget_app;

-- Zadania w tle chodza pod `budget_jobs` (SET LOCAL ROLE), wiec ona tez musi widziec kolejke.
GRANT USAGE ON SCHEMA hangfire TO budget_jobs;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA hangfire TO budget_app, budget_jobs;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA hangfire TO budget_app, budget_jobs;

-- Tabele, ktore Hangfire zalozy PO tym skrypcie, maja od razu dostac te same uprawnienia.
DO $$
BEGIN
    EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA hangfire '
        'GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO budget_app, budget_jobs',
        current_user);

    EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA hangfire '
        'GRANT USAGE, SELECT ON SEQUENCES TO budget_app, budget_jobs',
        current_user);
END
$$;
