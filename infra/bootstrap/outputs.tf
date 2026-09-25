output "backend_config" {
  description = "Wartości do bloku `backend \"azurerm\"` w module głównym (infra/main.tf)."
  value = {
    resource_group_name  = azurerm_resource_group.state.name
    storage_account_name = azurerm_storage_account.state.name
    container_name       = azurerm_storage_container.state.name
    key                  = "beta.tfstate"
    use_azuread_auth     = true
  }
}
