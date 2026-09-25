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

resource "azurerm_resource_group" "main" {
  name     = "${local.prefix}-rg"
  location = var.location
  tags     = local.tags
}
