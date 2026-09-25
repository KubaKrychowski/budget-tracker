# Moduł „kura i jajko": stan Terraforma ma leżeć w Azure Storage, ale konta magazynu jeszcze nie ma,
# więc TEN moduł trzyma stan LOKALNIE i tworzy miejsce na stan modułu głównego. Uruchamia się go raz.
#
# ⚠️ Stan tego modułu jest odtwarzalny: gdyby plik zniknął, `terraform import` na trzech zasobach albo
# po prostu ręczne potwierdzenie, że konto magazynu istnieje, wystarczy. Stan modułu GŁÓWNEGO odtwarzalny
# NIE jest — dlatego to on idzie do Azure, a nie odwrotnie.

terraform {
  required_version = ">= 1.9"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

provider "azurerm" {
  subscription_id = var.subscription_id

  # ⚠️ WYMAGANE, skoro konto ma wyłączone klucze dostępu. Bez tego provider próbuje sięgnąć do warstwy
  # danych (sprawdzenie dostępności Blob Service, zakładanie kontenera) kluczem konta i dostaje
  # „403 Key based authentication is not permitted on this storage account" — już przy TWORZENIU konta,
  # nie dopiero przy kontenerze. Sprawdzone empirycznie 2026-09-25.
  storage_use_azuread = true

  features {}
}

resource "azurerm_resource_group" "state" {
  name     = "${var.project}-tfstate-rg"
  location = var.location
}

# Nazwa konta magazynu jest GLOBALNIE unikalna w całym Azure i dopuszcza tylko małe litery i cyfry,
# maksymalnie 24 znaki — stąd losowy przyrostek zamiast nazwy „opisowej", która i tak byłaby zajęta.
resource "random_string" "suffix" {
  length  = 6
  special = false
  upper   = false
}

resource "azurerm_storage_account" "state" {
  name                = substr("${replace(var.project, "-", "")}tfstate${random_string.suffix.result}", 0, 24)
  resource_group_name = azurerm_resource_group.state.name
  location            = azurerm_resource_group.state.location

  # ⚠️ Rola MUSI istnieć, zanim powstanie konto. Provider zaraz po utworzeniu odpytuje warstwę danych,
  # a przy wyłączonych kluczach robi to tożsamością wywołującego — bez przypisanej roli dostaje 403
  # i `apply` przerywa się z kontem, które już istnieje. Rola jest na grupie zasobów właśnie po to,
  # żeby dało się ją nadać przed kontem (na samym koncie byłby cykl).
  depends_on = [azurerm_role_assignment.state_writer]

  account_tier             = "Standard"
  account_replication_type = "LRS"

  # ⚠️ W stanie Terraforma leżą sekrety w postaci jawnej: connection string do bazy, hasło SMTP, sekret
  # klienta admina. Dlatego to konto jest zamknięte inaczej niż zwykłe konto na pliki.
  https_traffic_only_enabled      = true
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false

  # Dostęp kluczem wyłączony: stan czyta się tożsamością Entra ID (`use_azuread_auth` w backendzie),
  # więc klucz konta nie musi istnieć w żadnym pliku ani zmiennej CI.
  shared_access_key_enabled = false

  blob_properties {
    # Wersjonowanie to jedyna siatka pod błąd „apply nadpisał stan czymś niepełnym".
    versioning_enabled = true

    delete_retention_policy {
      days = 30
    }
  }
}

resource "azurerm_storage_container" "state" {
  name                  = "tfstate"
  storage_account_id    = azurerm_storage_account.state.id
  container_access_type = "private"
}

# Bez tego `terraform init` z `use_azuread_auth = true` dostaje 403: dostęp do DANYCH w blobie jest osobną
# rolą niż „jestem właścicielem subskrypcji" — Owner nie czyta blobów.
#
# ⚠️ Zakres to GRUPA ZASOBÓW, nie konto magazynu. Na koncie powstałby cykl: konto czeka na rolę (bo zaraz
# po utworzeniu odpytuje warstwę danych), a rola czeka na identyfikator konta. Grupa jest tu dedykowana
# wyłącznie stanowi, więc szerszy zakres nie daje dostępu do niczego poza nim.
resource "azurerm_role_assignment" "state_writer" {
  scope                = azurerm_resource_group.state.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = var.state_writer_principal_id
}
