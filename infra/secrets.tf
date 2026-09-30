# Sekret klienta OAuth, którego NIKT nie musi znać.
#
# `budgettracker-admin` służy wyłącznie serwerowi tożsamości do wołania /api/admin API budżetu, a sekret
# jest jednocześnie zapisywany w bazie (seeder uzgadnia klienta przy każdym starcie) i odczytywany przez
# ten sam proces. Nie ma drugiej strony, która musiałaby go dostać — więc nie ma powodu, żeby człowiek go
# wymyślał, przenosił i pilnował.
#
# ⚠️ `bt-cli` NIE ma tu sekretu: to klient publiczny (kod + PKCE na loopbacku), bo CLI jest dla wszystkich
# użytkowników. Do 2026-09-30 klient miał sekret losowany tutaj, którego nikt nie znał — więc `bt` nigdy nie
# dało się zalogować do produkcji. Po wdrożeniu Terraforma `random_password.cli_client` zniknie ze stanu.

resource "random_password" "admin_client" {
  length  = 48
  special = false
}
