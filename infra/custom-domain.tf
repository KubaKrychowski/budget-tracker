# Wiązania własnej domeny i certyfikaty. Cały plik jest warunkowy: przy pustym `custom_domain`
# nie powstaje nic i zostajemy na adresach nadanych przez Azure.
#
# ⚠️ KOLEJNOŚĆ JEST WYMUSZONA i nie da się jej skrócić:
#   1. rekordy DNS w Cloudflare (szara chmurka, bez proxy) — patrz README,
#   2. wiązanie nazwy hosta: Azure odpytuje DNS i sprawdza, czy wskazuje na jego zasób,
#   3. certyfikat zarządzany: wystawiany DOPIERO dla nazwy, która jest już związana,
#   4. podpięcie certyfikatu do wiązania — dopiero to włącza https.
# Terraform wyprowadza tę kolejność z zależności między zasobami, więc nie trzeba jej pilnować ręcznie.

resource "azurerm_app_service_custom_hostname_binding" "identity" {
  count = local.custom ? 1 : 0

  hostname            = local.identity_host
  app_service_name    = azurerm_linux_web_app.identity.name
  resource_group_name = data.azurerm_resource_group.main.name
}

resource "azurerm_app_service_custom_hostname_binding" "api" {
  count = local.custom ? 1 : 0

  hostname            = local.api_host
  app_service_name    = azurerm_linux_web_app.api.name
  resource_group_name = data.azurerm_resource_group.main.name
}

# Certyfikat zarządzany przez Azure: darmowy, odnawiany automatycznie, ważny tylko dla tej nazwy.
# ⚠️ Wymaga planu B1 lub wyższego — w F1 i D1 nie istnieje.
resource "azurerm_app_service_managed_certificate" "identity" {
  count = local.custom ? 1 : 0

  custom_hostname_binding_id = azurerm_app_service_custom_hostname_binding.identity[0].id
}

resource "azurerm_app_service_managed_certificate" "api" {
  count = local.custom ? 1 : 0

  custom_hostname_binding_id = azurerm_app_service_custom_hostname_binding.api[0].id
}

# Samo wystawienie certyfikatu niczego nie włącza — bez tego wiązania https na własnej nazwie
# kończy się ostrzeżeniem przeglądarki o niezgodnym certyfikacie.
resource "azurerm_app_service_certificate_binding" "identity" {
  count = local.custom ? 1 : 0

  hostname_binding_id = azurerm_app_service_custom_hostname_binding.identity[0].id
  certificate_id      = azurerm_app_service_managed_certificate.identity[0].id
  ssl_state           = "SniEnabled"
}

resource "azurerm_app_service_certificate_binding" "api" {
  count = local.custom ? 1 : 0

  hostname_binding_id = azurerm_app_service_custom_hostname_binding.api[0].id
  certificate_id      = azurerm_app_service_managed_certificate.api[0].id
  ssl_state           = "SniEnabled"
}

# Static Web Apps załatwia certyfikat samo, bez osobnych zasobów.
resource "azurerm_static_web_app_custom_domain" "front" {
  count = local.custom ? 1 : 0

  static_web_app_id = azurerm_static_web_app.front.id
  domain_name       = local.front_host
  validation_type   = "cname-delegation"
}

# ⚠️ Landing siedzi na samej domenie, więc weryfikacja idzie rekordem TXT, nie CNAME: na szczycie
# domeny nie wolno postawić CNAME-a obok innych rekordów. Cloudflare spłaszcza CNAME na szczycie,
# więc ruch zadziała — ale własność potwierdza się TXT-em.
resource "azurerm_static_web_app_custom_domain" "landing" {
  count = local.custom ? 1 : 0

  static_web_app_id = azurerm_static_web_app.landing.id
  domain_name       = local.landing_host
  validation_type   = "dns-txt-token"
}
