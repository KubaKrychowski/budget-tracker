using Xunit;

// Testy jadą na JEDNYM serwerze Postgresa, a każda klasa testowa odtwarza swoją bazę przed
// KAŻDYM testem. Domyślnie xUnit uruchamia klasy równolegle, więc kilka `DROP DATABASE` /
// `CREATE DATABASE` trafiało na siebie nawzajem — stąd sekwencyjne wykonanie.
//
// ⚠️ To NIE wystarczało i komentarz w tym miejscu twierdził kiedyś, że „usuwa wyścig całkowicie".
// Wyścig MIĘDZY klasami tak, ale zostały trzy mechanizmy dające ten sam objaw (rzadki błąd
// infrastruktury, dłuższy przebieg): martwe połączenie z puli Npgsql do bazy odtworzonej pod tą
// samą nazwą, obca sesja na usuwanej bazie i obca sesja na `template1`. Wszystkie trzy zamyka
// `TestDatabase` — i tam jest opis, a klasy testowe nie odtwarzają już baz same.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
