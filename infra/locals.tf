locals {
  prefix = "${var.project}-${var.environment}"

  tags = {
    project     = var.project
    environment = var.environment
    managed_by  = "terraform"
  }

  # Adresy App Service są przewidywalne z nazwy, więc da się ich użyć w ustawieniach obu aplikacji bez
  # cyklu w grafie zależności. Adresy Static Web Apps przewidywalne NIE są (Azure dokleja losowy człon),
  # więc wszędzie, gdzie są potrzebne, bierzemy je z atrybutu zasobu.
  api_url      = "https://${local.prefix}-api.azurewebsites.net"
  identity_url = "https://${local.prefix}-identity.azurewebsites.net"

  front_url   = "https://${azurerm_static_web_app.front.default_host_name}"
  landing_url = "https://${azurerm_static_web_app.landing.default_host_name}"

  # Katalog trwały na App Service dla Linuksa. ⚠️ Klucze ochrony danych MUSZĄ przeżyć restart: bez tego
  # każdy restart procesu unieważnia ciasteczka i wylogowuje wszystkich (patrz HostingDefaults).
  data_protection_keys_path = "/home/data-protection-keys"

  # Certyfikaty OpenIddict jadą razem z paczką wdrożeniową — patrz README, sekcja o certyfikatach.
  certs_dir = "/home/site/wwwroot/certs"
}
