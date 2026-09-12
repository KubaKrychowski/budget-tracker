using Xunit;

// Testy jadą na JEDNYM serwerze Postgresa, a każda klasa testowa odtwarza swoją bazę
// (`EnsureDeletedAsync` + `EnsureCreatedAsync`) przed KAŻDYM testem. Domyślnie xUnit
// uruchamia klasy równolegle, więc kilka `DROP DATABASE` / `CREATE DATABASE` trafiało na
// siebie nawzajem: Postgres blokuje wtedy szablon bazy i zrywa połączenia, co wychodziło
// jako „database ... does not exist" albo „terminating connection due to administrator
// command" — błędy infrastruktury, nie logiki.
//
// To był wyścig obecny od początku; ujawnił się dopiero, gdy klas testowych zrobiło się
// więcej. Sekwencyjne wykonanie kosztuje kilka sekund i usuwa go całkowicie.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
