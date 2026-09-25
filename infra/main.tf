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
    tls = {
      source  = "hashicorp/tls"
      version = "~> 4.0"
    }
  }

  # ⚠️ Wypełnij wartościami z `terraform output backend_config` w module bootstrap, ALBO przekaż je
  # przez `terraform init -backend-config=backend.hcl` (plik poza gitem). Zostawiam pusty blok, bo
  # nazwa konta magazynu ma losowy przyrostek i nie da się jej wpisać na sztywno w repozytorium.
  backend "azurerm" {
    use_azuread_auth = true
  }
}

provider "azurerm" {
  subscription_id = var.subscription_id
  features {}
}

# ⚠️ Grupa zasobów jest ISTNIEJĄCA, nie tworzona tutaj. Stoją w niej rzeczy, których Terraform nie zna
# i znać nie powinien (Azure Communication Services z domeną poczty), więc `terraform destroy` nie ma
# prawa jej ruszyć. Region zasobów bierze się z tej grupy — jedno źródło prawdy zamiast drugiej zmiennej,
# którą dałoby się ustawić niezgodnie.
data "azurerm_resource_group" "main" {
  name = var.resource_group_name
}
