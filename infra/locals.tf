locals {
  # Always On i proces 64-bitowy sa niedostepne w F1 i D1 - przy tych planach apply odrzuca oba
  # ustawienia. Wyprowadzamy je z SKU, zeby powrot na F1 byl zmiana JEDNEJ wartosci, a nie polowaniem
  # na trzy miejsca w konfiguracji.
  supports_always_on = !contains(["F1", "D1"], var.app_service_sku)

  # Pelne nazwy hostow, gdy domena wlasna jest ustawiona. Null = zostajemy na adresach Azure.
  custom = var.custom_domain != null && var.custom_domain != ""

  front_host    = local.custom ? "${var.subdomains.front}.${var.custom_domain}" : null
  identity_host = local.custom ? "${var.subdomains.identity}.${var.custom_domain}" : null
  api_host      = local.custom ? "${var.subdomains.api}.${var.custom_domain}" : null
  landing_host  = local.custom ? var.custom_domain : null

  location = coalesce(var.location, data.azurerm_resource_group.main.location)

  # ACS najczesciej stoi w tej samej grupie co reszta, ale nie musi - stad osobna zmienna z pustym
  # domyslnym. Zmienne nie moga odwolywac sie do innych zmiennych, wiec sklejenie jest tutaj.
  communication_service_resource_group = coalesce(var.communication_service_resource_group_name, var.resource_group_name)

  prefix = "${var.project}-${var.environment}"

  tags = {
    project     = var.project
    environment = var.environment
    managed_by  = "terraform"
  }

  # Adresy App Service są przewidywalne z nazwy, więc da się ich użyć w ustawieniach obu aplikacji bez
  # cyklu w grafie zależności. Adresy Static Web Apps przewidywalne NIE są (Azure dokleja losowy człon),
  # więc wszędzie, gdzie są potrzebne, bierzemy je z atrybutu zasobu.
  api_url      = "https://${coalesce(local.api_host, "${local.prefix}-api.azurewebsites.net")}"
  identity_url = "https://${coalesce(local.identity_host, "${local.prefix}-identity.azurewebsites.net")}"

  front_url   = "https://${coalesce(local.front_host, azurerm_static_web_app.front.default_host_name)}"
  landing_url = "https://${coalesce(local.landing_host, azurerm_static_web_app.landing.default_host_name)}"

  # Katalog trwały na App Service dla Linuksa. ⚠️ Klucze ochrony danych MUSZĄ przeżyć restart: bez tego
  # każdy restart procesu unieważnia ciasteczka i wylogowuje wszystkich (patrz HostingDefaults).
  data_protection_keys_path = "/home/data-protection-keys"

}
