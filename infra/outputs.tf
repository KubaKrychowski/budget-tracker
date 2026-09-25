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
