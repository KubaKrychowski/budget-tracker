# Magazyn modeli kategoryzacji. Lokalnie zastępuje go Azurite; na Azure musi być prawdziwe konto, bo API
# przy starcie robi `new Uri(Configuration["Storage:BlobServiceUri"]!)` — brak wartości to wyjątek, nie
# tryb awaryjny. Sam model nie jest wymagany do działania aplikacji (bez niego kategoryzują same reguły),
# ale ADRES magazynu jest.

resource "random_string" "storage_suffix" {
  length  = 6
  special = false
  upper   = false
}

resource "azurerm_storage_account" "models" {
  name                = substr("${replace(local.prefix, "-", "")}models${random_string.storage_suffix.result}", 0, 24)
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  account_tier             = "Standard"
  account_replication_type = "LRS"

  https_traffic_only_enabled      = true
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false

  # ⚠️ Dostęp wyłącznie tożsamością zarządzaną API (przypisanie roli niżej). Klucze konta wyłączone, żeby
  # nie powstała druga, cicha droga dostępu, której nikt nie odbierze przy wycofaniu aplikacji.
  shared_access_key_enabled = false

  tags = local.tags
}

resource "azurerm_storage_container" "shared" {
  name                  = "shared"
  storage_account_id    = azurerm_storage_account.models.id
  container_access_type = "private"
}

# API czyta i zapisuje zbiór uczący oraz wytrenowane modele, więc potrzebuje zapisu, nie samego odczytu.
resource "azurerm_role_assignment" "api_blob" {
  scope                = azurerm_storage_account.models.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_web_app.api.identity[0].principal_id
}
