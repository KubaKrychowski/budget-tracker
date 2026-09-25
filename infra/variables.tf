variable "subscription_id" {
  description = "Identyfikator subskrypcji Azure."
  type        = string
}

variable "project" {
  description = "Przedrostek nazw zasobów."
  type        = string
  default     = "wydatki"
}

variable "environment" {
  description = "Nazwa środowiska; wchodzi w nazwy zasobów i adresy."
  type        = string
  default     = "beta"
}

variable "resource_group_name" {
  description = <<-EOT
    ISTNIEJĄCA grupa zasobów, w której staną App Service, Static Web Apps i konto magazynu na modele.
    Terraform jej NIE tworzy i nie usunie — ma stać obok rzeczy, których nie zarządza (ACS z pocztą).

    Region wszystkich zasobów bierze się z tej grupy, więc jej wybór to też wybór regionu.
    ⚠️ Limit 60 minut CPU na dobę w planie F1 jest liczony PER REGION PER SUBSKRYPCJA i dzielony między
    wszystkie darmowe aplikacje w tym regionie. Jeśli masz już gdzieś plan F1, wybranie tego samego
    regionu znaczy, że oba projekty jedzą z jednego budżetu.
  EOT
  type        = string
}

variable "static_web_apps_location" {
  description = <<-EOT
    Region Static Web Apps. ⚠️ To OSOBNA zmienna, bo usługa istnieje tylko w kilku regionach:
    westeurope, eastus2, centralus, westus2, eastasia. Wpisanie tu regionu App Service (np. polandcentral)
    kończy się błędem przy `apply`, a nie ostrzeżeniem.
  EOT
  type        = string
  default     = "westeurope"

  validation {
    condition     = contains(["westeurope", "eastus2", "centralus", "westus2", "eastasia"], var.static_web_apps_location)
    error_message = "Static Web Apps działa tylko w: westeurope, eastus2, centralus, westus2, eastasia."
  }
}

# ---------------------------------------------------------------------------------------------------
# Baza: Neon, poza Terraformem. Terraform dostaje gotowe connection stringi i tylko je wstrzykuje.
# ---------------------------------------------------------------------------------------------------

variable "postgres_connection_string_api" {
  description = <<-EOT
    Connection string do bazy budżetu. ⚠️ Rola `budget_app`, NIE właściciel bazy: RLS nie dotyczy
    właściciela tabel, więc połączenie właścicielem po cichu wyłącza całą izolację danych między kontami.
  EOT
  type        = string
  sensitive   = true
}

variable "postgres_connection_string_identity" {
  description = "Connection string do bazy serwera tożsamości (osobna baza od budżetowej)."
  type        = string
  sensitive   = true
}

# ---------------------------------------------------------------------------------------------------
# Serwer tożsamości
# ---------------------------------------------------------------------------------------------------

variable "admin_emails" {
  description = "Adresy, które dostają rolę administratora po potwierdzeniu adresu e-mail."
  type        = list(string)

  validation {
    condition     = length(var.admin_emails) > 0
    error_message = "Bez adresu administratora nie da się wejść do panelu, a przy zamkniętej becie nikt nie założy konta."
  }
}

variable "registration_closed_beta" {
  description = <<-EOT
    `true` = konto założy tylko adres z listy zaproszeń (plus adresy z admin_emails).
    ⚠️ Zmiana na `false` otwiera rejestrację dla każdego, kto zna adres serwera.
  EOT
  type        = bool
  default     = true
}

variable "token_certificate_validity_hours" {
  description = <<-EOT
    Ważność certyfikatów podpisującego i szyfrującego tokeny (patrz certificates.tf).
    Domyślnie 5 lat.

    ⚠️ Wymiana tych certyfikatów UNIEWAŻNIA wszystkie wydane tokeny i wylogowuje wszystkich. Dlatego
    ważność jest długa, a `early_renewal_hours = 0` — Terraform nie wymieni ich sam przy okazji
    niepowiązanego `apply`. Po wygaśnięciu serwer tożsamości NIE WSTANIE (loader odrzuca przeterminowany
    certyfikat), więc wymiana jest planowanym zadaniem, a nie awarią do odkrycia.
  EOT
  type        = number
  default     = 43800

  validation {
    condition     = var.token_certificate_validity_hours >= 720
    error_message = "Ważność poniżej 30 dni znaczy wylogowywanie wszystkich co miesiąc — to prawie na pewno pomyłka."
  }
}

# ---------------------------------------------------------------------------------------------------
# Poczta — Identity ma ValidateOnStart na tej sekcji, więc bez niej serwer NIE WSTANIE.
# ---------------------------------------------------------------------------------------------------

variable "smtp" {
  description = <<-EOT
    Serwer poczty wychodzącej dla potwierdzeń adresu, resetu hasła i kodów 2FA — część JAWNA.

    Domyślnie przekaźnik SMTP Azure Communication Services. ⚠️ Port 25 odpada: App Service go blokuje.
    `from_address` przy domenie zarządzanej przez Azure ma postać `DoNotReply@<guid>.azurecomm.net`
    i odczytasz go w zasobie Email Service jako `mailFromSenderDomain`.

    Dane logowania są osobno (`smtp_username`, `smtp_password`) — patrz komentarz przy nich.
  EOT
  type = object({
    host          = string
    port          = number
    use_start_tls = bool
    from_address  = string
    from_name     = string
  })
  default = {
    host          = "smtp.azurecomm.net"
    port          = 587
    use_start_tls = true
    from_address  = ""
    from_name     = "Wydatki.pl"
  }

  validation {
    condition     = length(var.smtp.from_address) > 0
    error_message = "Podaj from_address — bez adresu nadawcy serwer tożsamości nie wyśle potwierdzenia rejestracji."
  }
}

# ---------------------------------------------------------------------------------------------------
# Sekrety wydzielone z obiektów. Każdy jest osobną zmienną skalarną, żeby dało się go podać przez
# zmienną środowiskową `TF_VAR_<nazwa>` i nie zapisywać w żadnym pliku:
#
#   TF_VAR_smtp_password, TF_VAR_postgres_connection_string_api, TF_VAR_smtp_username, …
#
# ⚠️ To trzyma sekret poza repozytorium i poza `terraform.tfvars`, ale NIE poza stanem Terraforma
# i NIE poza ustawieniami App Service — tam musi trafić, żeby aplikacja działała. Zmienna środowiskowa
# zmienia to, kto widzi wartość PO DRODZE, a nie to, gdzie ona ostatecznie leży.
# ---------------------------------------------------------------------------------------------------

variable "smtp_username" {
  description = <<-EOT
    Nazwa zasobu „SMTP Username" z ACS (portal → zasób ACS → *SMTP Usernames*).
    ⚠️ To NIE jest identyfikator rejestracji aplikacji ani adres e-mail konta.
  EOT
  type        = string
  sensitive   = true
}

variable "smtp_password" {
  description = "Sekret klienta rejestracji aplikacji w Entra ID powiązanej z tym SMTP Username."
  type        = string
  sensitive   = true
}
