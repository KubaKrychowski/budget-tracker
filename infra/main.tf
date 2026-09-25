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

  # ⚠️ WYMAGANE, bo konto magazynu na modele ma wyłączone klucze dostępu. Bez tego provider próbuje
  # sięgnąć do warstwy danych kluczem i dostaje „403 Key based authentication is not permitted" już przy
  # TWORZENIU konta. Ta sama poprawka jest w module bootstrap — tam trafiła wcześniej, tu jej brakowało.
  storage_use_azuread = true

  features {}
}

# Tożsamość, którą działa Terraform — potrzebna, żeby nadać jej dostęp do danych w blobie przed
# utworzeniem konta magazynu. Bycie właścicielem subskrypcji tego dostępu NIE daje.
data "azurerm_client_config" "current" {}

# ⚠️ Grupa zasobów jest ISTNIEJĄCA, nie tworzona tutaj. Stoją w niej rzeczy, których Terraform nie zna
# i znać nie powinien (Azure Communication Services z domeną poczty), więc `terraform destroy` nie ma
# prawa jej ruszyć. Region zasobów bierze się z tej grupy — jedno źródło prawdy zamiast drugiej zmiennej,
# którą dałoby się ustawić niezgodnie.
data "azurerm_resource_group" "main" {
  name = var.resource_group_name
}
