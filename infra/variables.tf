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

variable "location" {
  description = <<-EOT
    Region dla App Service i konta magazynu.
    ⚠️ Limit 60 minut CPU na dobę w planie F1 jest liczony PER REGION PER SUBSKRYPCJA i dzielony między
    wszystkie darmowe aplikacje w tym regionie. Jeśli masz już gdzieś darmowe App Service, postawienie
    tego w tym samym regionie zabiera im budżet.
  EOT
  type        = string
  default     = "westeurope"
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

variable "admin_client_secret" {
  description = "Sekret klienta `budgettracker-admin` (client credentials Identity → API). Losowy, długi."
  type        = string
  sensitive   = true
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
    Serwer poczty wychodzącej dla potwierdzeń adresu, resetu hasła i kodów 2FA.
    ⚠️ Azure nie ma darmowego SMTP, a App Service blokuje port 25. Potrzebny dostawca z zewnątrz
    (np. Brevo, Resend, Mailgun) na porcie 587.
  EOT
  type = object({
    host          = string
    port          = number
    use_start_tls = bool
    from_address  = string
    from_name     = string
    username      = string
    password      = string
  })
  sensitive = true
}
