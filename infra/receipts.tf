# OCR paragonow: Azure AI Document Intelligence (model `prebuilt-receipt`). API wola go tozsamoscia zarzadzana,
# bez kluczy, tak samo jak magazyn blob w storage.tf. Obraz paragonu wychodzi do tej uslugi - patrz DECISIONS.md §15.

resource "azurerm_cognitive_account" "receipts" {
  name                = "${local.prefix}-receipts"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = var.receipts_ocr_location
  kind                = "FormRecognizer"
  sku_name            = var.receipts_ocr_sku

  # ⚠️ Uwierzytelnienie tozsamoscia (Entra ID) wymaga wlasnej subdomeny - bez niej `endpoint` jest regionalny
  # i odrzuca tokeny. Nazwa musi byc unikalna w skali Azure, stad ten sam losowy sufiks co przy koncie magazynu.
  custom_subdomain_name = "${local.prefix}-receipts-${random_string.storage_suffix.result}"

  # ⚠️ Klucze wylaczone, zeby nie powstala druga, cicha droga dostepu, ktorej nikt nie odbierze.
  local_auth_enabled = false

  tags = local.tags
}

resource "azurerm_role_assignment" "api_receipts" {
  scope                = azurerm_cognitive_account.receipts.id
  role_definition_name = "Cognitive Services User"
  principal_id         = azurerm_linux_web_app.api.identity[0].principal_id
}
