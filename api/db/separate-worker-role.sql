-- Rozdzielenie roli aplikacji od roli zadan systemowych (patrz api/src/BudgetTracker.Api/Infrastructure/SystemDb.cs).
--
-- Po co: dotad budget_app mogla wykonac `SET ROLE budget_jobs` (BYPASSRLS), wiec wyciek connection stringa
-- budget_app dawal dostep do danych WSZYSTKICH uzytkownikow. Ten skrypt zaklada osobna role budget_worker
-- (LOGIN + BYPASSRLS, wlasne haslo) i odbiera budget_app prawo przelaczenia sie w budget_jobs.
--
-- KOLEJNOSC (wazna, w innej kolejnosci zadania systemowe przestana dzialac):
--   1. Uruchom KROK 1 (ponizej): zaklada budget_worker. Nic jeszcze nie zmienia dla dzialajacej aplikacji.
--   2. W Azure ustaw ConnectionStrings__PostgresWorker (rola budget_worker) w API i wdroz aplikacje.
--      Log startu nie ma juz ostrzezenia o braku PostgresWorker.
--   3. Sprawdz: import, sprzatanie budzetow, ekran administratora (usuniecie danych wlasciciela).
--   4. Dopiero teraz KROK 2: `REVOKE budget_jobs FROM budget_app`.
--
-- Uruchamiaj polaczony jako wlasciciel bazy, PO backupie (`pg_dump`):
--     psql "<connection-string-wlasciciela>" -v worker_password='<haslo>' -f api/db/separate-worker-role.sql
-- Haslo nigdy nie trafia do repo.

\if :{?worker_password}
\else
    \echo 'BRAK HASLA. Uruchom z: psql ... -v worker_password=''<haslo>'' -f api/db/separate-worker-role.sql'
    \quit
\endif

-- KROK 1 ---------------------------------------------------------------------------------------------------------
SELECT NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'budget_worker') AS trzeba_zalozyc \gset

\if :trzeba_zalozyc
    CREATE ROLE budget_worker WITH LOGIN BYPASSRLS PASSWORD :'worker_password';
\else
    \echo 'budget_worker juz istnieje - haslo NIE jest zmieniane. Zmiana: ALTER ROLE budget_worker WITH PASSWORD ...'
\endif

-- Uprawnienia do tabel dziedziczy z budget_jobs (BYPASSRLS jest ATRYBUTEM roli i NIE dziedziczy sie przez
-- czlonkostwo, dlatego budget_worker ma go wprost powyzej). Domyslne uprawnienia do przyszlych tabel
-- (grant-app-privileges.sql) idą do budget_jobs, wiec budget_worker dostaje je razem z nimi.
GRANT budget_jobs TO budget_worker;

-- KROK 2 (uruchom dopiero po kroku 3 z listy wyzej) ---------------------------------------------------------------
-- Odkomentuj i uruchom osobno:
--
-- REVOKE budget_jobs FROM budget_app;
--
-- Sprawdzenie po odebraniu, polaczony jako budget_app: `SET ROLE budget_jobs;` musi zwrocic blad
-- "permission denied to set role". Jesli nie zwraca, izolacja nadal nie chroni przed wyciekiem stringa.
