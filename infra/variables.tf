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

# ---------------------------------------------------------------------------------------------------
# Sekrety wydzielone z obiektów. Każdy jest osobną zmienną skalarną, żeby dało się go podać przez
# zmienną środowiskową `TF_VAR_<nazwa>` i nie zapisywać w żadnym pliku:
#
#   TF_VAR_postgres_connection_string_api, TF_VAR_postgres_connection_string_identity
#
# ⚠️ To trzyma sekret poza repozytorium i poza `terraform.tfvars`, ale NIE poza stanem Terraforma
# i NIE poza ustawieniami App Service — tam musi trafić, żeby aplikacja działała. Zmienna środowiskowa
# zmienia to, kto widzi wartość PO DRODZE, a nie to, gdzie ona ostatecznie leży.
# ---------------------------------------------------------------------------------------------------


# ---------------------------------------------------------------------------------------------------
# Poczta — Azure Communication Services (odczyt istniejących zasobów, patrz email.tf)
# ---------------------------------------------------------------------------------------------------

variable "communication_service_name" {
  description = <<-EOT
    Nazwa ISTNIEJĄCEGO zasobu Azure Communication Services, z którego idą maile.
    Terraform go nie tworzy i nie usuwa — czyta z niego tylko connection string, żeby nie musiał
    przechodzić przez plik ani przez zmienną środowiskową.
  EOT
  type        = string
}

variable "communication_service_resource_group_name" {
  description = <<-EOT
    Grupa zasobów, w której stoi ACS. Puste = ta sama co `resource_group_name`.
  EOT
  type        = string
  default     = null
}

variable "email_sender_address" {
  description = <<-EOT
    Adres nadawcy maili z potwierdzeniem konta, resetem hasła i kodami 2FA.

    ⚠️ Musi należeć do domeny podpiętej do tego zasobu ACS — inaczej wysyłka jest odrzucana. Przy domenie
    zarządzanej przez Azure ma postać `DoNotReply@<guid>.azurecomm.net`; `<guid>` odczytasz w zasobie
    Email Service jako `mailFromSenderDomain`. Prowider Terraforma nie ma źródła danych na domeny poczty,
    więc trzeba to podać wprost.
  EOT
  type        = string
}

variable "location" {
  description = <<-EOT
    Region zasobow. Puste = region grupy zasobow.

    Grupa zasobow w Azure NIE ogranicza regionu zasobow w niej stojacych, wiec ta zmienna pozwala
    postawic aplikacje w innym regionie niz grupa. Powod praktyczny: limit darmowych planow F1 jest
    per region, wiec quota bywa w jednym regionie, a grupa w drugim.
  EOT
  type        = string
  default     = null
}

variable "app_service_sku" {
  description = <<-EOT
    Plan App Service dla obu aplikacji .NET.

    B1 zdejmuje trzy ograniczenia F1 naraz: limit 60 minut CPU na dobe, brak Always On (przez ktory
    zadania Hangfire nie wykonywaly sie bez ruchu na stronie) oraz brak wlasnych domen i certyfikatu.
    To ostatnie jest warunkiem postawienia Identity pod wlasna domena - bez tego ciasteczko sesji
    zostaje ciasteczkiem trzeciej strony i ciche odnawianie nie dziala w Safari.

    Naliczanie idzie od REZERWACJI, nie od zuzycia: plan kosztuje za kazda godzine swojego istnienia,
    takze przy zerowym ruchu i przy zatrzymanych aplikacjach. Powrot na F1 to zmiana tej wartosci.
  EOT
  type        = string
  default     = "B1"
}
