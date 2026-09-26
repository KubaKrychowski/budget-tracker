output "api_url" {
  description = "Adres API budżetu."
  value       = local.api_url
}

output "identity_url" {
  description = "Adres serwera tożsamości. Panel administratora: <adres>/Admin/Invites."
  value       = local.identity_url
}

output "front_url" {
  description = "Adres aplikacji (Angular)."
  value       = local.front_url
}

output "landing_url" {
  description = "Adres strony projektu."
  value       = local.landing_url
}

# Tokeny wdrożeniowe trafiają do sekretów repozytorium GitHuba, z których korzysta akcja budująca front.
# `sensitive`, więc `terraform output` ich nie wypisze — trzeba poprosić o konkretny:
#   terraform output -raw front_deployment_token
output "front_deployment_token" {
  description = "Token wdrożeniowy Static Web App aplikacji."
  value       = azurerm_static_web_app.front.api_key
  sensitive   = true
}

output "landing_deployment_token" {
  description = "Token wdrożeniowy Static Web App strony projektu."
  value       = azurerm_static_web_app.landing.api_key
  sensitive   = true
}

output "models_storage_account" {
  description = "Konto magazynu na modele kategoryzacji."
  value       = azurerm_storage_account.models.name
}

# Rekordy, ktore trzeba zalozyc w Cloudflare ZANIM apply zwiaze domeny - Azure sprawdza wlasnosc
# odpytujac DNS.
#
# UWAGA: wyjscie jest BEZWARUNKOWE i dziala takze przy pustym custom_domain. Tak musi byc, bo te
# wartosci sa potrzebne WLASNIE WTEDY: najpierw zaklada sie rekordy, dopiero potem ustawia domene
# i wiaze. Wersja warunkowa zwracala pustke dokladnie w chwili, w ktorej byla potrzebna.
# odpytujac DNS. Proxy ma byc wylaczone (szara chmurka): przy wlaczonym Azure widzi adresy
# Cloudflare'a zamiast swoich i weryfikacja nie przechodzi.
output "dns_do_zalozenia" {
  description = "Rekordy DNS wymagane przez wiazania wlasnej domeny."
  value = {
    "${var.subdomains.front} (CNAME)"    = azurerm_static_web_app.front.default_host_name
    "${var.subdomains.identity} (CNAME)" = azurerm_linux_web_app.identity.default_hostname
    "${var.subdomains.api} (CNAME)"      = azurerm_linux_web_app.api.default_hostname
    "@ (CNAME, splaszczony)"             = azurerm_static_web_app.landing.default_host_name
  }
}
