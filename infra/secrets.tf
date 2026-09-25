# Sekrety klientów OAuth, których NIKT nie musi znać.
#
# Oba są wewnętrzne: `budgettracker-admin` służy wyłącznie serwerowi tożsamości do wołania /api/admin
# API budżetu, a sekret jest jednocześnie zapisywany w bazie (seeder uzgadnia klienta przy każdym starcie)
# i odczytywany przez ten sam proces. Nie ma drugiej strony, która musiałaby go dostać — więc nie ma powodu,
# żeby człowiek go wymyślał, przenosił i pilnował.
#
# ⚠️ `budgettracker-cli` jest tu NIE dlatego, że wdrażamy CLI, tylko dlatego, że OpenIddictSeeder zakłada
# tego klienta BEZWARUNKOWO i rzuca wyjątkiem przy braku sekretu. Bez tej wartości serwer tożsamości nie
# wstanie na świeżej bazie — niezależnie od tego, czy ktokolwiek zamierza używać `bt`.

resource "random_password" "admin_client" {
  length  = 48
  special = false
}

resource "random_password" "cli_client" {
  length  = 48
  special = false
}
